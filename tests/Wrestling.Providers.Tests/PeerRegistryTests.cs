using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using Wrestling.Providers.Network;
using Xunit;

namespace Wrestling.Providers.Tests;

public class PeerRegistryTests
{
    private static readonly IPAddress AnySender = IPAddress.Parse("192.168.1.99");
    private static readonly DateTime T0 = new DateTime(2026, 4, 21, 12, 0, 0, DateTimeKind.Utc);

    private static PeerAdvertisement Ad(Guid instance, Guid tournament, string name = "Ковёр")
    {
        return new PeerAdvertisement
        {
            Proto = PeerAdvertisement.CurrentProto,
            InstanceId = instance,
            TournamentId = tournament,
            TournamentTitle = "T",
            NodeName = name,
            HttpUrl = "http://10.0.0.1:24566/tournament/" + tournament + ".wrt",
            AppVersion = "1.0",
            SentAt = T0
        };
    }

    [Fact]
    public void Ingesting_new_peer_fires_upsert_and_snapshot_contains_it()
    {
        var registry = new PeerRegistry();
        var self = Guid.NewGuid();
        var tournament = Guid.NewGuid();
        registry.SetContext(self, tournament);

        var upserted = new List<DiscoveredPeer>();
        registry.PeerUpserted += (s, p) => upserted.Add(p);

        var peerInstance = Guid.NewGuid();
        var reason = registry.Ingest(Ad(peerInstance, tournament), AnySender, T0);

        reason.Should().BeNull();
        upserted.Should().HaveCount(1);
        upserted[0].InstanceId.Should().Be(peerInstance);
        registry.Snapshot().Should().ContainSingle(p => p.InstanceId == peerInstance);
    }

    [Fact]
    public void Ingesting_self_is_dropped()
    {
        var registry = new PeerRegistry();
        var self = Guid.NewGuid();
        var tournament = Guid.NewGuid();
        registry.SetContext(self, tournament);

        var reason = registry.Ingest(Ad(self, tournament), AnySender, T0);

        reason.Should().Be("self");
        registry.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void Ingesting_foreign_tournament_is_dropped()
    {
        var registry = new PeerRegistry();
        registry.SetContext(Guid.NewGuid(), Guid.NewGuid());

        var reason = registry.Ingest(Ad(Guid.NewGuid(), Guid.NewGuid()), AnySender, T0);

        reason.Should().Be("tournament mismatch");
        registry.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void Ingesting_unknown_proto_is_dropped()
    {
        var registry = new PeerRegistry();
        var tournament = Guid.NewGuid();
        registry.SetContext(Guid.NewGuid(), tournament);
        var ad = Ad(Guid.NewGuid(), tournament);
        ad.Proto = 999;

        var reason = registry.Ingest(ad, AnySender, T0);

        reason.Should().StartWith("unknown proto");
    }

    [Fact]
    public void Re_announce_updates_LastSeen_without_duplicating_peer()
    {
        var registry = new PeerRegistry();
        var tournament = Guid.NewGuid();
        registry.SetContext(Guid.NewGuid(), tournament);
        var peer = Guid.NewGuid();

        registry.Ingest(Ad(peer, tournament), AnySender, T0);
        var firstSeen = registry.Snapshot().Single().LastSeenUtc;

        registry.Ingest(Ad(peer, tournament), AnySender, T0.AddSeconds(3));

        registry.Snapshot().Should().HaveCount(1);
        registry.Snapshot().Single().LastSeenUtc.Should().Be(T0.AddSeconds(3)).And.NotBe(firstSeen);
    }

    [Fact]
    public void Tick_expires_peers_past_the_timeout_window()
    {
        var registry = new PeerRegistry(TimeSpan.FromSeconds(6));
        var tournament = Guid.NewGuid();
        registry.SetContext(Guid.NewGuid(), tournament);

        var expired = new List<DiscoveredPeer>();
        registry.PeerExpired += (s, p) => expired.Add(p);

        var peer = Guid.NewGuid();
        registry.Ingest(Ad(peer, tournament), AnySender, T0);

        registry.Tick(T0.AddSeconds(5));
        expired.Should().BeEmpty("5 s < 6 s window — peer still alive");
        registry.Snapshot().Should().HaveCount(1);

        registry.Tick(T0.AddSeconds(7));
        expired.Should().HaveCount(1);
        registry.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void SetContext_clears_existing_peers_and_fires_expire_for_each()
    {
        var registry = new PeerRegistry();
        registry.SetContext(Guid.NewGuid(), Guid.NewGuid());
        var tournament = Guid.NewGuid();
        registry.SetContext(Guid.NewGuid(), tournament);
        registry.Ingest(Ad(Guid.NewGuid(), tournament), AnySender, T0);
        registry.Ingest(Ad(Guid.NewGuid(), tournament), AnySender, T0);
        registry.Snapshot().Should().HaveCount(2);

        var expired = new List<DiscoveredPeer>();
        registry.PeerExpired += (s, p) => expired.Add(p);

        registry.SetContext(Guid.NewGuid(), Guid.NewGuid());

        expired.Should().HaveCount(2);
        registry.Snapshot().Should().BeEmpty();
    }
}
