using System;
using System.Collections.Generic;
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

        private readonly object _lock = new object();
        private readonly HashSet<Guid> _pendingPulls = new HashSet<Guid>();
        private readonly Dictionary<Guid, string> _lastPulledHashByPeer = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, FailureInfo> _failures = new Dictionary<Guid, FailureInfo>();

        public PeerSyncService(
            IPeerDiscoveryService discovery,
            IDataContext dataContext,
            ITournamentImporter importer,
            ITournamentsManager tournService,
            IResultsService resultsService,
            Dispatcher uiDispatcher,
            Func<DateTime> clock = null)
        {
            _discovery = discovery;
            _dataContext = dataContext;
            _importer = importer;
            _tournService = tournService;
            _resultsService = resultsService;
            _uiDispatcher = uiDispatcher;
            _clock = clock ?? (() => DateTime.UtcNow);

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
            lock (_lock)
            {
                _pendingPulls.Clear();
                _lastPulledHashByPeer.Clear();
                _failures.Clear();
            }
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
            if (peerHash == localHash) return;

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
                var outcome = await PullAndApplyAsync(peer, local);

                lock (_lock)
                {
                    if (IsCacheable(outcome))
                    {
                        // W1.2: only memorize the peer's announced hash when we
                        // actually managed to pull and apply (or saw nothing
                        // new). On failure outcomes we deliberately leave the
                        // cache alone so the next identical announce retries.
                        _lastPulledHashByPeer[peer.InstanceId] = peerHash;
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
                        }
                    }
                }
            }
            finally
            {
                lock (_lock) _pendingPulls.Remove(peer.InstanceId);
            }
        }

        // Treat outcomes that confirm a successful round-trip with the peer
        // as cacheable; everything else means we should retry on the next
        // identical announce.
        private static bool IsCacheable(ImportOutcome outcome)
        {
            return outcome == ImportOutcome.Imported || outcome == ImportOutcome.NoNewData;
        }

        private async Task<ImportOutcome> PullAndApplyAsync(DiscoveredPeer peer, Entities.Tournament target)
        {
            var source = !string.IsNullOrEmpty(peer.HttpUrl) ? peer.HttpUrl : peer.UncPath;
            if (string.IsNullOrEmpty(source)) return ImportOutcome.Error;
            if (target == null) return ImportOutcome.Error;

            ImportPlan plan;
            try
            {
                plan = await Task.Run(() => _importer.PrepareAsync(target, source)).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                FileLogger.Log("Sync.error", source, ex);
                return ImportOutcome.Error;
            }
            if (plan == null) return ImportOutcome.Error;

            // W2.4: between Task.Run completion and the synchronous Apply call
            // the user could have closed the tournament. Reject Apply against
            // a stale target — local Tournament reference is now different.
            if (_dataContext?.Tournament == null || !ReferenceEquals(_dataContext.Tournament, target))
            {
                FileLogger.Log("Sync.skip", source, "tournament changed during prepare");
                return ImportOutcome.Error;
            }

            ImportResult result;
            try
            {
                result = plan.NeedsApply
                    ? _importer.Apply(target, plan)
                    : new ImportResult(plan.ShortCircuit ?? ImportOutcome.Error, 0);
            }
            catch (Exception ex)
            {
                FileLogger.Log("Sync.error", source, ex);
                return ImportOutcome.Error;
            }

            FileLogger.Log("Sync.apply", peer.NodeName ?? source, result.Outcome + ":" + result.ImportedCount);

            if (result.Outcome == ImportOutcome.Imported)
            {
                _resultsService?.Recalculate(target);

                await SaveIfAutosaveEnabledAsync(target);
            }

            return result.Outcome;
        }

        private async Task SaveIfAutosaveEnabledAsync(Entities.Tournament target)
        {
            if (target?.Settings == null) return;
            if (!target.Settings.IsAutosaveEnabled) return;
            if (string.IsNullOrEmpty(target.FileName)) return;

            try
            {
                await _tournService.SaveToFileAsync(target, target.FileName);
            }
            catch (Exception ex)
            {
                FileLogger.Log("Sync.error", target.FileName, ex);
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
        }

        private sealed class FailureInfo
        {
            public int ConsecutiveFailures;
            public DateTime? CooldownUntilUtc;
        }
    }
}
