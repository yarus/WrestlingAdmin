using System;
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Wrestling.Providers.Network;
using Xunit;

namespace Wrestling.Providers.Tests;

public class PeerDiscoveryServiceTests
{
    private static int FindFreeUdpPort()
    {
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint).Port;
    }

    [Fact]
    public void Fresh_service_has_no_peers()
    {
        using var service = new PeerDiscoveryService();

        service.SnapshotPeers().Should().BeEmpty();
    }

    [Fact]
    public void StartForTournament_opens_listener_and_Stop_cleans_up()
    {
        var port = FindFreeUdpPort();
        using var service = new PeerDiscoveryService();
        string diagnostic = null;
        service.DiagnosticMessage += (s, m) => diagnostic = m;

        service.StartForTournament(
            port: port,
            tournamentId: Guid.NewGuid(),
            tournamentTitle: "T",
            nodeName: "Ковёр 1",
            httpUrl: "http://127.0.0.1:1234/tournament/x.wrt");
        diagnostic.Should().BeNull("normal start should not raise a diagnostic");

        service.Stop();
        // After stop, a fresh UdpClient should be able to bind the same port —
        // proves we released the socket.
        using var after = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        after.Should().NotBeNull();
    }

    [Fact]
    public void Conflicting_port_raises_diagnostic_and_does_not_throw()
    {
        var port = FindFreeUdpPort();
        var blocker = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        blocker.ExclusiveAddressUse = true;
        blocker.Bind(new IPEndPoint(IPAddress.Any, port));

        try
        {
            using var service = new PeerDiscoveryService();
            string diagnostic = null;
            service.DiagnosticMessage += (s, m) => diagnostic = m;

            service.StartForTournament(port, Guid.NewGuid(), "T", "N", string.Empty);

            diagnostic.Should().NotBeNull().And.Contain(port.ToString());
        }
        finally
        {
            blocker.Close();
        }
    }

    [Fact]
    public void Double_Start_is_idempotent_reopens_listener_without_leaking()
    {
        var port = FindFreeUdpPort();
        using var service = new PeerDiscoveryService();

        service.StartForTournament(port, Guid.NewGuid(), "T", "N", string.Empty);
        // Second Start on the same service should stop the first listener
        // and open a new one — no port-in-use diagnostic.
        string diagnostic = null;
        service.DiagnosticMessage += (s, m) => diagnostic = m;
        service.StartForTournament(port, Guid.NewGuid(), "T2", "N2", string.Empty);

        diagnostic.Should().BeNull();
    }
}
