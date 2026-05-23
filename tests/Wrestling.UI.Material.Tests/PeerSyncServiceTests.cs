using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Xunit;

namespace Wrestling.UI.Material.Tests;

// PeerSyncService is the engine that converges local state to peers.
// These tests exercise the failure-handling invariants directly via the
// internal HandlePeerAsync entry point, bypassing the WPF Dispatcher path.
//
// The invariants tested:
//   - Skip pull when peer hash matches local hash (no work).
//   - Skip pull when peer's previous hash was already pulled (dedup).
//   - **W1.2**: A failed pull does NOT update _lastPulledHashByPeer, so the
//     next identical announce will retry. This is the regression we just
//     introduced; the test pins it.
//   - **W2.3**: Three consecutive pull failures from a peer arm a 30-second
//     cooldown — further announces from that peer are skipped.
//   - Tournament-changed event clears all per-peer state.
//   - Autosave fires only on Imported.
public sealed class PeerSyncServiceTests
{
    private sealed class StubImporter : ITournamentImporter
    {
        public ImportOutcome NextOutcome { get; set; } = ImportOutcome.Imported;
        public int NextImportedCount { get; set; } = 1;
        public int PrepareCalls { get; private set; }
        public int ApplyCalls { get; private set; }

        // For the tournament-changed-mid-prepare test we let the test code
        // mutate state between Prepare and Apply.
        public Func<Task> BeforeApply { get; set; }

        public Task<ImportPlan> PrepareAsync(Entities.Tournament target, string fileName, System.Threading.CancellationToken cancellationToken = default)
        {
            PrepareCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            switch (NextOutcome)
            {
                case ImportOutcome.FileUnavailable: return Task.FromResult(ImportPlan.Skip(ImportOutcome.FileUnavailable));
                case ImportOutcome.TournamentMismatch: return Task.FromResult(ImportPlan.Skip(ImportOutcome.TournamentMismatch));
                case ImportOutcome.Error: return Task.FromResult(ImportPlan.Skip(ImportOutcome.Error));
                default:
                    var remote = new Entities.Tournament(new GlobalSettings()) { Name = target.Name };
                    return Task.FromResult(ImportPlan.Proceed(remote));
            }
        }

        public ImportResult Apply(Entities.Tournament target, ImportPlan plan)
        {
            ApplyCalls++;
            BeforeApply?.Invoke().GetAwaiter().GetResult();
            return new ImportResult(NextOutcome, NextOutcome == ImportOutcome.Imported ? NextImportedCount : 0);
        }
    }

    private sealed class TestDiscovery : IPeerDiscoveryService
    {
        // PeerUpserted is unused in tests because we drive HandlePeerAsync
        // directly; suppress the unused-event warning.
#pragma warning disable CS0067
        public event EventHandler<DiscoveredPeer> PeerUpserted;
#pragma warning restore CS0067
        public event EventHandler<DiscoveredPeer> PeerExpired;
        public event EventHandler<string> DiagnosticMessage { add { } remove { } }

        public IReadOnlyCollection<DiscoveredPeer> SnapshotPeers() => Array.Empty<DiscoveredPeer>();
        public void StartForTournament(int port, Guid tournamentId, string tournamentTitle, string nodeName, string httpUrl, Func<string> stateHashProvider = null) { }
        public void Stop() { }
        public void Dispose() { }

        public void RaiseExpired(DiscoveredPeer p) => PeerExpired?.Invoke(this, p);
    }

