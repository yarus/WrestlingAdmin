using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Wrestling.DataAccess;
using Wrestling.Providers;
using Wrestling.Providers.Network;

namespace Wrestling.UI.Material.Model
{
    // Event-driven replacement for the old DispatcherTimer-based pull import.
    // Subscribes to PeerDiscoveryService events and reacts to advertisements
    // whose StateHash differs from the local tournament's hash by initiating
    // a pull-and-apply against that peer.
    //
    // Reliability invariants (see docs/TodoList.md and conversation 2026-05-03):
    //   - No timer. Convergence is signal-driven: announce → divergence → pull.
    //   - PrepareAsync runs on a threadpool thread; Apply marshals to the UI
    //     dispatcher because it touches ObservableCollection<T> and INPC.
    //   - Pulls are de-duplicated per peer InstanceId: only one in-flight pull
    //     per peer.
    //   - **W1.2**: _lastPulledHashByPeer is updated ONLY when the pull
    //     produced a usable outcome (Imported/NoNewData). On FileUnavailable /
    //     Error / TournamentMismatch the cache stays stale so the next
    //     identical-hash announce will retry. Otherwise a peer that briefly
    //     becomes unreachable would never be retried until they bumped the hash.
    //   - **W2.2**: PeerExpired removes per-peer entries so the dictionaries
    //     don't grow over a long session.
    //   - **W2.3**: After 3 consecutive pull failures from a peer, that peer
    //     enters a 30-second cooldown — HandlePeerAsync returns early. One
    //     downed peer cannot keep dragging UI thread through 5-second HTTP
    //     timeouts on every announce.
    //   - **W2.4**: Tournament reference is rechecked on the UI thread after
    //     the threadpool Prepare. If the user closed the tournament during the
    //     download, Apply is skipped — preventing mutations against a stale
    //     graph.
    //   - Autosave hook fires only on outcome=Imported, mirroring the old
    //     ImportViewModel behaviour.
    public sealed class PeerSyncService : IDisposable
    {
        private const int FailuresBeforeCooldown = 3;
        private static readonly TimeSpan FailureCooldown = TimeSpan.FromSeconds(30);

        private readonly IPeerDiscoveryService _discovery;
        private readonly IDataContext _dataContext;
        private readonly ITournamentImporter _importer;
        private readonly ITournamentsManager _tournService;
        private readonly IResultsService _resultsService;
        private readonly Dispatcher _uiDispatcher;
        private readonly Func<DateTime> _clock;
        private readonly PeerSyncDiagnostics _diagnostics;
        private bool _savePending;

        private readonly object _lock = new object();
        private readonly HashSet<Guid> _pendingPulls = new HashSet<Guid>();
        private readonly Dictionary<Guid, string> _lastPulledHashByPeer = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, FailureInfo> _failures = new Dictionary<Guid, FailureInfo>();
        // Tournament-scoped cancellation. All in-flight pulls share this token;
        // when the tournament changes (operator closed it / opened another),
        // the source is cancelled so any HTTP fetch on the threadpool stops
        // immediately instead of fighting through retries against an obsolete
        // peer.
        private CancellationTokenSource _tournamentCts = new CancellationTokenSource();

        public PeerSyncService(
            IPeerDiscoveryService discovery,
            IDataContext dataContext,
            ITournamentImporter importer,
            ITournamentsManager tournService,
            IResultsService resultsService,
            Dispatcher uiDispatcher,
            Func<DateTime> clock = null,
            PeerSyncDiagnostics diagnostics = null)
        {
            _discovery = discovery;
            _dataContext = dataContext;
            _importer = importer;
            _tournService = tournService;
            _resultsService = resultsService;
            _uiDispatcher = uiDispatcher;
            _clock = clock ?? (() => DateTime.UtcNow);
            _diagnostics = diagnostics;

            if (_discovery != null)
            {
                _discovery.PeerUpserted += OnPeerUpserted;
                _discovery.PeerExpired += OnPeerExpired;
            }
            if (_dataContext != null) _dataContext.TournamentChanged += OnTournamentChanged;
        }

