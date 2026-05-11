using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Wrestling.Providers.Network;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Model
{
    // Maintains an observable, UI-friendly list of peers for the Dashboard
    // "Синхронизация" card. Differs from PeerSyncService:
    //   - This is a read model. It never initiates a pull.
    //   - It includes a 5-minute session-cache so a peer that drops off the
    //     network stays visible (with a "Disconnected" status) long enough for
    //     the operator to notice, instead of silently disappearing the moment
    //     PeerRegistry expires it.
    //
    // Status is recomputed periodically on the UI dispatcher because some
    // states age out by time (a recently-disconnected peer crosses the
    // 5-minute boundary even though no event fired).
    public sealed class PeerSyncStatusTracker : IDisposable
    {
        public static readonly TimeSpan SessionCacheRetention = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

        private readonly IPeerDiscoveryService _discovery;
        private readonly IDataContext _dataContext;
        private readonly Dispatcher _uiDispatcher;
        private readonly DispatcherTimer _refreshTimer;
        private readonly Func<DateTime> _clock;
        private readonly Dictionary<Guid, PeerStatusViewModel> _byInstance = new Dictionary<Guid, PeerStatusViewModel>();

        public ObservableCollection<PeerStatusViewModel> Peers { get; } = new ObservableCollection<PeerStatusViewModel>();

        public PeerSyncStatusTracker(IPeerDiscoveryService discovery, IDataContext dc, Dispatcher uiDispatcher, Func<DateTime> clock = null)
        {
            _discovery = discovery;
            _dataContext = dc;
            _uiDispatcher = uiDispatcher;
            _clock = clock ?? (() => DateTime.UtcNow);

            if (_discovery != null)
            {
                _discovery.PeerUpserted += OnPeerUpserted;
                _discovery.PeerExpired += OnPeerExpired;
            }
            if (_dataContext != null)
            {
                _dataContext.TournamentChanged += OnTournamentChanged;
            }

            // DispatcherTimer requires a running Dispatcher (i.e. WPF UI). In
            // unit tests we pass a null dispatcher and call Refresh() directly.
            if (_uiDispatcher != null)
            {
                _refreshTimer = new DispatcherTimer { Interval = RefreshInterval };
                _refreshTimer.Tick += (s, e) => Refresh();
                _refreshTimer.Start();
            }
        }

        private void OnTournamentChanged(object sender, Entities.Tournament tournament)
        {
            RunUi(() =>
            {
                _byInstance.Clear();
                Peers.Clear();
            });
        }

        private void OnPeerUpserted(object sender, DiscoveredPeer peer)
        {
            RunUi(() => UpsertPeer(peer));
        }

        private void OnPeerExpired(object sender, DiscoveredPeer peer)
        {
            RunUi(() => MarkExpired(peer));
        }

        private void RunUi(Action action)
        {
            if (_uiDispatcher == null || _uiDispatcher.CheckAccess()) action();
            else _uiDispatcher.BeginInvoke(action);
        }

        private void UpsertPeer(DiscoveredPeer peer)
        {
            if (peer == null) return;
            if (!_byInstance.TryGetValue(peer.InstanceId, out var vm))
            {
                vm = new PeerStatusViewModel(peer.InstanceId);
                _byInstance[peer.InstanceId] = vm;
                Peers.Add(vm);
            }
            vm.UpdateLive(peer.NodeName, peer.StateHash, peer.LastSeenUtc);
            Refresh();
        }

        private void MarkExpired(DiscoveredPeer peer)
        {
            if (peer == null) return;
            if (!_byInstance.TryGetValue(peer.InstanceId, out var vm)) return;
            vm.MarkDisconnected();
            Refresh();
        }

        // Internal so tests can drive the refresh tick without spinning up a
        // real WPF DispatcherTimer.
        internal void Refresh()
        {
            var local = _dataContext?.Tournament;
            var localHash = local != null ? PeerStateHasher.Compute(local) : string.Empty;
            var now = _clock();

            // Iterate snapshot to allow removal mid-loop.
            var snapshot = new List<PeerStatusViewModel>(_byInstance.Values);
            foreach (var vm in snapshot)
            {
                if (!vm.IsLive && (now - vm.LastSeenUtc) > SessionCacheRetention)
                {
                    _byInstance.Remove(vm.InstanceId);
                    Peers.Remove(vm);
                    continue;
                }
                vm.RecomputeStatus(localHash);
            }
        }

        public void Dispose()
        {
            _refreshTimer?.Stop();
            if (_discovery != null)
            {
                _discovery.PeerUpserted -= OnPeerUpserted;
                _discovery.PeerExpired -= OnPeerExpired;
            }
            if (_dataContext != null)
            {
                _dataContext.TournamentChanged -= OnTournamentChanged;
            }
        }
    }

    public sealed class PeerStatusViewModel : INotifyPropertyChanged
    {
        public Guid InstanceId { get; }

        private string _nodeName;
        public string NodeName
        {
            get => _nodeName;
            private set { _nodeName = value; Raise(); }
        }

        // Glyph rendered next to the name in the Card. Kept as plain text so
        // the XAML doesn't need a converter; "✅" / "⏳" / "⚠" map to the three
        // states described in PeerSyncStatusTracker.
        private string _statusGlyph = "⏳";
        public string StatusGlyph
        {
            get => _statusGlyph;
            private set { _statusGlyph = value; Raise(); }
        }

        // Initialized to the Russian fallback; recomputed by RecomputeStatus
        // on the next tick so a language switch reaches the UI within seconds.
        private string _statusText = T("PeerSync_Waiting", "ожидание");

        private static string T(string key, string fallback)
        {
            var value = LocalizationService.Instance?.T(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }
        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; Raise(); }
        }

        public string StateHash { get; private set; } = string.Empty;
        public DateTime LastSeenUtc { get; private set; }
        public bool IsLive { get; private set; }

        public PeerStatusViewModel(Guid instanceId)
        {
            InstanceId = instanceId;
        }

        public void UpdateLive(string nodeName, string stateHash, DateTime lastSeenUtc)
        {
            NodeName = string.IsNullOrWhiteSpace(nodeName) ? T("PeerSync_NoName", "(без имени)") : nodeName;
            StateHash = stateHash ?? string.Empty;
            LastSeenUtc = lastSeenUtc;
            IsLive = true;
        }

        public void MarkDisconnected()
        {
            IsLive = false;
        }

        public void RecomputeStatus(string localHash)
        {
            if (!IsLive)
            {
                StatusGlyph = "⚠";
                StatusText = T("PeerSync_Offline", "не в сети");
                return;
            }
            if (string.IsNullOrEmpty(StateHash))
            {
                StatusGlyph = "⏳";
                StatusText = T("PeerSync_Unknown", "состояние неизвестно");
                return;
            }
            if (StateHash == localHash)
            {
                StatusGlyph = "✅";
                StatusText = T("PeerSync_InSync", "синхронизирован");
            }
            else
            {
                StatusGlyph = "⏳";
                StatusText = T("PeerSync_CatchingUp", "догоняет");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
