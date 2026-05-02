using System;
using System.Collections.Generic;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Model;
using Xunit;

namespace Wrestling.UI.Material.Tests;

// Verifies the glue class that ties DataContext.Tournament lifecycle events to
// the network services — opening a tournament should kick both services on,
// closing should stop them, changing NodeName mid-session should restart.
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

        public event EventHandler<DiscoveredPeer> PeerUpserted { add { } remove { } }
        public event EventHandler<DiscoveredPeer> PeerExpired { add { } remove { } }
        public event EventHandler<string> DiagnosticMessage { add { } remove { } }

        public IReadOnlyCollection<DiscoveredPeer> SnapshotPeers() => Array.Empty<DiscoveredPeer>();

        public void StartForTournament(int port, Guid tournamentId, string tournamentTitle, string nodeName, string httpUrl, string uncPath, Func<string> stateHashProvider = null)
        {
            StartCalls++;
            LastPort = port;
            LastTournamentId = tournamentId;
            LastNodeName = nodeName;
            LastHttpUrl = httpUrl;
        }

        public void Stop() => StopCalls++;
        public void Dispose() { }
    }

    private sealed class FakeHttpServer : ITournamentHttpServer
    {
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public int LastPort { get; private set; }
        public Guid LastServedId { get; private set; }
        public int? ActualPort { get; private set; }
        public event EventHandler<string> DiagnosticMessage { add { } remove { } }

        public void SetServedTournament(Guid tournamentId, string wrtPath) { LastServedId = tournamentId; }
        public void Start(int port) { StartCalls++; LastPort = port; ActualPort = port; }
        public void Stop() { StopCalls++; ActualPort = null; }
        public void Dispose() { }
    }

    private static Entities.Tournament MakeTournament(string nodeName = "Ковёр 1", bool httpOn = true, int discoveryPort = 30001, int httpPort = 30002)
    {
        var t = new Entities.Tournament(new GlobalSettings
        {
            NodeName = nodeName,
            IsHttpServerEnabled = httpOn,
            DiscoveryPort = discoveryPort,
            HttpServerPort = httpPort
        })
        {
            ID = Guid.NewGuid(),
            Name = "T",
            FileName = "tournament.wrt"
        };
        return t;
    }

    [Fact]
    public void Assigning_Tournament_starts_both_services_with_settings_ports()
    {
        var dc = new DataContext();
        var d = new FakeDiscovery();
        var h = new FakeHttpServer();
        using var _ = new NetworkServicesLifecycle(dc, d, h);

        dc.Tournament = MakeTournament(discoveryPort: 40001, httpPort: 40002);

        d.StartCalls.Should().Be(1);
        d.LastPort.Should().Be(40001);
        d.LastNodeName.Should().Be("Ковёр 1");
        h.StartCalls.Should().Be(1);
        h.LastPort.Should().Be(40002);
    }

    [Fact]
    public void Clearing_Tournament_stops_both_services()
    {
        var dc = new DataContext();
        var d = new FakeDiscovery();
        var h = new FakeHttpServer();
        using var _ = new NetworkServicesLifecycle(dc, d, h);
        dc.Tournament = MakeTournament();

        dc.Tournament = null;

        d.StopCalls.Should().BeGreaterThanOrEqualTo(1);
        h.StopCalls.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Changing_NodeName_mid_session_restarts_discovery_with_new_name()
    {
        var dc = new DataContext();
        var d = new FakeDiscovery();
        var h = new FakeHttpServer();
        using var _ = new NetworkServicesLifecycle(dc, d, h);
        var t = MakeTournament();
        dc.Tournament = t;
        d.StartCalls.Should().Be(1);

        t.Settings.NodeName = "Ковёр 42";

        d.StartCalls.Should().Be(2, "name change must re-announce so peers see the new label");
        d.LastNodeName.Should().Be("Ковёр 42");
    }

    [Fact]
    public void Empty_NodeName_prevents_discovery_start_but_still_runs_http()
    {
        var dc = new DataContext();
        var d = new FakeDiscovery();
        var h = new FakeHttpServer();
        using var _ = new NetworkServicesLifecycle(dc, d, h);

        dc.Tournament = MakeTournament(nodeName: string.Empty);

        d.StartCalls.Should().Be(0, "anonymous nodes are pointless on the network");
        h.StartCalls.Should().Be(1);
    }

}
