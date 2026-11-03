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
    // 5-minute boundary even though no event fired). Offline rows are
    // removed earlier if a live instance already serves the same URL.
    public sealed class PeerSyncStatusTracker : IDisposable
    {
        public static readonly TimeSpan SessionCacheRetention = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

        private readonly IPeerDiscoveryService _discovery;
        private readonly IDataContext _dataContext;
        private readonly Dispatcher _uiDispatcher;
        private readonly DispatcherTimer _refreshTimer;
        private readonly Func<DateTime> _clock;
        private readonly PeerSyncDiagnostics _diagnostics;
        private readonly Dictionary<Guid, PeerStatusViewModel> _byInstance = new Dictionary<Guid, PeerStatusViewModel>();

        public ObservableCollection<PeerStatusViewModel> Peers { get; } = new ObservableCollection<PeerStatusViewModel>();

        public PeerSyncStatusTracker(IPeerDiscoveryService discovery, IDataContext dc, Dispatcher uiDispatcher, Func<DateTime> clock = null, PeerSyncDiagnostics diagnostics = null)
        {
            _discovery = discovery;
            _dataContext = dc;
            _uiDispatcher = uiDispatcher;
            _clock = clock ?? (() => DateTime.UtcNow);
            _diagnostics = diagnostics;
            if (_diagnostics != null) _diagnostics.Changed += OnDiagnosticsChanged;

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
            var tournament = _dataContext?.Tournament;
            if (tournament == null || (tournament.ID.HasValue && peer.TournamentId != tournament.ID.Value)) return;
            if (!_byInstance.TryGetValue(peer.InstanceId, out var vm))
            {
                vm = new PeerStatusViewModel(peer.InstanceId);
                _byInstance[peer.InstanceId] = vm;
                Peers.Add(vm);
            }
            vm.Connection = peer;
            vm.UpdateLive(peer.NodeName, peer.StateHash, peer.LastSeenUtc, peer.HttpUrl, peer.ComputerName);
            Refresh();
        }

        private void MarkExpired(DiscoveredPeer peer)
        {
            if (peer == null) return;
            if (!_byInstance.TryGetValue(peer.InstanceId, out var vm)) return;
            // Expiry is raised outside the registry lock. The same instance
            // may already have returned as a new connection by this point.
            if (!ReferenceEquals(vm.Connection, peer)) return;
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
                if (!vm.IsLive && ((now - vm.LastSeenUtc) > SessionCacheRetention
                    || snapshot.Exists(other => other.IsLive && SharesEndpoint(vm.Address, other.Address))))
                {
                    _byInstance.Remove(vm.InstanceId);
                    Peers.Remove(vm);
                    continue;
                }
                vm.Diagnostic = _diagnostics?.Get(vm.InstanceId);
                vm.HasDuplicateName = vm.IsLive && snapshot.Exists(other => other.IsLive && other.InstanceId != vm.InstanceId
                    && string.Equals(other.NodeName, vm.NodeName, StringComparison.OrdinalIgnoreCase));
                vm.RecomputeStatus(localHash);
            }
        }

        private static bool SharesEndpoint(string first, string second)
        {
            return Uri.TryCreate(first, UriKind.Absolute, out var firstUri)
                && (firstUri.Scheme == Uri.UriSchemeHttp || firstUri.Scheme == Uri.UriSchemeHttps)
                && Uri.TryCreate(second, UriKind.Absolute, out var secondUri)
                && firstUri.Equals(secondUri);
        }

        public void Dispose()
        {
            if (_diagnostics != null) _diagnostics.Changed -= OnDiagnosticsChanged;
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

        private void OnDiagnosticsChanged() => RunUi(Refresh);
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
        public string Address { get; private set; }
        public string ComputerName { get; private set; }
        internal bool HasDuplicateName { get; set; }
        internal DiscoveredPeer Connection { get; set; }
        internal PeerSyncDiagnostic Diagnostic { get; set; }
        private string _details;
        public string Details { get => _details; private set { _details = value; Raise(); } }

        public PeerStatusViewModel(Guid instanceId)
        {
            InstanceId = instanceId;
        }

        public void UpdateLive(string nodeName, string stateHash, DateTime lastSeenUtc, string address = null, string computerName = null)
        {
            NodeName = string.IsNullOrWhiteSpace(nodeName) ? T("PeerSync_NoName", "(без имени)") : nodeName;
            StateHash = stateHash ?? string.Empty;
            LastSeenUtc = lastSeenUtc;
            IsLive = true;
            Address = address;
            ComputerName = computerName;
        }

        public void MarkDisconnected()
        {
            IsLive = false;
        }

        public void RecomputeStatus(string localHash)
        {
            var diagnostic = Diagnostic;
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(ComputerName)) lines.Add(ComputerName);
            if (HasDuplicateName) lines.Add(T("Network_DuplicatePeerName", "Одинаковое имя у разных компьютеров"));
            var address = Address ?? diagnostic?.Address;
            if (!string.IsNullOrEmpty(address)) lines.Add(Uri.TryCreate(address, UriKind.Absolute, out var url) ? url.GetLeftPart(UriPartial.Authority) : address);
            if (diagnostic != null)
            {
                lines.Add(T("PeerSync_LastAttempt", "Проверка") + ": " + diagnostic.LastAttemptUtc.ToLocalTime().ToString("HH:mm:ss"));
                if (diagnostic.LastSuccessUtc.HasValue)
                    lines.Add(T("PeerSync_LastSuccess", "Успешно") + ": " + diagnostic.LastSuccessUtc.Value.ToLocalTime().ToString("HH:mm:ss"));
                if (diagnostic.ImportedCount > 0)
                    lines.Add(T("PeerSync_ImportedCount", "Применено изменений") + ": " + diagnostic.ImportedCount);
                if (diagnostic.RetryAtUtc.HasValue)
                    lines.Add(T("PeerSync_RetryAt", "Следующая попытка") + ": " + diagnostic.RetryAtUtc.Value.ToLocalTime().ToString("HH:mm:ss"));
                foreach (var conflict in diagnostic.Conflicts)
                {
                    var reason = conflict.Reason switch
                    {
                        ImportConflictReason.DifferentResult => T("PeerSync_Conflict_Result", "Разные результаты одной версии"),
                        ImportConflictReason.DifferentParticipants => T("PeerSync_Conflict_Participants", "Разные участники встречи"),
                        ImportConflictReason.CompletedDependentMatch => T("PeerSync_Conflict_Dependent", "Следующая встреча уже завершена"),
                        ImportConflictReason.InvalidResult => T("PeerSync_Conflict_Invalid", "Неполный результат встречи"),
                        _ => T("PeerSync_Conflict_Structure", "Различается структура сетки")
                    };
                    lines.Add(conflict.GroupName + (conflict.MatchNumber.HasValue ? " №" + conflict.MatchNumber : "") + ": " + reason);
                }
                if (!string.IsNullOrEmpty(diagnostic.Detail)) lines.Add(diagnostic.Detail);
            }
            Details = string.Join(" · ", lines);
            if (!IsLive)
            {
                StatusGlyph = "⚠";
                StatusText = T("PeerSync_Offline", "не в сети");
                return;
            }
            if (diagnostic != null && diagnostic.Phase is not PeerSyncPhase.Synchronized)
            {
                StatusGlyph = diagnostic.Phase is PeerSyncPhase.Downloading or PeerSyncPhase.Applying or PeerSyncPhase.AwaitingSavedFile or PeerSyncPhase.LocalAhead ? "⏳" : "⚠";
                StatusText = diagnostic.Phase switch
                {
                    PeerSyncPhase.Downloading => T("PeerSync_Downloading", "получение файла"),
                    PeerSyncPhase.Applying => T("PeerSync_Applying", "применение изменений"),
                    PeerSyncPhase.NoNewData => T("PeerSync_NoNewData", "новых данных нет; состояния различаются"),
                    PeerSyncPhase.LocalAhead => T("PeerSync_LocalAhead", "локальная копия новее"),
                    PeerSyncPhase.AwaitingSavedFile => T("PeerSync_AwaitingSavedFile", "ожидание сохранения на другом компьютере"),
                    PeerSyncPhase.Conflict => T("PeerSync_Conflict", "конфликт результатов"),
                    PeerSyncPhase.FileUnavailable => T("PeerSync_FileUnavailable", "не удалось получить файл"),
                    PeerSyncPhase.TournamentMismatch => T("PeerSync_TournamentMismatch", "открыт другой турнир"),
                    PeerSyncPhase.SaveFailed => T("PeerSync_SaveFailed", "изменения не сохранены"),
                    _ => T("PeerSync_Error", "ошибка синхронизации")
                };
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
