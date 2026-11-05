using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluentAssertions;
using Wrestling.Providers.Network;
using Xunit;

namespace Wrestling.Providers.Tests;

public class PeerDiscoveryServiceTests
{
    private sealed class Reservation : IDisposable
    {
        public int First { get; }
        public List<UdpClient> Sockets { get; } = new();
        public Reservation()
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                First = Random.Shared.Next(30000, 60000);
                try
                {
                    for (var i = 0; i < NetworkDefaults.DiscoveryPortCount; i++)
                    {
                        var socket = new UdpClient(AddressFamily.InterNetwork) { ExclusiveAddressUse = true };
                        Sockets.Add(socket);
                        socket.Client.Bind(new IPEndPoint(IPAddress.Any, First + i));
                    }
                    return;
                }
                catch (SocketException) { Dispose(); Sockets.Clear(); }
            }
            throw new InvalidOperationException("Could not reserve a UDP test range");
        }
        public void Dispose() { foreach (var socket in Sockets) socket.Dispose(); }
    }

    private static PeerDiscoveryService MakeService() => new(
        announceInterval: TimeSpan.FromMilliseconds(50),
        addresses: () => new[] { new LocalIpAddressProbe.LanAddress(IPAddress.Loopback, IPAddress.Loopback, "Test") });

    [Fact]
    public async Task Four_instances_publish_three_open_bouts_and_release_immediately_without_changing_hash()
    {
        using var range = new Reservation();
        for (int i = 0; i < 4; i++) range.Sockets[i].Dispose();
        PeerDiscoveryService Create() => new(announceInterval: TimeSpan.FromSeconds(30),
            addresses: () => new[] { new LocalIpAddressProbe.LanAddress(IPAddress.Loopback, IPAddress.Loopback, "Test") });
        using var first = Create(); using var second = Create(); using var third = Create(); using var fourth = Create();
        var tournament = Guid.NewGuid(); var group = Guid.NewGuid();
        var seen = new System.Collections.Concurrent.ConcurrentDictionary<string, ActiveMatchIdentity>();
        var allOpen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        first.PeerUpserted += (_, peer) =>
        {
            if (peer.ActiveMatch != null)
            {
                seen[peer.NodeName] = peer.ActiveMatch;
                if (seen.Count == 3) allOpen.TrySetResult();
            }
            if (peer.NodeName == "B" && peer.ActiveMatch == null && peer.ActivityRevision >= 3) released.TrySetResult();
        };
        first.StartForTournament(range.First, tournament, "T", "A", string.Empty, () => "same");
        var helpers = new[] { second, third, fourth };
        for (int i = 0; i < 3; i++)
        {
            helpers[i].StartForTournament(range.First, tournament, "T", ((char)('B' + i)).ToString(), string.Empty, () => "same");
            helpers[i].SetActiveMatch(new ActiveMatchIdentity(group, 1, "1." + (i + 1)));
        }
        await allOpen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        seen.Values.Select(m => m.BracketFullNumber).Should().BeEquivalentTo("1.1", "1.2", "1.3");
        second.SetActiveMatch(null);
        second.Stop(); // A graceful close must announce release before disposing its timers.
        await released.Task.WaitAsync(TimeSpan.FromSeconds(5));
        first.SnapshotPeers().Single(p => p.NodeName == "B").ActiveMatch.Should().BeNull();
        first.SnapshotPeers().Where(p => p.NodeName != "B").Should().OnlyContain(p => p.ActiveMatch != null);
        first.SnapshotPeers().Should().OnlyContain(p => p.StateHash == "same");
    }

    [Fact]
    public void Fresh_service_has_no_peers()
    {
        using var service = MakeService();
        service.SnapshotPeers().Should().BeEmpty();
        service.ActualPort.Should().BeNull();
    }

    [Fact]
    public void Start_Stop_and_repeated_start_release_and_reuse_the_same_port()
    {
        using var range = new Reservation();
        range.Sockets[0].Dispose();
        using var service = MakeService();
        string diagnostic = null;
        service.DiagnosticMessage += (_, text) => diagnostic = text;
        service.StartForTournament(range.First, Guid.NewGuid(), "T", "N", string.Empty);
        service.ActualPort.Should().Be(range.First);
        service.StartForTournament(range.First, Guid.NewGuid(), "T2", "N2", string.Empty);
        service.ActualPort.Should().Be(range.First);
        diagnostic.Should().BeNull();
        service.Stop();
        service.ActualPort.Should().BeNull();
        using var after = new UdpClient(AddressFamily.InterNetwork) { ExclusiveAddressUse = true };
        after.Client.Bind(new IPEndPoint(IPAddress.Any, range.First));
    }

    [Fact]
    public void Busy_first_port_falls_back_to_next_without_failure_diagnostic()
    {
        using var range = new Reservation();
        range.Sockets[1].Dispose();
        using var service = MakeService();
        string diagnostic = null;
        service.DiagnosticMessage += (_, text) => diagnostic = text;
        service.StartForTournament(range.First, Guid.NewGuid(), "T", "N", string.Empty);
        service.ActualPort.Should().Be(range.First + 1);
        diagnostic.Should().BeNull();
    }

    [Fact]
    public void Busy_entire_range_raises_clear_diagnostic_without_throwing_and_can_retry()
    {
        using var range = new Reservation();
        using var service = MakeService();
        string diagnostic = null;
        service.DiagnosticMessage += (_, text) => diagnostic = text;
        service.StartForTournament(range.First, Guid.NewGuid(), "T", "N", string.Empty);
        service.ActualPort.Should().BeNull();
        diagnostic.Should().NotBeNull().And.Contain(range.First.ToString()).And.Contain((range.First + 4).ToString());
        range.Sockets[4].Dispose();
        service.StartForTournament(range.First, Guid.NewGuid(), "T", "N", string.Empty);
        service.ActualPort.Should().Be(range.First + 4);
    }

    [Fact]
    public async Task Instances_using_different_ports_discover_each_other_and_announce_the_sending_interfaces_address()
    {
        using var range = new Reservation();
        range.Sockets[1].Dispose(); range.Sockets[2].Dispose();
        using var first = MakeService();
        using var second = MakeService();
        var tournament = Guid.NewGuid();
        var firstSeen = new TaskCompletionSource<DiscoveredPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSeen = new TaskCompletionSource<DiscoveredPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.PeerUpserted += (_, peer) => firstSeen.TrySetResult(peer);
        second.PeerUpserted += (_, peer) => secondSeen.TrySetResult(peer);
        first.StartForTournament(range.First, tournament, "T", "First", $"http://192.0.2.11:40001/tournament/{tournament}.wrt");
        second.StartForTournament(range.First, tournament, "T", "Second", $"http://192.0.2.12:40002/tournament/{tournament}.wrt");
        first.ActualPort.Should().Be(range.First + 1);
        second.ActualPort.Should().Be(range.First + 2);
        var found = await Task.WhenAll(firstSeen.Task, secondSeen.Task).WaitAsync(TimeSpan.FromSeconds(5));
        found[0].NodeName.Should().Be("Second");
        found[1].NodeName.Should().Be("First");
        found[0].HttpUrl.Should().Be($"http://127.0.0.1:40002/tournament/{tournament}.wrt");
        found[1].HttpUrl.Should().Be($"http://127.0.0.1:40001/tournament/{tournament}.wrt");
        first.SnapshotPeers().Should().ContainSingle();
        second.SnapshotPeers().Should().ContainSingle();
        found.Should().OnlyContain(peer => peer.ComputerName == Environment.MachineName);
    }
}
