using System;
using System.Collections.Generic;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Xunit;

namespace Wrestling.UI.Material.Tests;

// PeerSyncStatusTracker is the read model for the Dashboard "Синхронизация"
// Card. The behavior under test:
//   - New peer announce → entry added with status derived from hash compare
//     against local tournament.
//   - PeerExpired → entry transitions to "не в сети" but stays in the list
//     (5-minute session cache).
//   - After 5 minutes of "не в сети" → entry removed.
//   - Tournament changed → list cleared.
//
// Tests bypass the real DispatcherTimer by passing null dispatcher and
// invoking the internal Refresh() directly.
public sealed class PeerSyncStatusTrackerTests
{
    private sealed class TestDiscovery : IPeerDiscoveryService
    {
        public event EventHandler<DiscoveredPeer> PeerUpserted;
        public event EventHandler<DiscoveredPeer> PeerExpired;
        public event EventHandler<string> DiagnosticMessage { add { } remove { } }

        public IReadOnlyCollection<DiscoveredPeer> SnapshotPeers() => Array.Empty<DiscoveredPeer>();
        public void StartForTournament(int port, Guid tournamentId, string tournamentTitle, string nodeName, string httpUrl, Func<string> stateHashProvider = null) { }
        public void Stop() { }
        public void Dispose() { }

        public void RaiseUpserted(DiscoveredPeer p) => PeerUpserted?.Invoke(this, p);
        public void RaiseExpired(DiscoveredPeer p) => PeerExpired?.Invoke(this, p);
    }

    private sealed class FuncClock { public DateTime Now { get; set; } = new DateTime(2026, 5, 3, 12, 0, 0, DateTimeKind.Utc); }

    private static DiscoveredPeer MakePeer(Guid instance, string nodeName, string hash, DateTime lastSeen)
    {
        var peer = (DiscoveredPeer)Activator.CreateInstance(
            typeof(DiscoveredPeer),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            new object[] { instance, Guid.NewGuid() },
            null);
        typeof(DiscoveredPeer).GetProperty("StateHash").SetValue(peer, hash);
        typeof(DiscoveredPeer).GetProperty("NodeName").SetValue(peer, nodeName);
        typeof(DiscoveredPeer).GetProperty("LastSeenUtc").SetValue(peer, lastSeen);
        return peer;
    }

    private static (PeerSyncStatusTracker tr, TestDiscovery disc, DataContext dc, FuncClock clock) Build(Entities.Tournament tournament = null)
    {
        var dc = new DataContext { Tournament = tournament ?? new Entities.Tournament(new GlobalSettings()) { Name = "T" } };
        var disc = new TestDiscovery();
        var clock = new FuncClock();
        var tr = new PeerSyncStatusTracker(disc, dc, uiDispatcher: null, clock: () => clock.Now);
        return (tr, disc, dc, clock);
    }

    [Fact]
    public void New_peer_with_matching_hash_is_synced()
    {
        var (tr, disc, dc, clock) = Build();
        var localHash = PeerStateHasher.Compute(dc.Tournament);

        disc.RaiseUpserted(MakePeer(Guid.NewGuid(), "Ковёр 1", localHash, clock.Now));

        tr.Peers.Should().HaveCount(1);
        var p = tr.Peers[0];
        p.NodeName.Should().Be("Ковёр 1");
        p.StatusGlyph.Should().Be("✅");
        p.StatusText.Should().Be("синхронизирован");
    }

    [Fact]
    public void New_peer_with_different_hash_is_lagging()
    {
        var (tr, disc, _, clock) = Build();

        disc.RaiseUpserted(MakePeer(Guid.NewGuid(), "Ковёр 2", "differenthashAB", clock.Now));

        tr.Peers.Should().HaveCount(1);
        tr.Peers[0].StatusGlyph.Should().Be("⏳");
        tr.Peers[0].StatusText.Should().Be("догоняет");
    }

    [Fact]
    public void Empty_peer_hash_yields_неизвестно_status()
    {
        var (tr, disc, _, clock) = Build();

        disc.RaiseUpserted(MakePeer(Guid.NewGuid(), "Ковёр 3", string.Empty, clock.Now));

        tr.Peers.Should().HaveCount(1);
        tr.Peers[0].StatusGlyph.Should().Be("⏳");
        tr.Peers[0].StatusText.Should().Contain("неизвестно");
    }

