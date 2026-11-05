using System;
using System.Linq;
using System.Net;
using FluentAssertions;
using Wrestling.Providers.Network;
using Xunit;

namespace Wrestling.Providers.Tests;

public class MatchActivityAdvertisementTests
{
    [Fact]
    public void Active_and_released_match_round_trip_and_legacy_packet_is_distinguishable()
    {
        var match = new ActiveMatchIdentity(Guid.NewGuid(), 12, "2.3");
        var ad = new PeerAdvertisement { ActiveMatch = match, ActivityRevision = 42 };
        var restored = PeerAdvertisement.TryFromBytes(ad.ToBytes());
        restored.ActiveMatch.Should().Be(match);
        restored.ActivityRevision.Should().Be(42);
        ad.ActiveMatch = null;
        ad.ActivityRevision++;
        restored = PeerAdvertisement.TryFromBytes(ad.ToBytes());
        restored.ActiveMatch.Should().BeNull();
        restored.ActivityRevision.Should().Be(43);
        PeerAdvertisement.TryFromBytes(System.Text.Encoding.UTF8.GetBytes("{\"proto\":1}")).ActivityRevision.Should().BeNull();
    }

    [Fact]
    public void Out_of_order_announcement_cannot_reopen_released_match_but_still_refreshes_peer()
    {
        var registry = new PeerRegistry();
        var tournament = Guid.NewGuid();
        registry.SetContext(Guid.NewGuid(), tournament);
        var ad = new PeerAdvertisement { InstanceId = Guid.NewGuid(), TournamentId = tournament,
            ActiveMatch = new ActiveMatchIdentity(Guid.NewGuid(), 1, "1.1"), ActivityRevision = 1 };
        var now = DateTime.UtcNow;
        registry.Ingest(ad, IPAddress.Loopback, now);
        registry.Snapshot().Single().ActiveMatch.Should().Be(ad.ActiveMatch);
        var released = new PeerAdvertisement { InstanceId = ad.InstanceId, TournamentId = tournament, ActivityRevision = 2 };
        registry.Ingest(released, IPAddress.Loopback, now.AddSeconds(1));
        registry.Ingest(ad, IPAddress.Loopback, now.AddSeconds(2));
        var peer = registry.Snapshot().Single();
        peer.ActiveMatch.Should().BeNull();
        peer.ActivityRevision.Should().Be(2);
        peer.LastSeenUtc.Should().Be(now.AddSeconds(2));
    }
}
