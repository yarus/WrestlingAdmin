using System;
using System.ComponentModel;
using System.Net;
using System.Threading;
using System.Windows.Threading;
using Wrestling.Entities;
using Wrestling.Providers.Network;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Model
{
    // Bridges DataContext.TournamentChanged to the peer-discovery and HTTP
    // services: start both when a tournament is opened, stop when closed,
    // restart when a relevant setting is edited during the session.
    //
    // Also surfaces service-level diagnostics (port conflicts, zero-peers
    // timeout) through a single DiagnosticMessage event so App.xaml.cs can
    // route them to the snackbar without the UI subscribing to each service.
    public sealed class NetworkServicesLifecycle : IDisposable
    {
        // Empty discovery is normal on a single computer. Check firewall
        // permissions separately before choosing the diagnostic message.
        private static readonly TimeSpan FirewallHintDelay = TimeSpan.FromSeconds(30);

        private readonly IDataContext _dataContext;
        private readonly IPeerDiscoveryService _discovery;
        private readonly ITournamentHttpServer _httpServer;
        private readonly Dispatcher _uiDispatcher;
        private readonly Func<int, int?, FirewallAccess> _firewallAccess;

        private GlobalSettings _subscribedSettings;
        private Timer _firewallHintTimer;
        private int _networkGeneration;

        public event EventHandler<string> DiagnosticMessage;

        public NetworkServicesLifecycle(IDataContext dataContext, IPeerDiscoveryService discovery, ITournamentHttpServer httpServer, Dispatcher uiDispatcher = null)
            : this(dataContext, discovery, httpServer, uiDispatcher, WindowsFirewallAccessProbe.Check)
        {
        }

        internal NetworkServicesLifecycle(IDataContext dataContext, IPeerDiscoveryService discovery,
            ITournamentHttpServer httpServer, Dispatcher uiDispatcher, Func<int, int?, FirewallAccess> firewallAccess)
        {
            _dataContext = dataContext;
            _discovery = discovery;
            _httpServer = httpServer;
            _uiDispatcher = uiDispatcher;
            _firewallAccess = firewallAccess;
            _dataContext.TournamentChanged += OnTournamentChanged;
            _discovery.DiagnosticMessage += Bubble;
            _httpServer.DiagnosticMessage += Bubble;
        }

        private void Bubble(object sender, string message) => DiagnosticMessage?.Invoke(this, message);

        private void OnTournamentChanged(object sender, Entities.Tournament tournament)
        {
            if (_subscribedSettings != null)
            {
                _subscribedSettings.PropertyChanged -= OnSettingsChanged;
                _subscribedSettings = null;
            }

            StopAll();

            if (tournament == null) return;

            _subscribedSettings = tournament.Settings;
            if (_subscribedSettings != null)
            {
                _subscribedSettings.PropertyChanged += OnSettingsChanged;
            }

            StartAll(tournament);
        }

        private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(GlobalSettings.NodeName):
                case nameof(GlobalSettings.DiscoveryPort):
                case nameof(GlobalSettings.IsHttpServerEnabled):
                case nameof(GlobalSettings.HttpServerPort):
                case nameof(GlobalSettings.AnnounceIpOverride):
                    var t = _dataContext.Tournament;
                    StopAll();
                    if (t != null) StartAll(t);
                    break;
            }
        }

        private void StartAll(Entities.Tournament tournament)
        {
            var settings = tournament.Settings;
            if (settings == null) return;
            if (!tournament.ID.HasValue) return;

            string httpUrl = null;
            if (settings.IsHttpServerEnabled && !string.IsNullOrEmpty(tournament.FileName))
            {
                _httpServer.SetServedTournament(tournament.ID.Value, tournament.FileName);
                _httpServer.Start(settings.HttpServerPort);
                if (_httpServer.ActualPort.HasValue)
                {
                    var ip = LocalIpAddressProbe.PickAnnounceAddress(settings.AnnounceIpOverride);
                    if (!IPAddress.IsLoopback(ip))
                    {
                        httpUrl = "http://" + ip + ":" + _httpServer.ActualPort.Value + "/tournament/" + tournament.ID.Value + ".wrt";
                    }
                }
            }

            // Discovery announces only when a human-readable name is set —
            // otherwise peers see a bunch of nameless nodes and can't tell who
            // is who.
            if (!string.IsNullOrEmpty(settings.NodeName))
            {
                _discovery.StartForTournament(
                    port: settings.DiscoveryPort,
                    tournamentId: tournament.ID.Value,
                    tournamentTitle: tournament.Name ?? string.Empty,
                    nodeName: settings.NodeName,
                    httpUrl: httpUrl ?? string.Empty,
                    stateHashProvider: ComputeStateHashOnUiThread);

                ArmFirewallHint(settings.DiscoveryPort, _httpServer.ActualPort);
            }
        }

        // Hash callback fires from PeerDiscoveryService.AnnounceSafely on a
        // threadpool Timer thread. PeerStateHasher.Compute walks
        // ObservableCollection<AgeWeightGroup> + ObservableCollection<Round> +
        // List<WrestlingMatch> — all of which the UI thread can be mutating
        // (bracket regen, group edit, match completion). Iterating without
        // marshaling throws InvalidOperationException intermittently. Bouncing
        // through the UI dispatcher serializes hash-compute against UI mutation.
        private string ComputeStateHashOnUiThread()
        {
            var t = _dataContext?.Tournament;
            if (t == null) return string.Empty;
            if (_uiDispatcher == null) return PeerStateHasher.Compute(t);
            try
            {
                return _uiDispatcher.Invoke(() => PeerStateHasher.Compute(_dataContext.Tournament));
            }
            catch
            {
                // Dispatcher shutdown / disposed — fall back to direct compute;
                // worst case the announce skips this tick.
                return string.Empty;
            }
        }

        private void ArmFirewallHint(int discoveryPort, int? httpPort)
        {
            _firewallHintTimer?.Dispose();
            int generation = _networkGeneration;
            _firewallHintTimer = new Timer(_ => CheckNetworkStatus(discoveryPort, httpPort, generation), null, FirewallHintDelay, Timeout.InfiniteTimeSpan);
        }

        internal void CheckNetworkStatus(int discoveryPort, int? httpPort, int? generation = null)
        {
            if (_subscribedSettings == null || (generation.HasValue && generation != _networkGeneration)) return;
            var access = _firewallAccess(discoveryPort, httpPort);
            if (_subscribedSettings == null || (generation.HasValue && generation != _networkGeneration)) return;
            if (access == FirewallAccess.Allowed) return;
            string key, fallback;
            if (access == FirewallAccess.NotAllowed)
            {
                key = "Network_FirewallNotAllowed";
                fallback = "В брандмауэре Windows нет разрешения на входящие подключения для приложения или действует запрет. Разрешите приложение для текущей сети, чтобы компьютеры могли обмениваться данными турнира.";
            }
            else
            {
                if (_discovery.SnapshotPeers().Count > 0) return;
                key = "Network_FirewallUnknown";
                fallback = "Не удалось проверить разрешения брандмауэра Windows. Если ожидаете соседей, проверьте сеть и разрешения приложения.";
            }
            var localized = LocalizationService.Instance?.T(key);
            DiagnosticMessage?.Invoke(this, string.IsNullOrEmpty(localized) || localized == key ? fallback : localized);
        }

        private void StopAll()
        {
            Interlocked.Increment(ref _networkGeneration);
            _firewallHintTimer?.Dispose();
            _firewallHintTimer = null;
            _discovery.Stop();
            _httpServer.Stop();
        }

        public void Dispose()
        {
            _dataContext.TournamentChanged -= OnTournamentChanged;
            _discovery.DiagnosticMessage -= Bubble;
            _httpServer.DiagnosticMessage -= Bubble;
            if (_subscribedSettings != null)
            {
                _subscribedSettings.PropertyChanged -= OnSettingsChanged;
                _subscribedSettings = null;
            }
            StopAll();
        }
    }
}