    [Fact]
    public void Empty_NodeName_falls_back_to_placeholder()
    {
        var (tr, disc, _, clock) = Build();

        disc.RaiseUpserted(MakePeer(Guid.NewGuid(), string.Empty, "h", clock.Now));

        tr.Peers[0].NodeName.Should().Be("(без имени)");
    }

    [Fact]
    public void Re_announce_updates_existing_entry_in_place()
    {
        var (tr, disc, dc, clock) = Build();
        var instanceId = Guid.NewGuid();

        disc.RaiseUpserted(MakePeer(instanceId, "Ковёр 1", "h1", clock.Now));
        var localHash = PeerStateHasher.Compute(dc.Tournament);
        disc.RaiseUpserted(MakePeer(instanceId, "Ковёр 1", localHash, clock.Now.AddSeconds(2)));

        tr.Peers.Should().HaveCount(1, "second announce updates the same entry");
        tr.Peers[0].StatusGlyph.Should().Be("✅", "after second announce, hashes match → synced");
    }

    [Fact]
    public void PeerExpired_keeps_entry_visible_with_offline_status()
    {
        // 5-minute session cache: an operator-noticeable warning instead of
        // silent disappearance.
        var (tr, disc, _, clock) = Build();
        var instanceId = Guid.NewGuid();
        var peer = MakePeer(instanceId, "Ковёр 1", "h1", clock.Now);
        disc.RaiseUpserted(peer);

        disc.RaiseExpired(peer);
        tr.Refresh();

        tr.Peers.Should().HaveCount(1, "still in session-cache");
        tr.Peers[0].StatusGlyph.Should().Be("⚠");
        tr.Peers[0].StatusText.Should().Be("не в сети");
    }

    [Fact]
    public void Disconnected_entry_is_purged_after_session_cache_expires()
    {
        var (tr, disc, _, clock) = Build();
        var instanceId = Guid.NewGuid();
        var peer = MakePeer(instanceId, "Ковёр 1", "h1", clock.Now);

        disc.RaiseUpserted(peer);
        disc.RaiseExpired(peer);
        tr.Refresh();
        tr.Peers.Should().HaveCount(1);

        // Advance past the 5-minute cache window.
        clock.Now = clock.Now.Add(PeerSyncStatusTracker.SessionCacheRetention).AddSeconds(1);
        tr.Refresh();

        tr.Peers.Should().BeEmpty("session cache window elapsed; tracker forgets the peer");
    }

    [Fact]
    public void Reappearance_after_expire_revives_status()
    {
        var (tr, disc, _, clock) = Build();
        var instanceId = Guid.NewGuid();
        var peer = MakePeer(instanceId, "Ковёр 1", "h1", clock.Now);

        disc.RaiseUpserted(peer);
        disc.RaiseExpired(peer);
        tr.Refresh();
        tr.Peers[0].StatusGlyph.Should().Be("⚠");

        // Peer comes back inside the session-cache window — same instance,
        // marked live again.
        clock.Now = clock.Now.AddSeconds(30);
        disc.RaiseUpserted(MakePeer(instanceId, "Ковёр 1", "h1", clock.Now));

        tr.Peers.Should().HaveCount(1);
        tr.Peers[0].StatusGlyph.Should().NotBe("⚠");
    }

    [Fact]
    public void Tournament_change_clears_peer_list()
    {
        var (tr, disc, dc, clock) = Build();
        disc.RaiseUpserted(MakePeer(Guid.NewGuid(), "Ковёр 1", "h", clock.Now));
        disc.RaiseUpserted(MakePeer(Guid.NewGuid(), "Ковёр 2", "h", clock.Now));
        tr.Peers.Should().HaveCount(2);

        dc.Tournament = new Entities.Tournament(new GlobalSettings()) { Name = "Other" };

        tr.Peers.Should().BeEmpty();
    }

    [Fact]
    public void Multiple_peers_with_distinct_instance_ids_stay_separate()
    {
        var (tr, disc, _, clock) = Build();
        disc.RaiseUpserted(MakePeer(Guid.NewGuid(), "Ковёр 1", "h", clock.Now));
        disc.RaiseUpserted(MakePeer(Guid.NewGuid(), "Ковёр 2", "h", clock.Now));
        disc.RaiseUpserted(MakePeer(Guid.NewGuid(), "Ковёр 3", "h", clock.Now));

        tr.Peers.Should().HaveCount(3);
    }
}