    // Discovery field uses internal setter on DiscoveredPeer; build via reflection-free factory.
    private static DiscoveredPeer MakePeer(Guid instance, string hash, string nodeName = "Ковёр")
    {
        // DiscoveredPeer ctor is internal — we're in InternalsVisibleTo for both Wrestling.UI.Material
        // and Wrestling.Providers via... actually only Wrestling.UI.Material has that hookup.
        // Use Activator.CreateInstance to bypass — DiscoveredPeer is constructed via internal ctor.
        var peer = (DiscoveredPeer)Activator.CreateInstance(
            typeof(DiscoveredPeer),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            new object[] { instance, Guid.NewGuid() },
            null);
        // Internal setters; same trick.
        typeof(DiscoveredPeer).GetProperty("StateHash").SetValue(peer, hash);
        typeof(DiscoveredPeer).GetProperty("NodeName").SetValue(peer, nodeName);
        typeof(DiscoveredPeer).GetProperty("HttpUrl").SetValue(peer, "http://127.0.0.1:9999/x.wrt");
        return peer;
    }

    private static (PeerSyncService svc, StubImporter imp, FakeTournamentsManager mgr, DataContext dc, TestDiscovery disc, FuncClock clock)
        Build(Entities.Tournament tournament = null, ImportOutcome outcome = ImportOutcome.Imported, IResultsService resultsService = null)
    {
        var dc = new DataContext { Tournament = tournament ?? new Entities.Tournament(new GlobalSettings()) { Name = "T", FileName = "tournament.wrt" } };
        var imp = new StubImporter { NextOutcome = outcome };
        var mgr = new FakeTournamentsManager();
        var disc = new TestDiscovery();
        var clock = new FuncClock();
        // Pass null dispatcher — we call HandlePeerAsync directly to bypass the
        // marshal-to-UI step.
        var svc = new PeerSyncService(disc, dc, imp, mgr, resultsService: resultsService, uiDispatcher: null, clock: () => clock.Now);
        return (svc, imp, mgr, dc, disc, clock);
    }

    private sealed class StubResultsService : IResultsService
    {
        public List<Entities.Tournament> RecalculateCalls { get; } = new();
        public IReadOnlyList<Entities.Results.TournamentResult> AllResults { get; private set; } = new List<Entities.Results.TournamentResult>();
        public IReadOnlyList<Entities.Results.TournamentTeamResult> TeamResults { get; private set; } = new List<Entities.Results.TournamentTeamResult>();
        public IReadOnlyList<Entities.WrestlerAchievement> Achievements { get; private set; } = new List<Entities.WrestlerAchievement>();
#pragma warning disable CS0067
        public event Action ResultsChanged;
#pragma warning restore CS0067
        public void Recalculate(Entities.Tournament tournament) => RecalculateCalls.Add(tournament);
        public IReadOnlyList<Entities.Results.TournamentTeamResult> GetOrderedTeamResults(Entities.Results.ITeamResultsOrderer orderer) => TeamResults;
    }

    private sealed class FuncClock { public DateTime Now { get; set; } = DateTime.UtcNow; }

    [Fact]
    public async Task Skips_pull_when_peer_hash_matches_local_hash()
    {
        var (svc, imp, _, dc, _, _) = Build();
        var localHash = PeerStateHasher.Compute(dc.Tournament);
        var peer = MakePeer(Guid.NewGuid(), hash: localHash);

        await svc.HandlePeerAsync(peer);

        imp.PrepareCalls.Should().Be(0);
        imp.ApplyCalls.Should().Be(0);
    }

    [Fact]
    public async Task Pulls_when_peer_hash_differs()
    {
        var (svc, imp, _, _, _, _) = Build();
        var peer = MakePeer(Guid.NewGuid(), hash: "differenthash1");

        await svc.HandlePeerAsync(peer);

        imp.PrepareCalls.Should().Be(1);
        imp.ApplyCalls.Should().Be(1);
    }

    [Fact]
    public async Task Skips_when_peer_StateHash_is_empty()
    {
        // First-tick announce arrives before peer computes its hash. Don't spam pulls on it.
        var (svc, imp, _, _, _, _) = Build();
        var peer = MakePeer(Guid.NewGuid(), hash: string.Empty);

        await svc.HandlePeerAsync(peer);

        imp.PrepareCalls.Should().Be(0);
    }