        // Reset the per-peer caches when the tournament changes — old hashes
        // are meaningless against a new local state.
        private void OnTournamentChanged(object sender, Entities.Tournament tournament)
        {
            CancellationTokenSource oldCts;
            lock (_lock)
            {
                _pendingPulls.Clear();
                _lastPulledHashByPeer.Clear();
                _failures.Clear();
                _savePending = false;
                _diagnostics?.Clear();

                oldCts = _tournamentCts;
                _tournamentCts = new CancellationTokenSource();
            }

            // Cancel any in-flight HTTP fetches against the now-stale tournament
            // graph. New peers will use the fresh token.
            try { oldCts.Cancel(); } catch { /* already disposed */ }
            try { oldCts.Dispose(); } catch { /* idempotent */ }
        }

        // W2.2: drop per-peer state when discovery declares the peer expired.
        // Keeping it would slowly grow the dicts over a long tournament session
        // with reconnections; if the same peer comes back, its first new
        // announce starts from a clean slate.
        private void OnPeerExpired(object sender, DiscoveredPeer peer)
        {
            if (peer == null) return;
            lock (_lock)
            {
                _pendingPulls.Remove(peer.InstanceId);
                _lastPulledHashByPeer.Remove(peer.InstanceId);
                _failures.Remove(peer.InstanceId);
                _diagnostics?.Remove(peer.InstanceId);
            }
        }

        // PeerUpserted fires from the discovery receive thread. Hand off to
        // the UI dispatcher early — Apply has to run there anyway, and keeping
        // the divergence check on the UI thread keeps DataContext.Tournament
        // reads coherent with whatever else might be mutating it.
        private void OnPeerUpserted(object sender, DiscoveredPeer peer)
        {
            if (peer == null) return;
            if (_uiDispatcher == null) return;

            if (_uiDispatcher.CheckAccess()) _ = HandlePeerAsync(peer);
            else _uiDispatcher.BeginInvoke(new Action(() => _ = HandlePeerAsync(peer)));
        }

        internal async Task HandlePeerAsync(DiscoveredPeer peer)
        {
            var local = _dataContext?.Tournament;
            if (local == null) return;

            var peerHash = peer.StateHash ?? string.Empty;
            if (string.IsNullOrEmpty(peerHash)) return;

            var localHash = PeerStateHasher.Compute(local);
            if (_savePending)
            {
                var saved = await SaveAfterMergeAsync(local, _tournamentCts.Token);
                if (!ReferenceEquals(_dataContext?.Tournament, local)) return;
                if (!saved)
                {
                    _diagnostics?.Report(peer.InstanceId, peer.HttpUrl, PeerSyncPhase.SaveFailed, _clock());
                    return;
                }
                localHash = PeerStateHasher.Compute(local);
            }
            if (peerHash == localHash)
            {
                _diagnostics?.Report(peer.InstanceId, peer.HttpUrl, PeerSyncPhase.Synchronized, _clock());
                return;
            }

            lock (_lock)
            {
                // W2.3: respect circuit-breaker cooldown.
                if (_failures.TryGetValue(peer.InstanceId, out var failure)
                    && failure.CooldownUntilUtc.HasValue
                    && _clock() < failure.CooldownUntilUtc.Value)
                {
                    return;
                }

                if (_lastPulledHashByPeer.TryGetValue(peer.InstanceId, out var prev) && prev == peerHash) return;
                if (!_pendingPulls.Add(peer.InstanceId)) return;
            }

            try
            {
                FileLogger.Log("Sync.divergence", peer.NodeName ?? peer.HttpUrl ?? "<peer>", "remote=" + peerHash + " local=" + localHash);
                var pull = await PullAndApplyAsync(peer, local);
                var outcome = pull.Outcome;

                if (!ReferenceEquals(_dataContext?.Tournament, local)) return;

                lock (_lock)
                {
                    if (IsCacheable(outcome))
                    {
                        // W1.2: only memorize the peer's announced hash when we
                        // actually managed to pull and apply (or saw nothing
                        // new). On failure outcomes we deliberately leave the
                        // cache alone so the next identical announce retries.
                        // The announcement describes live state; HTTP serves the
                        // last saved file. Do not cache an unseen live revision.
                        if (pull.StateHash == peerHash) _lastPulledHashByPeer[peer.InstanceId] = peerHash;
                        else _lastPulledHashByPeer.Remove(peer.InstanceId);
                        _failures.Remove(peer.InstanceId);
                    }
                    else
                    {
                        // W2.3: bump failure count, arm cooldown if at threshold.
                        if (!_failures.TryGetValue(peer.InstanceId, out var failure))
                        {
                            failure = new FailureInfo();
                            _failures[peer.InstanceId] = failure;
                        }
                        failure.ConsecutiveFailures++;
                        if (failure.ConsecutiveFailures >= FailuresBeforeCooldown)
                        {
                            failure.CooldownUntilUtc = _clock() + FailureCooldown;
                            FileLogger.Log("Sync.cooldown", peer.NodeName ?? "<peer>",
                                "fails=" + failure.ConsecutiveFailures + " until=" + failure.CooldownUntilUtc.Value.ToString("o"));
                            _diagnostics?.SetRetry(peer.InstanceId, failure.CooldownUntilUtc.Value);
                        }
                    }
                }
            }
            finally
            {
                lock (_lock)
                    if (ReferenceEquals(_dataContext?.Tournament, local)) _pendingPulls.Remove(peer.InstanceId);
            }
        }

