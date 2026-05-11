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
        // If a tournament has been open for this long with discovery enabled
        // and we've heard nothing back, the most likely cause is a firewall
        // or VPN interfering with the UDP broadcast. Surface a hint so
        // operators don't waste minutes debugging silence.
        private static readonly TimeSpan FirewallHintDelay = TimeSpan.FromSeconds(30);

        private readonly IDataContext _dataContext;
        private readonly IPeerDiscoveryService _discovery;
        private readonly ITournamentHttpServer _httpServer;
        private readonly Dispatcher _uiDispatcher;

        private GlobalSettings _subscribedSettings;
        private Timer _firewallHintTimer;

        public event EventHandler<string> DiagnosticMessage;

        public NetworkServicesLifecycle(IDataContext dataContext, IPeerDiscoveryService discovery, ITournamentHttpServer httpServer, Dispatcher uiDispatcher = null)
        {
            _dataContext = dataContext;
            _discovery = discovery;
            _httpServer = httpServer;
            _uiDispatcher = uiDispatcher;
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

                ArmFirewallHint();
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

        private void ArmFirewallHint()
        {
            _firewallHintTimer?.Dispose();
            _firewallHintTimer = new Timer(_ => CheckFirewallHint(), null, FirewallHintDelay, Timeout.InfiniteTimeSpan);
        }

        private void CheckFirewallHint()
        {
            if (_discovery.SnapshotPeers().Count == 0)
            {
                var v = LocalizationService.Instance?.T("Network_FirewallHint");
                var msg = string.IsNullOrEmpty(v) || v == "Network_FirewallHint"
                    ? "Сеть не видит соседей — проверьте, что на первом запуске разрешили приложение в фаерволе Windows."
                    : v;
                DiagnosticMessage?.Invoke(this, msg);
            }
        }

        private void StopAll()
        {
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
