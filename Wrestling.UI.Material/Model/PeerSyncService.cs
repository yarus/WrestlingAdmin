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
    // Design notes (see docs/TodoList.md and conversation 2026-05-02):
    //   - No timer. Convergence is signal-driven: announce → divergence → pull.
    //   - PrepareAsync runs on a threadpool thread; Apply marshals to the UI
    //     dispatcher because it touches ObservableCollection<T> and INPC.
    //   - Pulls are de-duplicated per peer InstanceId: only one in-flight pull
    //     per peer; if the peer's hash hasn't changed since the last accepted
    //     pull, skip (avoid livelock when our hash naturally differs from
    //     theirs because we have local-newer match completions they don't).
    //   - Autosave hook fires only on outcome=Imported, mirroring the old
    //     ImportViewModel behaviour. Uses ITournamentsManager directly so the
    //     service does not depend on TournamentViewModelBase.
    public sealed class PeerSyncService : IDisposable
    {
        private readonly IPeerDiscoveryService _discovery;
        private readonly IDataContext _dataContext;
        private readonly ITournamentImporter _importer;
        private readonly ITournamentsManager _tournService;
        private readonly Dispatcher _uiDispatcher;

        private readonly object _lock = new object();
        private readonly HashSet<Guid> _pendingPulls = new HashSet<Guid>();
        private readonly Dictionary<Guid, string> _lastPulledHashByPeer = new Dictionary<Guid, string>();

        public PeerSyncService(
            IPeerDiscoveryService discovery,
            IDataContext dataContext,
            ITournamentImporter importer,
            ITournamentsManager tournService,
            Dispatcher uiDispatcher)
        {
            _discovery = discovery;
            _dataContext = dataContext;
            _importer = importer;
            _tournService = tournService;
            _uiDispatcher = uiDispatcher;

            _discovery.PeerUpserted += OnPeerUpserted;
            if (_dataContext != null) _dataContext.TournamentChanged += OnTournamentChanged;
        }

        // Reset the per-peer "last pulled hash" cache when the tournament
        // changes — old hashes are meaningless against a new local state.
        private void OnTournamentChanged(object sender, Entities.Tournament tournament)
        {
            lock (_lock)
            {
                _pendingPulls.Clear();
                _lastPulledHashByPeer.Clear();
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

        private async Task HandlePeerAsync(DiscoveredPeer peer)
        {
            var local = _dataContext?.Tournament;
            if (local == null) return;

            var peerHash = peer.StateHash ?? string.Empty;
            if (string.IsNullOrEmpty(peerHash)) return;

            var localHash = PeerStateHasher.Compute(local);
            if (peerHash == localHash) return;

            lock (_lock)
            {
                if (_lastPulledHashByPeer.TryGetValue(peer.InstanceId, out var prev) && prev == peerHash) return;
                if (!_pendingPulls.Add(peer.InstanceId)) return;
            }

            try
            {
                FileLogger.Log("Sync.divergence", peer.NodeName ?? peer.HttpUrl ?? "<peer>", "remote=" + peerHash + " local=" + localHash);
                await PullAndApplyAsync(peer);
                lock (_lock) _lastPulledHashByPeer[peer.InstanceId] = peerHash;
            }
            finally
            {
                lock (_lock) _pendingPulls.Remove(peer.InstanceId);
            }
        }

        private async Task PullAndApplyAsync(DiscoveredPeer peer)
        {
            var source = !string.IsNullOrEmpty(peer.HttpUrl) ? peer.HttpUrl : peer.UncPath;
            if (string.IsNullOrEmpty(source)) return;

            var target = _dataContext.Tournament;
            if (target == null) return;

            ImportPlan plan;
            try
            {
                plan = await Task.Run(() => _importer.PrepareAsync(target, source)).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                FileLogger.Log("Sync.error", source, ex);
                return;
            }
            if (plan == null) return;

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
                return;
            }

            FileLogger.Log("Sync.apply", peer.NodeName ?? source, result.Outcome + ":" + result.ImportedCount);

            if (result.Outcome == ImportOutcome.Imported)
            {
                await SaveIfAutosaveEnabledAsync(target);
            }
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
            _discovery.PeerUpserted -= OnPeerUpserted;
            if (_dataContext != null) _dataContext.TournamentChanged -= OnTournamentChanged;
        }
    }
}