        // Treat outcomes that confirm a successful round-trip with the peer
        // as cacheable; everything else means we should retry on the next
        // identical announce.
        private static bool IsCacheable(ImportOutcome outcome)
        {
            return outcome == ImportOutcome.Imported || outcome == ImportOutcome.NoNewData;
        }

        private async Task<(ImportOutcome Outcome, string StateHash)> PullAndApplyAsync(DiscoveredPeer peer, Entities.Tournament target)
        {
            var source = peer.HttpUrl;
            if (string.IsNullOrEmpty(source))
            {
                _diagnostics?.Report(peer.InstanceId, source, PeerSyncPhase.FileUnavailable, _clock());
                return (ImportOutcome.FileUnavailable, null);
            }
            if (target == null) return (ImportOutcome.Error, null);

            CancellationToken ct;
            lock (_lock) ct = _tournamentCts.Token;

            ImportPlan plan;
            _diagnostics?.Report(peer.InstanceId, source, PeerSyncPhase.Downloading, _clock());
            try
            {
                plan = await Task.Run(() => _importer.PrepareAsync(target, source, ct), ct).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Tournament closed/swapped during fetch — silent skip, no log spam.
                return (ImportOutcome.Error, null);
            }
            catch (Exception ex)
            {
                FileLogger.Log("Sync.error", source, ex);
                if (ReferenceEquals(_dataContext?.Tournament, target))
                    _diagnostics?.Report(peer.InstanceId, source, PeerSyncPhase.Error, _clock(), detail: ex.Message);
                return (ImportOutcome.Error, null);
            }
            if (plan == null)
            {
                _diagnostics?.Report(peer.InstanceId, source, PeerSyncPhase.Error, _clock());
                return (ImportOutcome.Error, null);
            }

            // W2.4: between Task.Run completion and the synchronous Apply call
            // the user could have closed the tournament. Reject Apply against
            // a stale target — local Tournament reference is now different.
            if (_dataContext?.Tournament == null || !ReferenceEquals(_dataContext.Tournament, target))
            {
                FileLogger.Log("Sync.skip", source, "tournament changed during prepare");
                return (ImportOutcome.Error, null);
            }

            var downloadedHash = plan.Remote == null ? null : PeerStateHasher.Compute(plan.Remote);
            ImportResult result;
            _diagnostics?.Report(peer.InstanceId, source, PeerSyncPhase.Applying, _clock());
            try
            {
                result = plan.NeedsApply
                    ? _importer.Apply(target, plan)
                    : new ImportResult(plan.ShortCircuit ?? ImportOutcome.Error, 0);
            }
            catch (Exception ex)
            {
                FileLogger.Log("Sync.error", source, ex);
                _diagnostics?.Report(peer.InstanceId, source, PeerSyncPhase.Error, _clock(), detail: ex.Message);
                return (ImportOutcome.Error, null);
            }

            FileLogger.Log("Sync.apply", peer.NodeName ?? source, result.Outcome + ":" + result.ImportedCount);

            if (result.ImportedCount > 0 || result.Outcome == ImportOutcome.Imported)
            {
                _resultsService?.Recalculate(target);

                if (!await SaveAfterMergeAsync(target, ct))
                {
                    if (ReferenceEquals(_dataContext?.Tournament, target))
                        _diagnostics?.Report(peer.InstanceId, source, PeerSyncPhase.SaveFailed, _clock(), result.ImportedCount);
                    return (ImportOutcome.Error, downloadedHash);
                }
            }

            if (!ReferenceEquals(_dataContext?.Tournament, target)) return (ImportOutcome.Error, null);
            var phase = result.Outcome switch
            {
                ImportOutcome.Conflict => PeerSyncPhase.Conflict,
                ImportOutcome.FileUnavailable => PeerSyncPhase.FileUnavailable,
                ImportOutcome.TournamentMismatch => PeerSyncPhase.TournamentMismatch,
                ImportOutcome.Error => PeerSyncPhase.Error,
                _ when downloadedHash != peer.StateHash => PeerSyncPhase.AwaitingSavedFile,
                _ when PeerStateHasher.Compute(target) == downloadedHash => PeerSyncPhase.Synchronized,
                _ when plan.Remote != null && LocalVersionsAreNewer(target, plan.Remote) => PeerSyncPhase.LocalAhead,
                _ => PeerSyncPhase.NoNewData
            };
            _diagnostics?.Report(peer.InstanceId, source, phase, _clock(), result.ImportedCount, conflicts: result.Conflicts);

            return (result.Outcome, downloadedHash);
        }

