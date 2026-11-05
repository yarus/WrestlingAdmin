using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Model;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public sealed class NetworkServicesLifecycleTests
{
    private sealed class FakeDiscovery : IPeerDiscoveryService
    {
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public int LastPort { get; private set; }
        public string LastNodeName { get; private set; }
        public Guid LastTournamentId { get; private set; }
        public string LastHttpUrl { get; private set; }
        public int? ActualPort { get; private set; }
        public int SelectedPort { get; set; } = 24568;
        public bool Fail { get; set; }
        public IReadOnlyCollection<DiscoveredPeer> Peers { get; set; } = Array.Empty<DiscoveredPeer>();
        public event EventHandler<DiscoveredPeer> PeerUpserted;
        public event EventHandler<DiscoveredPeer> PeerExpired { add { } remove { } }
        public event EventHandler<string> DiagnosticMessage;
        public IReadOnlyCollection<DiscoveredPeer> SnapshotPeers() => Peers;
        public void StartForTournament(int port, Guid tournamentId, string tournamentTitle, string nodeName, string httpUrl, Func<string> stateHashProvider = null)
        {
            StartCalls++; LastPort = port; LastTournamentId = tournamentId; LastNodeName = nodeName; LastHttpUrl = httpUrl;
            ActualPort = Fail ? null : SelectedPort;
            if (Fail) DiagnosticMessage?.Invoke(this, "UDP 24565–24569 заняты");
        }
        public void PublishPeers(IReadOnlyCollection<DiscoveredPeer> peers) { Peers = peers; PeerUpserted?.Invoke(this, peers.First()); }
        public void Stop() { StopCalls++; ActualPort = null; }
        public void SetActiveMatch(ActiveMatchIdentity match) { }
        public void Dispose() { }
    }

    private sealed class FakeHttpServer : ITournamentHttpServer
    {
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public int LastPort { get; private set; }
        public Guid LastServedId { get; private set; }
        public string LastServedPath { get; private set; }
        public int? ActualPort { get; private set; }
        public int SelectedPort { get; set; } = 45000;
        public bool Fail { get; set; }
        public event EventHandler<string> DiagnosticMessage;
        public void SetServedTournament(Guid tournamentId, string path) { LastServedId = tournamentId; LastServedPath = path; }
        public void Start(int port)
        {
            StartCalls++; LastPort = port; ActualPort = Fail ? null : SelectedPort;
            if (Fail) DiagnosticMessage?.Invoke(this, "HTTP unavailable");
        }
        public void Stop() { StopCalls++; ActualPort = null; }
        public void Dispose() { }
    }

    private static Entities.Tournament MakeTournament() => new(new GlobalSettings())
    {
        ID = Guid.NewGuid(), Name = "T", FileName = "tournament.wrt"
    };

    private static NetworkServicesLifecycle Create(DataContext context, FakeDiscovery discovery, FakeHttpServer http,
        LocalNetworkIdentity identity = null, Func<int, int?, FirewallAccess> firewall = null,
        Func<IReadOnlyList<LocalIpAddressProbe.LanAddress>> addresses = null) =>
        new(context, discovery, http, null, firewall ?? ((_, _) => FirewallAccess.Allowed), identity,
            addresses ?? (() => new[] { new LocalIpAddressProbe.LanAddress(IPAddress.Parse("192.168.10.2"), IPAddress.Parse("192.168.10.255"), "Wi-Fi") }), watchNetwork: false);

    private static IReadOnlyCollection<DiscoveredPeer> MakePeers(string name = "Other")
    {
        var registry = new PeerRegistry(); var tournament = Guid.NewGuid();
        registry.SetContext(Guid.NewGuid(), tournament);
        registry.Ingest(new PeerAdvertisement { InstanceId = Guid.NewGuid(), TournamentId = tournament, NodeName = name }, IPAddress.Loopback, DateTime.UtcNow);
        return registry.Snapshot();
    }

    [Fact]
    public void Tournament_uses_local_name_and_automatic_services_with_actual_endpoints()
    {
        var dc = new DataContext(); var d = new FakeDiscovery(); var h = new FakeHttpServer();
        var identity = new LocalNetworkIdentity(null) { NodeName = "Ковёр B" };
        using var lifecycle = Create(dc, d, h, identity);
        dc.Tournament = MakeTournament();
        d.StartCalls.Should().Be(1); h.StartCalls.Should().Be(1);
        d.LastPort.Should().Be(NetworkDefaults.DiscoveryPort); h.LastPort.Should().Be(NetworkDefaults.HttpPort);
        d.LastNodeName.Should().Be("Ковёр B"); h.LastServedId.Should().Be(dc.Tournament.ID.Value);
        d.LastHttpUrl.Should().Contain(":45000/");
        lifecycle.EndpointsLabel.Should().Contain("http://192.168.10.2:45000/tournament/");
        lifecycle.PortsLabel.Should().Contain("24568").And.Contain("45000");
    }

    [Fact]
    public void Closing_tournament_stops_services_and_shows_closed_state()
    {
        var dc = new DataContext(); var d = new FakeDiscovery(); var h = new FakeHttpServer();
        using var lifecycle = Create(dc, d, h);
        dc.Tournament = MakeTournament(); dc.Tournament = null;
        d.ActualPort.Should().BeNull(); h.ActualPort.Should().BeNull();
        lifecycle.EndpointsLabel.Should().BeEmpty();
    }

    [Fact]
    public void Local_rename_survives_replacing_tournament_and_blank_name_uses_windows_name()
    {
        var dc = new DataContext(); var d = new FakeDiscovery(); var h = new FakeHttpServer();
        using var lifecycle = Create(dc, d, h);
        dc.Tournament = MakeTournament();
        lifecycle.Identity.NodeName = "Ковёр 42";
        d.LastNodeName.Should().Be("Ковёр 42");
        dc.Tournament = MakeTournament();
        d.LastNodeName.Should().Be("Ковёр 42");
        lifecycle.Identity.NodeName = "  ";
        d.LastNodeName.Should().Be(Environment.MachineName);
        d.ActualPort.Should().NotBeNull();
    }

    [Fact]
    public void First_save_starts_publication_and_save_as_updates_served_file()
    {
        var dc = new DataContext(); var d = new FakeDiscovery(); var h = new FakeHttpServer();
        using var lifecycle = Create(dc, d, h);
        var tournament = MakeTournament(); tournament.FileName = null; dc.Tournament = tournament;
        h.StartCalls.Should().Be(0); d.LastHttpUrl.Should().BeEmpty();
        tournament.FileName = "saved.wrt";
        h.LastServedPath.Should().Be("saved.wrt"); d.LastHttpUrl.Should().NotBeEmpty();
        tournament.FileName = "new-name.wrt";
        h.LastServedPath.Should().Be("new-name.wrt");
    }

    [Fact]
    public void Address_change_refreshes_live_urls_without_interrupting_downloads()
    {
        var dc = new DataContext(); var d = new FakeDiscovery(); var h = new FakeHttpServer();
        var address = IPAddress.Parse("192.168.10.2");
        using var lifecycle = Create(dc, d, h, addresses: () => new[] { new LocalIpAddressProbe.LanAddress(address, IPAddress.Broadcast, "Wi-Fi") });
        dc.Tournament = MakeTournament();
        address = IPAddress.Parse("10.0.0.15"); lifecycle.RefreshNetwork();
        lifecycle.EndpointsLabel.Should().Contain("10.0.0.15:45000").And.NotContain("192.168.10.2");
        h.StartCalls.Should().Be(1); d.StartCalls.Should().Be(1);
    }

    [Fact]
    public void Ordinary_autosave_notifications_do_not_rebind_network_ports()
    {
        var dc = new DataContext(); var d = new FakeDiscovery(); var h = new FakeHttpServer();
        using var lifecycle = Create(dc, d, h);
        dc.Tournament = MakeTournament();
        for (var i = 0; i < 5; i++)
        {
            dc.Tournament.FileName = "tournament.wrt";
            dc.Tournament.Name = "T";
        }
        h.StartCalls.Should().Be(1); d.StartCalls.Should().Be(1);
    }

    [Fact]
    public void Save_assigning_first_identity_starts_network_even_if_the_path_was_already_set()
    {
        var dc = new DataContext(); var d = new FakeDiscovery(); var h = new FakeHttpServer();
        using var lifecycle = Create(dc, d, h);
        dc.Tournament = MakeTournament();
        dc.Tournament = new Entities.Tournament(new GlobalSettings()) { FileName = "already-set.wrt" };
        var starts = d.StartCalls;
        dc.Tournament.ID = Guid.NewGuid();
        dc.Tournament.FileName = "already-set.wrt";
        d.StartCalls.Should().Be(starts + 1);
        h.LastServedId.Should().Be(dc.Tournament.ID.Value);
    }

    [Fact]
    public void Exhausted_discovery_ports_keep_visible_error_until_retry_succeeds()
    {
        var dc = new DataContext(); var d = new FakeDiscovery { Fail = true }; var h = new FakeHttpServer();
        using var lifecycle = Create(dc, d, h);
        dc.Tournament = MakeTournament();
        lifecycle.Warning.Should().Contain("24565–24569");
        lifecycle.StatusLabel.Should().Contain("недоступна");
        d.Fail = false; lifecycle.Restart();
        lifecycle.Warning.Should().BeEmpty(); d.ActualPort.Should().NotBeNull();
    }

    [Fact]
    public void Failed_http_does_not_announce_a_working_url_and_exposes_persistent_error()
    {
        var dc = new DataContext(); var d = new FakeDiscovery(); var h = new FakeHttpServer { Fail = true };
        using var lifecycle = Create(dc, d, h);
        dc.Tournament = MakeTournament();
        d.LastHttpUrl.Should().BeEmpty(); lifecycle.EndpointsLabel.Should().BeEmpty();
        lifecycle.Warning.Should().Contain("HTTP unavailable");
    }

    [Fact]
    public void Duplicate_local_name_is_visible_without_merging_computers()
    {
        var dc = new DataContext(); var d = new FakeDiscovery(); var h = new FakeHttpServer();
        using var lifecycle = Create(dc, d, h);
        dc.Tournament = MakeTournament(); d.PublishPeers(MakePeers(lifecycle.Identity.NodeName));
        lifecycle.Warning.Should().Contain("таким же именем"); lifecycle.PeerCount.Should().Be(1);
        lifecycle.Identity.NodeName = "Unique name";
        lifecycle.Warning.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "нет разрешения")]
    [InlineData(2, "Не удалось проверить")]
    public void Empty_discovery_does_not_determine_firewall_permissions(int access, string expected)
    {
        var dc = new DataContext(); int checkedUdp = 0; int? checkedHttp = null;
        using var lifecycle = Create(dc, new FakeDiscovery(), new FakeHttpServer(), firewall:
            (udp, http) => { checkedUdp = udp; checkedHttp = http; return (FirewallAccess)access; });
        dc.Tournament = MakeTournament(); string message = null;
        lifecycle.DiagnosticMessage += (_, text) => message = text;
        lifecycle.CheckNetworkStatus(24568, 45000);
        if (expected == null) message.Should().BeNull(); else message.Should().Contain(expected);
        checkedUdp.Should().Be(24568); checkedHttp.Should().Be(45000);
        if (expected != null) lifecycle.Warning.Should().Contain(expected);
    }

    [Fact]
    public void Existing_peers_do_not_hide_missing_http_permission()
    {
        var dc = new DataContext(); var discovery = new FakeDiscovery { Peers = MakePeers() };
        using var lifecycle = Create(dc, discovery, new FakeHttpServer(), firewall: (_, _) => FirewallAccess.NotAllowed);
        dc.Tournament = MakeTournament(); lifecycle.CheckNetworkStatus(24568, 45000);
        lifecycle.Warning.Should().Contain("нет разрешения");
    }

    [Fact]
    public void Existing_peers_and_allowed_firewall_need_no_warning()
    {
        var dc = new DataContext(); var discovery = new FakeDiscovery { Peers = MakePeers() };
        using var lifecycle = Create(dc, discovery, new FakeHttpServer());
        dc.Tournament = MakeTournament(); lifecycle.CheckNetworkStatus(24568, 45000);
        lifecycle.Warning.Should().BeEmpty();
    }

    [Fact]
    public void Closing_tournament_discards_pending_diagnostic()
    {
        var dc = new DataContext();
        using var lifecycle = Create(dc, new FakeDiscovery(), new FakeHttpServer(), firewall:
            (_, _) => { dc.Tournament = null; return FirewallAccess.NotAllowed; });
        dc.Tournament = MakeTournament(); string message = null;
        lifecycle.DiagnosticMessage += (_, text) => message = text;
        lifecycle.CheckNetworkStatus(24568, 45000); message.Should().BeNull();
    }

    [Fact]
    public void Successful_firewall_recheck_clears_previous_persistent_warning()
    {
        var dc = new DataContext(); var access = FirewallAccess.NotAllowed;
        using var lifecycle = Create(dc, new FakeDiscovery(), new FakeHttpServer(), firewall: (_, _) => access);
        dc.Tournament = MakeTournament(); lifecycle.CheckNetworkStatus(24568, 45000);
        lifecycle.Warning.Should().NotBeEmpty();
        access = FirewallAccess.Allowed; lifecycle.CheckNetworkStatus(24568, 45000);
        lifecycle.Warning.Should().BeEmpty();
    }

    [Fact]
    public void Network_change_discards_firewall_result_from_previous_network()
    {
        var dc = new DataContext(); NetworkServicesLifecycle lifecycle = null;
        using (lifecycle = Create(dc, new FakeDiscovery(), new FakeHttpServer(), firewall: (_, _) =>
        {
            lifecycle.RefreshNetwork();
            return FirewallAccess.NotAllowed;
        }))
        {
            dc.Tournament = MakeTournament();
            var generation = (int)typeof(NetworkServicesLifecycle).GetField("_networkGeneration",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(lifecycle);
            lifecycle.CheckNetworkStatus(24568, 45000, generation);
            lifecycle.Warning.Should().BeEmpty();
        }
    }
}
