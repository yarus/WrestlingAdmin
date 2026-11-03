using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows.Threading;
using Wrestling.Entities;
using Wrestling.Providers.Network;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Model
{
    // Machine identity is local; endpoints are runtime state and never come from .wrt.
    public sealed class NetworkServicesLifecycle : ObservableObject, IDisposable
    {
        private static readonly TimeSpan FirewallHintDelay = TimeSpan.FromSeconds(30);
        private readonly IDataContext _dataContext;
        private readonly IPeerDiscoveryService _discovery;
        private readonly ITournamentHttpServer _httpServer;
        private readonly Dispatcher _uiDispatcher;
        private readonly Func<int, int?, FirewallAccess> _firewallAccess;
        private readonly Func<IReadOnlyList<LocalIpAddressProbe.LanAddress>> _addresses;
        private readonly bool _watchNetwork;
        private readonly object _timerLock = new object();
        private Entities.Tournament _subscribedTournament;
        private Timer _firewallHintTimer;
        private Timer _networkChangeTimer;
        private int _networkGeneration;
        private volatile bool _disposed;
        private IReadOnlyList<LocalIpAddressProbe.LanAddress> _localAddresses = Array.Empty<LocalIpAddressProbe.LanAddress>();
        private string _discoveryError;
        private string _httpError;
        private string _firewallNote;
        private FirewallAccess? _checkedFirewall;
        private string _startedFileName;
        private string _startedTitle;
        private Guid? _startedTournamentId;

        public LocalNetworkIdentity Identity { get; }
        public event EventHandler<string> DiagnosticMessage;

        public NetworkServicesLifecycle(IDataContext dataContext, IPeerDiscoveryService discovery,
            ITournamentHttpServer httpServer, Dispatcher uiDispatcher = null, LocalNetworkIdentity identity = null)
            : this(dataContext, discovery, httpServer, uiDispatcher, WindowsFirewallAccessProbe.Check, identity) { }

        internal NetworkServicesLifecycle(IDataContext dataContext, IPeerDiscoveryService discovery,
            ITournamentHttpServer httpServer, Dispatcher uiDispatcher, Func<int, int?, FirewallAccess> firewallAccess,
            LocalNetworkIdentity identity = null, Func<IReadOnlyList<LocalIpAddressProbe.LanAddress>> addresses = null, bool watchNetwork = true)
        {
            _dataContext = dataContext;
            _discovery = discovery;
            _httpServer = httpServer;
            _uiDispatcher = uiDispatcher;
            _firewallAccess = firewallAccess;
            _addresses = addresses ?? LocalIpAddressProbe.EnumerateLanInterfaces;
            _watchNetwork = watchNetwork;
            Identity = identity ?? new LocalNetworkIdentity(null);
            Identity.PropertyChanged += OnIdentityChanged;
            _dataContext.TournamentChanged += OnTournamentChanged;
            _discovery.DiagnosticMessage += OnDiscoveryDiagnostic;
            _httpServer.DiagnosticMessage += OnHttpDiagnostic;
            _discovery.PeerUpserted += OnPeerChanged;
            _discovery.PeerExpired += OnPeerChanged;
            if (_watchNetwork)
            {
                NetworkChange.NetworkAddressChanged += OnNetworkChanged;
                NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            }
            if (_dataContext.Tournament != null) OnTournamentChanged(this, _dataContext.Tournament);
        }

        public string StatusLabel
        {
            get
            {
                if (_dataContext.Tournament == null) return T("Network_LocalNoTournament", "Откройте турнир для обмена данными");
                if (!_dataContext.Tournament.ID.HasValue) return T("Network_LocalUnsaved", "Сохраните турнир, чтобы передавать его соседним компьютерам");
                if (!_discovery.ActualPort.HasValue) return T("Network_LocalDiscoveryUnavailable", "Автоматическая синхронизация недоступна");
                if (_localAddresses.Count == 0) return T("Network_LocalNoNetwork", "Нет подключения к локальной сети");
                if (string.IsNullOrEmpty(_dataContext.Tournament.FileName)) return T("Network_LocalUnsaved", "Сохраните турнир, чтобы передавать его соседним компьютерам");
                if (!_httpServer.ActualPort.HasValue) return T("Network_LocalPublicationUnavailable", "Передача данных недоступна; обнаружение компьютеров работает");
                if (!string.IsNullOrEmpty(_firewallNote)) return T("Network_LocalCheckFirewall", "Проверьте доступ через брандмауэр");
                return T("Network_LocalReady", "Готов к обмену данными");
            }
        }

        public string AddressesLabel => string.Join(" · ", _localAddresses.Select(a => a.NetworkName + ": " + a.Address));
        public string EndpointsLabel => string.Join(Environment.NewLine, _localAddresses.Select(a => HttpUrl(a.Address.ToString())).Where(s => !string.IsNullOrEmpty(s)));
        public string PortsLabel => "UDP: " + (_discovery.ActualPort?.ToString() ?? "—") + " · HTTP: " + (_httpServer.ActualPort?.ToString() ?? "—");
        public int PeerCount => _discovery.SnapshotPeers().Count;
        public string Warning
        {
            get
            {
                var duplicate = _discovery.SnapshotPeers().Any(p => string.Equals(p.NodeName, Identity.NodeName, StringComparison.OrdinalIgnoreCase));
                return string.Join(Environment.NewLine, new[] { _discoveryError, _httpError, _firewallNote, Identity.SaveError,
                    duplicate ? T("Network_DuplicateLocalName", "В сети есть компьютер с таким же именем. Измените имя здесь, чтобы различать компьютеры.") : null }
                    .Where(s => !string.IsNullOrEmpty(s)));
            }
        }

        private static string T(string key, string fallback)
        {
            var localized = LocalizationService.Instance?.T(key);
            return string.IsNullOrEmpty(localized) || localized == key ? fallback : localized;
        }

        private void RunUi(Action action)
        {
            if (_disposed) return;
            if (_uiDispatcher == null || _uiDispatcher.CheckAccess()) { if (!_disposed) action(); }
            else if (!_uiDispatcher.HasShutdownStarted) _uiDispatcher.BeginInvoke(new Action(() => { if (!_disposed) action(); }));
        }

        private void OnDiscoveryDiagnostic(object sender, string message) => RunUi(() =>
        {
            _discoveryError = message; NotifyState(); DiagnosticMessage?.Invoke(this, message);
        });
        private void OnHttpDiagnostic(object sender, string message) => RunUi(() =>
        {
            _httpError = message; NotifyState(); DiagnosticMessage?.Invoke(this, message);
        });
        private void OnPeerChanged(object sender, DiscoveredPeer peer) => RunUi(() =>
        {
            if (_checkedFirewall == FirewallAccess.Unknown && PeerCount > 0) _firewallNote = null;
            NotifyState();
        });
        private void OnIdentityChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LocalNetworkIdentity.NodeName)) RunUi(Restart);
            else RunUi(NotifyState);
        }

        private void OnTournamentChanged(object sender, Entities.Tournament tournament)
        {
            if (_subscribedTournament != null) _subscribedTournament.PropertyChanged -= OnTournamentPropertyChanged;
            _subscribedTournament = tournament;
            if (tournament != null) tournament.PropertyChanged += OnTournamentPropertyChanged;
            Restart();
        }

        private void OnTournamentPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Entities.Tournament.FileName) && e.PropertyName != nameof(Entities.Tournament.Name)) return;
            RunUi(() =>
            {
                var tournament = _dataContext.Tournament;
                if (!ReferenceEquals(sender, tournament)) return;
                if (tournament.ID == _startedTournamentId
                    && string.Equals(tournament.FileName, _startedFileName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(tournament.Name, _startedTitle, StringComparison.Ordinal)) return;
                Restart();
            });
        }

        // Reconnection is also available from the card when ports become free after startup.
        public void Restart()
        {
            if (_disposed) return;
            StopAll();
            _discoveryError = _httpError = _firewallNote = null;
            _checkedFirewall = null;
            _localAddresses = _addresses();
            var tournament = _dataContext.Tournament;
            _startedFileName = tournament?.FileName;
            _startedTitle = tournament?.Name;
            _startedTournamentId = tournament?.ID;
            if (tournament?.ID.HasValue == true)
            {
                if (!string.IsNullOrEmpty(tournament.FileName))
                {
                    _httpServer.SetServedTournament(tournament.ID.Value, tournament.FileName);
                    _httpServer.Start(NetworkDefaults.HttpPort);
                }
                // Each announcement replaces this host with the address of its sending interface.
                _discovery.StartForTournament(NetworkDefaults.DiscoveryPort, tournament.ID.Value,
                    tournament.Name ?? string.Empty, Identity.NodeName, HttpUrl("127.0.0.1"), ComputeStateHashOnUiThread);
                if (_discovery.ActualPort.HasValue) ArmFirewallHint(_discovery.ActualPort.Value, _httpServer.ActualPort);
            }
            NotifyState();
        }

        private string HttpUrl(string address)
        {
            var tournament = _dataContext.Tournament;
            return tournament?.ID.HasValue == true && _httpServer.ActualPort.HasValue && !string.IsNullOrEmpty(tournament.FileName)
                ? "http://" + address + ":" + _httpServer.ActualPort.Value + "/tournament/" + tournament.ID.Value + ".wrt" : string.Empty;
        }

        private void OnNetworkChanged(object sender, EventArgs e)
        {
            lock (_timerLock)
            {
                if (_disposed) return;
                _networkChangeTimer ??= new Timer(_ => RunUi(RefreshNetwork), null, Timeout.Infinite, Timeout.Infinite);
                _networkChangeTimer.Change(500, Timeout.Infinite);
            }
        }
        private void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e) => OnNetworkChanged(sender, e);

        internal void RefreshNetwork()
        {
            // Bound-to-any listeners survive adapter/IP changes; announcements enumerate fresh addresses.
            // Keep downloads alive, only restart services if a previous startup failed.
            if (_dataContext.Tournament != null && (!_discovery.ActualPort.HasValue ||
                (!string.IsNullOrEmpty(_dataContext.Tournament.FileName) && !_httpServer.ActualPort.HasValue))) Restart();
            else
            {
                Interlocked.Increment(ref _networkGeneration);
                _localAddresses = _addresses();
                _firewallNote = null;
                _checkedFirewall = null;
                if (_discovery.ActualPort.HasValue) ArmFirewallHint(_discovery.ActualPort.Value, _httpServer.ActualPort);
                NotifyState();
            }
        }

        private void NotifyState()
        {
            foreach (var name in new[] { nameof(StatusLabel), nameof(AddressesLabel), nameof(EndpointsLabel), nameof(PortsLabel), nameof(PeerCount), nameof(Warning) })
                OnPropertyChanged(name);
        }

        private string ComputeStateHashOnUiThread()
        {
            if (_dataContext.Tournament == null) return string.Empty;
            if (_uiDispatcher == null) return PeerStateHasher.Compute(_dataContext.Tournament);
            try { return _uiDispatcher.Invoke(() => PeerStateHasher.Compute(_dataContext.Tournament)); }
            catch (InvalidOperationException) when (_uiDispatcher.HasShutdownStarted) { return string.Empty; }
            catch (OperationCanceledException) when (_uiDispatcher.HasShutdownStarted) { return string.Empty; }
        }

        private void ArmFirewallHint(int discoveryPort, int? httpPort)
        {
            _firewallHintTimer?.Dispose();
            int generation = _networkGeneration;
            _firewallHintTimer = new Timer(_ => CheckNetworkStatus(discoveryPort, httpPort, generation), null, FirewallHintDelay, Timeout.InfiniteTimeSpan);
        }

        internal void CheckNetworkStatus(int discoveryPort, int? httpPort, int? generation = null)
        {
            if (_disposed || _dataContext.Tournament == null || (generation.HasValue && generation != _networkGeneration)) return;
            var access = _firewallAccess(discoveryPort, httpPort);
            if (_disposed || _dataContext.Tournament == null || (generation.HasValue && generation != _networkGeneration)) return;
            if (access == FirewallAccess.Allowed)
            {
                RunUi(() =>
                {
                    if (generation.HasValue && generation != _networkGeneration) return;
                    _checkedFirewall = access; _firewallNote = null; NotifyState();
                });
                return;
            }
            if (access == FirewallAccess.Unknown && PeerCount > 0) return;
            var message = access == FirewallAccess.NotAllowed
                ? T("Network_FirewallNotAllowed", "В брандмауэре Windows нет разрешения на входящие подключения для приложения или действует запрет. Разрешите приложение для текущей сети, чтобы компьютеры могли обмениваться данными турнира.")
                : T("Network_FirewallUnknown", "Не удалось проверить разрешения брандмауэра Windows. Если ожидаете соседей, проверьте сеть и разрешения приложения.");
            RunUi(() =>
            {
                if (generation.HasValue && generation != _networkGeneration) return;
                _checkedFirewall = access; _firewallNote = message; NotifyState(); DiagnosticMessage?.Invoke(this, message);
            });
        }

        private void StopAll()
        {
            Interlocked.Increment(ref _networkGeneration);
            _firewallHintTimer?.Dispose(); _firewallHintTimer = null;
            _discovery.Stop(); _httpServer.Stop();
        }

        public void Dispose()
        {
            _disposed = true;
            _dataContext.TournamentChanged -= OnTournamentChanged;
            Identity.PropertyChanged -= OnIdentityChanged;
            _discovery.DiagnosticMessage -= OnDiscoveryDiagnostic;
            _httpServer.DiagnosticMessage -= OnHttpDiagnostic;
            _discovery.PeerUpserted -= OnPeerChanged;
            _discovery.PeerExpired -= OnPeerChanged;
            if (_subscribedTournament != null) _subscribedTournament.PropertyChanged -= OnTournamentPropertyChanged;
            if (_watchNetwork)
            {
                NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
                NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            }
            lock (_timerLock) { _networkChangeTimer?.Dispose(); _networkChangeTimer = null; }
            StopAll();
        }
    }
}