    [Fact]
    public async Task Dedups_repeat_announces_with_same_remote_hash()
    {
        var (svc, imp, _, _, _, _) = Build();
        var peerId = Guid.NewGuid();

        await svc.HandlePeerAsync(MakePeer(peerId, "remotehashA"));
        await svc.HandlePeerAsync(MakePeer(peerId, "remotehashA"));

        imp.PrepareCalls.Should().Be(1, "second announce with the same hash must be deduped");
    }

    [Fact]
    public async Task Failed_pull_does_NOT_cache_hash_so_next_identical_announce_retries()
    {
        // W1.2 regression: peer briefly unreachable returns FileUnavailable;
        // we must retry on the next identical-hash announce instead of
        // remembering "already pulled". Otherwise a single transient failure
        // strands the local copy until the peer bumps a version field.
        var (svc, imp, _, _, _, _) = Build(outcome: ImportOutcome.FileUnavailable);
        var peer = MakePeer(Guid.NewGuid(), hash: "remotehashX");

        await svc.HandlePeerAsync(peer);
        await svc.HandlePeerAsync(peer);

        imp.PrepareCalls.Should().Be(2, "FileUnavailable must not cache the peer's hash");
    }

    [Fact]
    public async Task Successful_pull_caches_hash_and_skips_repeat_announce()
    {
        var (svc, imp, _, _, _, _) = Build(outcome: ImportOutcome.Imported);
        var peer = MakePeer(Guid.NewGuid(), hash: "remotehashY");

        await svc.HandlePeerAsync(peer);
        await svc.HandlePeerAsync(peer);

        imp.PrepareCalls.Should().Be(1, "Imported caches the hash; repeat is no-op");
    }

    [Fact]
    public async Task NoNewData_outcome_caches_hash()
    {
        // Common when our local state is already ahead of theirs (e.g. they
        // restored an old .wrt). Treat as a successful round-trip.
        var (svc, imp, _, _, _, _) = Build(outcome: ImportOutcome.NoNewData);
        var peer = MakePeer(Guid.NewGuid(), hash: "remotehashZ");

        await svc.HandlePeerAsync(peer);
        await svc.HandlePeerAsync(peer);

        imp.PrepareCalls.Should().Be(1);
    }

    [Fact]
    public async Task Three_consecutive_failures_arm_30s_cooldown()
    {
        // W2.3: a downed peer cannot keep dragging the UI thread through
        // 5-second HTTP timeouts on every announce. After 3 fails it goes
        // into cooldown — pulls are skipped until 30s elapse.
        var (svc, imp, _, _, _, clock) = Build(outcome: ImportOutcome.Error);
        var peerId = Guid.NewGuid();

        await svc.HandlePeerAsync(MakePeer(peerId, "h1"));
        await svc.HandlePeerAsync(MakePeer(peerId, "h2"));
        await svc.HandlePeerAsync(MakePeer(peerId, "h3"));

        imp.PrepareCalls.Should().Be(3, "first three failures still attempt the pull");

        // Fourth announce within cooldown is skipped.
        await svc.HandlePeerAsync(MakePeer(peerId, "h4"));

        imp.PrepareCalls.Should().Be(3, "cooldown active, skip the pull");
    }

    [Fact]
    public async Task Cooldown_lifts_after_30_seconds()
    {
        var (svc, imp, _, _, _, clock) = Build(outcome: ImportOutcome.Error);
        var peerId = Guid.NewGuid();

        await svc.HandlePeerAsync(MakePeer(peerId, "h1"));
        await svc.HandlePeerAsync(MakePeer(peerId, "h2"));
        await svc.HandlePeerAsync(MakePeer(peerId, "h3"));

        clock.Now = clock.Now.AddSeconds(31);

        await svc.HandlePeerAsync(MakePeer(peerId, "h4"));

        imp.PrepareCalls.Should().Be(4, "cooldown elapsed, pull resumes");
    }

