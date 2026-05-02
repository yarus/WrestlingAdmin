using System;
using System.ComponentModel;
using System.Net;
using System.Threading;
using Wrestling.Entities;
using Wrestling.Providers.Network;

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

        private GlobalSettings _subscribedSettings;
        private Timer _firewallHintTimer;

        public event EventHandler<string> DiagnosticMessage;

        public NetworkServicesLifecycle(IDataContext dataContext, IPeerDiscoveryService discovery, ITournamentHttpServer httpServer)
        {
            _dataContext = dataContext;
            _discovery = discovery;
            _httpServer = httpServer;
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
                case nameof(GlobalSettings.SelfUncPath):
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
            // is who. HTTP server may still be serving (see above) so operator
            // can manually point peers at this node via UNC.
            if (!string.IsNullOrEmpty(settings.NodeName))
            {
                _discovery.StartForTournament(
                    port: settings.DiscoveryPort,
                    tournamentId: tournament.ID.Value,
                    tournamentTitle: tournament.Name ?? string.Empty,
                    nodeName: settings.NodeName,
                    httpUrl: httpUrl ?? string.Empty,
                    uncPath: settings.SelfUncPath ?? string.Empty,
                    stateHashProvider: () => Wrestling.Providers.Network.PeerStateHasher.Compute(_dataContext.Tournament));

                ArmFirewallHint();
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
                DiagnosticMessage?.Invoke(this,
                    "Сеть не видит соседей — проверьте, что на первом запуске разрешили приложение в фаерволе Windows.");
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