        private static bool LocalVersionsAreNewer(Entities.Tournament local, Entities.Tournament remote)
        {
            bool newer = local.MetaVersion > remote.MetaVersion;
            if (local.MetaVersion < remote.MetaVersion) return false;
            foreach (var group in remote.Groups)
            {
                var own = local.Groups.FirstOrDefault(g => g.ID == group.ID);
                if (own == null || own.FieldsVersion < group.FieldsVersion || own.BracketVersion < group.BracketVersion) return false;
                newer |= own.FieldsVersion > group.FieldsVersion || own.BracketVersion > group.BracketVersion;
                if (own.BracketVersion != group.BracketVersion || own.Bracket == null || group.Bracket == null) continue;
                var matches = own.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToDictionary(m => m.BracketFullNumber);
                foreach (var m in group.Bracket.Rounds.SelectMany(r => r.RoundMatches))
                {
                    if (!matches.TryGetValue(m.BracketFullNumber, out var ownMatch) || ownMatch.SyncVersion < m.SyncVersion) return false;
                    newer |= ownMatch.SyncVersion > m.SyncVersion;
                }
            }
            foreach (var mat in remote.Mats)
            {
                var own = local.Mats.FirstOrDefault(m => m.ID == mat.ID);
                if (own == null || own.FieldsVersion < mat.FieldsVersion) return false;
                newer |= own.FieldsVersion > mat.FieldsVersion;
            }
            foreach (var wrestler in remote.Wrestlers)
            {
                var own = local.Wrestlers.FirstOrDefault(w => w.ID == wrestler.ID);
                if (own == null) return false;
                var localTime = own.Timestamp?.ToUniversalTime() ?? DateTime.MinValue;
                var remoteTime = wrestler.Timestamp?.ToUniversalTime() ?? DateTime.MinValue;
                if (localTime < remoteTime) return false;
                newer |= localTime > remoteTime;
            }
            return newer;
        }

        // Persist after a peer-sync merge. Same gate as the UI-side
        // SaveIfAutosaveEnabledAsync hook: no FileName means the operator
        // never picked a save path, so we skip silently rather than pop a
        // dialog from a background sync tick.
        private async Task<bool> SaveAfterMergeAsync(Entities.Tournament target, CancellationToken cancellationToken)
        {
            if (target == null) return false;
            if (ReferenceEquals(_dataContext?.Tournament, target)) _savePending = true;
            if (string.IsNullOrEmpty(target.FileName)) return false;

            try
            {
                var saved = await _tournService.SaveToFileAsync(target, target.FileName, cancellationToken);
                if (ReferenceEquals(_dataContext?.Tournament, target)) _savePending = !saved;
                if (!saved) FileLogger.Log("Sync.save", target.FileName, "save returned false; retry required");
                return saved;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Tournament closed while autosave was in flight — silent skip.
                return false;
            }
            catch (Exception ex)
            {
                FileLogger.Log("Sync.error", target.FileName, ex);
                if (ReferenceEquals(_dataContext?.Tournament, target)) _savePending = true;
                return false;
            }
        }

        public void Dispose()
        {
            if (_discovery != null)
            {
                _discovery.PeerUpserted -= OnPeerUpserted;
                _discovery.PeerExpired -= OnPeerExpired;
            }
            if (_dataContext != null) _dataContext.TournamentChanged -= OnTournamentChanged;

            CancellationTokenSource cts;
            lock (_lock) cts = _tournamentCts;
            try { cts.Cancel(); } catch { }
            try { cts.Dispose(); } catch { }
        }

        private sealed class FailureInfo
        {
            public int ConsecutiveFailures;
            public DateTime? CooldownUntilUtc;
        }
    }
}
