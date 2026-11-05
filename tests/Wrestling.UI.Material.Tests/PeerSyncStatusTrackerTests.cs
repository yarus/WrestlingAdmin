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
        public int? ActualPort => null;
        public event EventHandler<DiscoveredPeer> PeerUpserted;
        public event EventHandler<DiscoveredPeer> PeerExpired;
        public event EventHandler<string> DiagnosticMessage { add { } remove { } }

        public IReadOnlyCollection<DiscoveredPeer> SnapshotPeers() => Array.Empty<DiscoveredPeer>();
        public void StartForTournament(int port, Guid tournamentId, string tournamentTitle, string nodeName, string httpUrl, Func<string> stateHashProvider = null) { }
        public void Stop() { }
        public void SetActiveMatch(ActiveMatchIdentity match) { }
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
    public void Transfer_error_and_conflict_details_are_visible_instead_of_catching_up()
    {
        var dc = new DataContext { Tournament = new Entities.Tournament(new GlobalSettings()) };
        var discovery = new TestDiscovery();
        var diagnostics = new PeerSyncDiagnostics();
        using var tracker = new PeerSyncStatusTracker(discovery, dc, null, diagnostics: diagnostics);
        var peer = MakePeer(Guid.NewGuid(), "Ковер C", "different", DateTime.UtcNow);
        discovery.RaiseUpserted(peer);
        diagnostics.Report(peer.InstanceId, "http://10.1.30.49:24566", PeerSyncPhase.Conflict, DateTime.UtcNow,
            conflicts: new[] { new ImportConflict(Guid.NewGuid(), "2016–2017 / 32 кг", 31, "2.3", ImportConflictReason.CompletedDependentMatch) });

        tracker.Peers[0].StatusText.Should().Be("конфликт результатов");
        tracker.Peers[0].Details.Should().Contain("№31").And.Contain("Следующая встреча уже завершена");
        diagnostics.Report(peer.InstanceId, peer.HttpUrl, PeerSyncPhase.SaveFailed, DateTime.UtcNow);
        tracker.Peers[0].StatusText.Should().Be("изменения не сохранены");
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
    public void Renaming_a_running_instance_updates_its_existing_row()
    {
        var (tr, disc, _, clock) = Build();
        var instance = Guid.NewGuid();
        disc.RaiseUpserted(MakePeer(instance, "Старое имя", "h", clock.Now));
        var row = tr.Peers[0];

        disc.RaiseUpserted(MakePeer(instance, "Новое имя", "h", clock.Now.AddSeconds(5)));

        tr.Peers.Should().ContainSingle().Which.Should().BeSameAs(row);
        row.NodeName.Should().Be("Новое имя");
        row.IsLive.Should().BeTrue();
    }

    [Fact]
    public void Delayed_expiry_of_an_old_connection_does_not_disconnect_a_rediscovered_instance()
    {
        var (tr, disc, _, clock) = Build();
        var instance = Guid.NewGuid();
        var old = MakePeer(instance, "Старое имя", "h", clock.Now);
        disc.RaiseUpserted(old);
        disc.RaiseUpserted(MakePeer(instance, "Новое имя", "h", clock.Now.AddSeconds(20)));

        // Discovery removes the old object under its lock, then raises expiry
        // outside the lock. A fresh announcement can reach the UI first.
        disc.RaiseExpired(old);

        tr.Peers.Should().ContainSingle();
        tr.Peers[0].NodeName.Should().Be("Новое имя");
        tr.Peers[0].IsLive.Should().BeTrue();
    }

    [Fact]
    public void Changed_ip_replaces_the_displayed_address_even_with_previous_transfer_diagnostics()
    {
        var dc = new DataContext { Tournament = new Entities.Tournament(new GlobalSettings()) };
        var discovery = new TestDiscovery();
        var diagnostics = new PeerSyncDiagnostics();
        using var tracker = new PeerSyncStatusTracker(discovery, dc, null, diagnostics: diagnostics);
        var instance = Guid.NewGuid();
        var hash = PeerStateHasher.Compute(dc.Tournament);
        var old = MakePeer(instance, "Ковер C", hash, DateTime.UtcNow);
        typeof(DiscoveredPeer).GetProperty("HttpUrl").SetValue(old, "http://192.168.1.5:24566/tournament/x.wrt");
        discovery.RaiseUpserted(old);
        diagnostics.Report(instance, old.HttpUrl, PeerSyncPhase.FileUnavailable, DateTime.UtcNow);
        var current = MakePeer(instance, "Ковер C", hash, DateTime.UtcNow.AddSeconds(5));
        typeof(DiscoveredPeer).GetProperty("HttpUrl").SetValue(current, "http://10.1.30.5:24566/tournament/x.wrt");

        discovery.RaiseUpserted(current);

        tracker.Peers.Should().ContainSingle();
        tracker.Peers[0].Details.Should().Contain("10.1.30.5").And.NotContain("192.168.1.5");
        tracker.Peers[0].IsLive.Should().BeTrue();
    }

    [Fact]
    public void Offline_previous_instance_at_the_same_endpoint_is_removed_on_rename()
    {
        var (tr, disc, _, clock) = Build();
        var old = MakePeer(Guid.NewGuid(), "Старое имя", "h", clock.Now);
        typeof(DiscoveredPeer).GetProperty("HttpUrl").SetValue(old, "http://192.168.1.5:24566/tournament/x.wrt");
        disc.RaiseUpserted(old);
        disc.RaiseExpired(old);
        var current = MakePeer(Guid.NewGuid(), "Новое имя", "h", clock.Now.AddSeconds(5));
        typeof(DiscoveredPeer).GetProperty("HttpUrl").SetValue(current, old.HttpUrl);

        disc.RaiseUpserted(current);

        tr.Peers.Should().ContainSingle().Which.NodeName.Should().Be("Новое имя");
    }

    [Theory]
    [InlineData("http://192.168.1.6:24566/tournament/x.wrt")]
    [InlineData("http://192.168.1.5:24567/tournament/x.wrt")]
    [InlineData(null)]
    public void Same_name_at_a_different_or_unknown_endpoint_does_not_remove_offline_peer(string address)
    {
        var (tr, disc, _, clock) = Build();
        var old = MakePeer(Guid.NewGuid(), "Ковер", "h", clock.Now);
        typeof(DiscoveredPeer).GetProperty("HttpUrl").SetValue(old, "http://192.168.1.5:24566/tournament/x.wrt");
        disc.RaiseUpserted(old);
        disc.RaiseExpired(old);
        var current = MakePeer(Guid.NewGuid(), "Ковер", "h", clock.Now.AddSeconds(5));
        typeof(DiscoveredPeer).GetProperty("HttpUrl").SetValue(current, address);

        disc.RaiseUpserted(current);

        tr.Peers.Should().HaveCount(2);
    }

    [Fact]
    public void Old_instance_expiring_after_replacement_announcement_is_removed()
    {
        var (tr, disc, _, clock) = Build();
        var old = MakePeer(Guid.NewGuid(), "Старое имя", "h", clock.Now);
        typeof(DiscoveredPeer).GetProperty("HttpUrl").SetValue(old, "http://192.168.1.5:24566/tournament/x.wrt");
        var current = MakePeer(Guid.NewGuid(), "Новое имя", "h", clock.Now.AddSeconds(5));
        typeof(DiscoveredPeer).GetProperty("HttpUrl").SetValue(current, old.HttpUrl);
        disc.RaiseUpserted(old);
        disc.RaiseUpserted(current);
        tr.Peers.Should().HaveCount(2, "separate live instances must not be merged");

        disc.RaiseExpired(old);

        tr.Peers.Should().ContainSingle().Which.InstanceId.Should().Be(current.InstanceId);
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

    [Fact]
    public void Duplicate_names_show_computer_details_without_merging_live_instances()
    {
        var (tracker, discovery, _, clock) = Build();
        using (tracker)
        {
            var first = MakePeer(Guid.NewGuid(), "Ковёр A", "h", clock.Now);
            var second = MakePeer(Guid.NewGuid(), "Ковёр A", "h", clock.Now);
            typeof(DiscoveredPeer).GetProperty("ComputerName").SetValue(first, "LAPTOP-1");
            typeof(DiscoveredPeer).GetProperty("ComputerName").SetValue(second, "LAPTOP-2");
            discovery.RaiseUpserted(first); discovery.RaiseUpserted(second);
            tracker.Peers.Should().HaveCount(2);
            tracker.Peers.Should().OnlyContain(p => p.Details.Contains("Одинаковое имя"));
            tracker.Peers[0].Details.Should().Contain("LAPTOP-1");
            typeof(DiscoveredPeer).GetProperty("NodeName").SetValue(second, "Ковёр B");
            discovery.RaiseUpserted(second);
            tracker.Peers.Should().OnlyContain(p => !p.Details.Contains("Одинаковое имя"));
        }
    }

    [Fact]
    public void Delayed_advertisement_after_closing_or_switching_tournament_is_ignored()
    {
        var (tracker, discovery, context, clock) = Build();
        using (tracker)
        {
            var peer = MakePeer(Guid.NewGuid(), "Old tournament peer", "h", clock.Now);
            context.Tournament = null;
            discovery.RaiseUpserted(peer);
            tracker.Peers.Should().BeEmpty();
            context.Tournament = new Entities.Tournament(new GlobalSettings()) { ID = Guid.NewGuid() };
            discovery.RaiseUpserted(peer);
            tracker.Peers.Should().BeEmpty();
        }
    }
}