    [Fact]
    public async Task Successful_pull_resets_failure_counter()
    {
        var (svc, imp, _, _, _, clock) = Build(outcome: ImportOutcome.Error);
        var peerId = Guid.NewGuid();

        await svc.HandlePeerAsync(MakePeer(peerId, "h1"));
        await svc.HandlePeerAsync(MakePeer(peerId, "h2"));

        // Now a successful announce.
        imp.NextOutcome = ImportOutcome.Imported;
        await svc.HandlePeerAsync(MakePeer(peerId, "h3"));

        // Two more fails should NOT yet trigger cooldown — the counter was reset.
        imp.NextOutcome = ImportOutcome.Error;
        await svc.HandlePeerAsync(MakePeer(peerId, "h4"));
        await svc.HandlePeerAsync(MakePeer(peerId, "h5"));

        imp.PrepareCalls.Should().Be(5, "all five announces should attempt — counter reset by success in middle");
    }

    [Fact]
    public async Task PeerExpired_clears_per_peer_state()
    {
        // W2.2: long sessions with reconnections must not grow the per-peer
        // dictionaries unboundedly.
        var (svc, imp, _, _, disc, _) = Build(outcome: ImportOutcome.Imported);
        var peerId = Guid.NewGuid();
        var peer = MakePeer(peerId, "h1");

        await svc.HandlePeerAsync(peer);
        imp.PrepareCalls.Should().Be(1);

        // Same hash again — deduped.
        await svc.HandlePeerAsync(peer);
        imp.PrepareCalls.Should().Be(1);

        // Expire and re-announce: dedup cache cleared, pull retries.
        disc.RaiseExpired(peer);
        await svc.HandlePeerAsync(peer);
        imp.PrepareCalls.Should().Be(2);
    }

    [Fact]
    public async Task Tournament_change_clears_all_per_peer_state()
    {
        var (svc, imp, _, dc, _, _) = Build(outcome: ImportOutcome.Imported);
        var peerId = Guid.NewGuid();

        await svc.HandlePeerAsync(MakePeer(peerId, "h1"));
        imp.PrepareCalls.Should().Be(1);

        // Switch tournament — dedup cache must clear so the same announce
        // is considered novel against the new local state.
        dc.Tournament = new Entities.Tournament(new GlobalSettings()) { Name = "Other", FileName = "other.wrt" };

        await svc.HandlePeerAsync(MakePeer(peerId, "h1"));
        imp.PrepareCalls.Should().Be(2, "tournament change clears the dedup cache");
    }

    [Fact]
    public async Task Autosave_fires_only_on_Imported()
    {
        // Sanity check: NoNewData / FileUnavailable / Error must not save.
        var t = new Entities.Tournament(new GlobalSettings()) { Name = "T", FileName = "tournament.wrt" };
        var (svc, imp, mgr, _, _, _) = Build(t, outcome: ImportOutcome.NoNewData);
        await svc.HandlePeerAsync(MakePeer(Guid.NewGuid(), "h-other"));

        mgr.SaveAsyncCount.Should().Be(0);

        imp.NextOutcome = ImportOutcome.Imported;
        await svc.HandlePeerAsync(MakePeer(Guid.NewGuid(), "h-yet-another"));

        mgr.SaveAsyncCount.Should().Be(1);
    }

    [Fact]
    public async Task Autosave_skipped_when_FileName_is_empty()
    {
        var t = new Entities.Tournament(new GlobalSettings()) { Name = "T", FileName = string.Empty };
        var (svc, _, mgr, _, _, _) = Build(t);
        await svc.HandlePeerAsync(MakePeer(Guid.NewGuid(), "h"));

        mgr.SaveAsyncCount.Should().Be(0);
    }

    [Fact]
    public async Task Imported_outcome_triggers_results_recalc()
    {
        // Recalc must fire on the SAME tournament instance held by DataContext —
        // and only once per merge, before the autosave hook.
        var rs = new StubResultsService();
        var (svc, _, _, dc, _, _) = Build(outcome: ImportOutcome.Imported, resultsService: rs);

        await svc.HandlePeerAsync(MakePeer(Guid.NewGuid(), "remote-hash"));

        rs.RecalculateCalls.Should().HaveCount(1);
        rs.RecalculateCalls[0].Should().BeSameAs(dc.Tournament);
    }

    [Theory]
    [InlineData(ImportOutcome.NoNewData)]
    [InlineData(ImportOutcome.FileUnavailable)]
    [InlineData(ImportOutcome.Error)]
    [InlineData(ImportOutcome.TournamentMismatch)]
    public async Task NonImported_outcomes_do_not_trigger_results_recalc(ImportOutcome outcome)
    {
        // No new completed matches merged → results haven't changed → no
        // recalc cost. Mirrors the autosave gate's "Imported only" rule.
        var rs = new StubResultsService();
        var (svc, _, _, _, _, _) = Build(outcome: outcome, resultsService: rs);

        await svc.HandlePeerAsync(MakePeer(Guid.NewGuid(), "remote-hash"));

        rs.RecalculateCalls.Should().BeEmpty();
    }

    // ---------- Wave 2.3 cancellation ----------

    // Stub that observes a CancellationToken and blocks until cancelled,
    // then throws OperationCanceledException — models a real HTTP fetch
    // that's been told to abort mid-flight.
    private sealed class CancellingImporter : ITournamentImporter
    {
        public TaskCompletionSource<bool> Started { get; } = new TaskCompletionSource<bool>();
        public bool WasCancelled { get; private set; }
        public int ApplyCalls { get; private set; }

        public async Task<ImportPlan> PrepareAsync(Entities.Tournament target, string fileName, System.Threading.CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            try
            {
                await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }
            return ImportPlan.Skip(ImportOutcome.NoNewData);
        }

        public ImportResult Apply(Entities.Tournament target, ImportPlan plan)
        {
            ApplyCalls++;
            return new ImportResult(ImportOutcome.NoNewData, 0);
        }
    }

    [Fact]
    public async Task Tournament_change_cancels_in_flight_pull()
    {
        var dc = new DataContext { Tournament = new Entities.Tournament(new GlobalSettings()) { Name = "T", FileName = "t.wrt" } };
        var importer = new CancellingImporter();
        var disc = new TestDiscovery();
        var svc = new PeerSyncService(disc, dc, importer, new FakeTournamentsManager(),
            resultsService: null, uiDispatcher: null);

        var peer = MakePeer(Guid.NewGuid(), "remote-hash");
        var pullTask = svc.HandlePeerAsync(peer);

        // Wait until PrepareAsync is actually running.
        await importer.Started.Task;

        // Swap the tournament — should cancel the in-flight token.
        dc.Tournament = new Entities.Tournament(new GlobalSettings()) { Name = "T2", FileName = "t2.wrt" };

        await pullTask;

        importer.WasCancelled.Should().BeTrue("OperationCanceledException must surface to importer");
        importer.ApplyCalls.Should().Be(0, "Apply must not run when prepare was cancelled");
    }

    [Fact]
    public async Task Dispose_cancels_in_flight_pull()
    {
        var dc = new DataContext { Tournament = new Entities.Tournament(new GlobalSettings()) { Name = "T", FileName = "t.wrt" } };
        var importer = new CancellingImporter();
        var disc = new TestDiscovery();
        var svc = new PeerSyncService(disc, dc, importer, new FakeTournamentsManager(),
            resultsService: null, uiDispatcher: null);

        var peer = MakePeer(Guid.NewGuid(), "remote-hash");
        var pullTask = svc.HandlePeerAsync(peer);

        await importer.Started.Task;

        svc.Dispose();

        await pullTask;

        importer.WasCancelled.Should().BeTrue();
        importer.ApplyCalls.Should().Be(0);
    }
}
