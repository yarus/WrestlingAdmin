using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.UI.Material.Model;
using Xunit;
using WTournament = Wrestling.Entities.Tournament;

namespace Wrestling.UI.Material.Tests;

// Regression suite for #14 — version-based import flow.
// The previous design tracked completion authority by source-string equality
// ("only the peer that gave us this completion may revert it"). With three or
// more peers and arbitrary import ordering, that string-stamp got pinned to
// whichever middleman replied first, so a revert by the original author was
// never recognised when it travelled through a different peer. The new model
// stores a monotonic Version on each match and applies any state the importer
// sees at strictly higher Version, regardless of the path the change took to
// reach this peer.
public sealed class TournamentImporterApplyTests
{
    // 4-wrestler Olympic bracket: two semifinals → final + 3rd place. Enough
    // structure to exercise CompleteMatch / RevertMatch propagation without
    // edge cases of FreeWin in odd brackets.
    private static (WTournament, AgeWeightGroup, OlympicGroupBracketProcessor) BuildPair()
    {
        var groupId = Guid.NewGuid();
        var wrestlers = new List<Wrestler>();
        for (int i = 0; i < 4; i++)
        {
            wrestlers.Add(new Wrestler
            {
                ID = Guid.NewGuid(),
                LastName = "W" + i,
                FirstName = "F" + i,
                BirthDate = new DateTime(2005, 1, 1),
                Weight = 60,
                IsEntryFeePaid = true,
                IsWeightApproved = true,
                GroupID = groupId,
                SeedNumber = i + 1
            });
        }

        var group = new AgeWeightGroup
        {
            ID = groupId,
            BirthYearMin = 2005, BirthYearMax = 2006, WeightMax = 60,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30,
            Wrestlers = wrestlers
        };

        var t = new WTournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "Test", Status = TournamentStatus.InProgress };
        t.Groups.Add(group);
        foreach (var w in wrestlers) t.Wrestlers.Add(w);

        var processor = new OlympicGroupBracketProcessor();
        processor.Generate(t, group);
        AssignSequentialMatchNumbers(group);

        return (t, group, processor);
    }

    // Mirrors a peer's tournament: same group ID, same wrestler IDs, same
    // bracket structure, identical MatchNumber assignments. Models a separate
    // .wrt file that has been loaded from a remote peer.
    private static (WTournament Remote, AgeWeightGroup Group, OlympicGroupBracketProcessor Proc) Mirror(WTournament source)
    {
        var srcGroup = source.Groups[0];
        var clones = srcGroup.Wrestlers.Select(w => new Wrestler
        {
            ID = w.ID, LastName = w.LastName, FirstName = w.FirstName, BirthDate = w.BirthDate,
            Weight = w.Weight, IsEntryFeePaid = true, IsWeightApproved = true,
            GroupID = srcGroup.ID, SeedNumber = w.SeedNumber
        }).ToList();

        var group = new AgeWeightGroup
        {
            ID = srcGroup.ID,
            BirthYearMin = srcGroup.BirthYearMin, BirthYearMax = srcGroup.BirthYearMax,
            WeightMax = srcGroup.WeightMax,
            MaxRoundSecond = srcGroup.MaxRoundSecond, MaxTimeoutSecond = srcGroup.MaxTimeoutSecond, MaxActionSecond = srcGroup.MaxActionSecond,
            Wrestlers = clones
        };

        var t = new WTournament(new GlobalSettings()) { ID = source.ID, Name = source.Name, Status = TournamentStatus.InProgress };
        t.Groups.Add(group);
        foreach (var w in clones) t.Wrestlers.Add(w);

        var proc = new OlympicGroupBracketProcessor();
        proc.Generate(t, group);
        AssignSequentialMatchNumbers(group);

        return (t, group, proc);
    }

    // Real code does this through MatchNumbersGenerator on the UI side; the
    // test mirrors the contract by numbering matches sequentially across rounds
    // so target and mirror line up by MatchNumber inside Apply.
    private static void AssignSequentialMatchNumbers(AgeWeightGroup g)
    {
        int n = 1;
        foreach (var round in g.Bracket.Rounds)
            foreach (var m in round.RoundMatches)
                m.MatchNumber = n++;
    }

    private static WrestlingMatch FirstPendingMatch(AgeWeightGroup g) =>
        g.Bracket.Rounds.SelectMany(r => r.RoundMatches).First(m => m.Status == MatchStatusEnum.Pending);

    private static TournamentImporter MakeImporter() =>
        new TournamentImporter(
            new NullTournamentsManager(),
            new List<IGroupBracketProcessor> { new OlympicGroupBracketProcessor() },
            new CarpetMatchNumbersGenerator());

    // Round-trip a tournament through the production adapter. Models the
    // "save and reload as another peer" step end-to-end scenarios need —
    // the cloned tournament has independent Wrestler / Group / Bracket /
    // Match instances with the same IDs as the source.
    private static WTournament CloneViaAdapter(WTournament source)
    {
        var adapter = new Wrestling.Providers.EntityToInfoAdapter();
        var info = adapter.GetInfoFromEntity(source);
        return adapter.GetEntityFromInfo(info);
    }

    private static Wrestler MakeWrestler(Guid groupId, string lastName, int seed) => new Wrestler
    {
        ID = Guid.NewGuid(),
        LastName = lastName,
        FirstName = "F",
        BirthDate = new DateTime(2005, 1, 1),
        Weight = 60,
        IsEntryFeePaid = true,
        IsWeightApproved = true,
        GroupID = groupId,
        SeedNumber = seed
    };

    // Two-group tournament on one carpet, both groups with brackets generated
    // and per-carpet match numbers assigned. Models a typical pre-tournament
    // ready-to-go state.
    private static (WTournament Tournament, AgeWeightGroup G1, AgeWeightGroup G2, Carpet Carpet) BuildTwoGroupTournament(int g1Size, int g2Size)
    {
        var t = new WTournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T", Status = TournamentStatus.InProgress };
        var carpet = new Carpet { ID = Guid.NewGuid(), Name = "Ковёр 1" };
        t.Carpets.Add(carpet);

        var g1Id = Guid.NewGuid();
        var g1Wrestlers = new List<Wrestler>();
        for (int i = 0; i < g1Size; i++) g1Wrestlers.Add(MakeWrestler(g1Id, "G1-" + i, i + 1));
        var g1 = new AgeWeightGroup
        {
            ID = g1Id, BirthYearMin = 2005, BirthYearMax = 2006, WeightMax = 55,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30,
            CarpetID = carpet.ID, Wrestlers = g1Wrestlers
        };
        t.Groups.Add(g1);
        carpet.Groups.Add(g1);
        foreach (var w in g1Wrestlers) t.Wrestlers.Add(w);

        var g2Id = Guid.NewGuid();
        var g2Wrestlers = new List<Wrestler>();
        for (int i = 0; i < g2Size; i++) g2Wrestlers.Add(MakeWrestler(g2Id, "G2-" + i, i + 1));
        var g2 = new AgeWeightGroup
        {
            ID = g2Id, BirthYearMin = 2005, BirthYearMax = 2006, WeightMax = 60,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30,
            CarpetID = carpet.ID, Wrestlers = g2Wrestlers
        };
        t.Groups.Add(g2);
        carpet.Groups.Add(g2);
        foreach (var w in g2Wrestlers) t.Wrestlers.Add(w);

        var processor = new OlympicGroupBracketProcessor();
        processor.Generate(t, g1);
        processor.Generate(t, g2);
        new CarpetMatchNumbersGenerator().Generate(t,
            new List<IGroupBracketProcessor> { new OlympicGroupBracketProcessor() });

        return (t, g1, g2, carpet);
    }

    // Compares the structurally-syncable parts of two tournaments (groups,
    // wrestlers, brackets, match identities, status, version). Carpets and
    // settings/import sources are intentionally local-per-laptop and not
    // checked here.
    private static void AssertEquivalent(WTournament expected, WTournament actual)
    {
        actual.Wrestlers.Select(w => w.ID).Should().BeEquivalentTo(expected.Wrestlers.Select(w => w.ID));
        actual.Groups.Select(g => g.ID).Should().BeEquivalentTo(expected.Groups.Select(g => g.ID));

        foreach (var expectedGroup in expected.Groups)
        {
            var actualGroup = actual.Groups.First(g => g.ID == expectedGroup.ID);
            actualGroup.Wrestlers.Select(w => w.ID).Should().BeEquivalentTo(
                expectedGroup.Wrestlers.Select(w => w.ID),
                $"group {expectedGroup.ID} membership");
            actualGroup.CarpetID.Should().Be(expectedGroup.CarpetID, $"group {expectedGroup.ID} carpet");
            actualGroup.MaxRoundSecond.Should().Be(expectedGroup.MaxRoundSecond);
            actualGroup.BracketVersion.Should().Be(expectedGroup.BracketVersion);
            actualGroup.FieldsVersion.Should().Be(expectedGroup.FieldsVersion);

            if (expectedGroup.Bracket == null)
            {
                actualGroup.Bracket.Should().BeNull();
                continue;
            }
            actualGroup.Bracket.Should().NotBeNull();

            var expectedMatches = expectedGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();
            var actualMatches = actualGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();
            actualMatches.Should().HaveCount(expectedMatches.Count);

            foreach (var em in expectedMatches)
            {
                var am = actualMatches.First(m => m.BracketFullNumber == em.BracketFullNumber);
                am.Status.Should().Be(em.Status, $"group {expectedGroup.ID} match {em.BracketFullNumber}");
                am.Version.Should().Be(em.Version);
                am.MatchNumber.Should().Be(em.MatchNumber);
                am.IsRedWon.Should().Be(em.IsRedWon);
                am.WinType.Should().Be(em.WinType);
                (am.WrestlerInRed?.ID).Should().Be(em.WrestlerInRed?.ID);
                (am.WrestlerInBlue?.ID).Should().Be(em.WrestlerInBlue?.ID);
            }
        }
    }

    // Apply doesn't touch the tournaments manager — it operates on the already-
    // loaded remote tournament passed via ImportPlan.Proceed. The stub satisfies
    // the constructor without exercising any I/O.
    private sealed class NullTournamentsManager : Wrestling.Providers.ITournamentsManager
    {
        public WTournament LoadFromFile(string fileName) => null;
        public System.Threading.Tasks.Task<WTournament> LoadFromFileAsync(string fileName) =>
            System.Threading.Tasks.Task.FromResult<WTournament>(null);
        public bool SaveToFile(WTournament item, string fileName) => true;
        public System.Threading.Tasks.Task<bool> SaveToFileAsync(WTournament item, string fileName) =>
            System.Threading.Tasks.Task.FromResult(true);
    }

    // Case 1 baseline: a remote completion at V=1 promotes a local Pending
    // match. The local copy carries the new Version after Apply so subsequent
    // ticks recognise it as already-applied.
    [Fact]
    public void Remote_completion_at_higher_version_promotes_local_pending()
    {
        var (target, tgtGroup, _) = BuildPair();
        var (remote, remGroup, remProc) = Mirror(target);

        var remMatch = FirstPendingMatch(remGroup);
        remMatch.IsRedWon = true;
        remMatch.WinType = MatchWinTypeEnum.PointsWin;
        remMatch.PointsRed = 5;
        remMatch.PointsBlue = 2;
        remProc.CompleteMatch(remMatch, true, MatchWinTypeEnum.PointsWin);
        remMatch.Version = 1;

        var importer = MakeImporter();
        var result = importer.Apply(target, ImportPlan.Proceed(remote));

        result.Outcome.Should().Be(ImportOutcome.Imported);
        var local = tgtGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                            .First(m => m.MatchNumber == remMatch.MatchNumber);
        local.Status.Should().Be(MatchStatusEnum.Completed);
        local.IsRedWon.Should().BeTrue();
        local.PointsRed.Should().Be(5);
        local.Version.Should().Be(1);
    }

    // The flagship #14 scenario. Three peers — A (author), B (middleman), C
    // (this peer). C imports the completion from B; that no longer matters
    // because the local stamp does not record a source. When A reverts and C
    // imports directly from A, the strictly-higher remote Version drives the
    // revert regardless of the path.
    [Fact]
    public void Revert_from_any_peer_propagates_when_remote_version_is_higher()
    {
        var (target, tgtGroup, _) = BuildPair();

        // Step 1: B-shaped remote at V=1 Completed. C imports — this is the
        // baseline state that the old design got stuck on (stamped with B's
        // source, refused to honour A's later revert).
        var (remoteB, remBGroup, remBProc) = Mirror(target);
        var remMatchB = FirstPendingMatch(remBGroup);
        remMatchB.IsRedWon = true;
        remMatchB.WinType = MatchWinTypeEnum.PointsWin;
        remBProc.CompleteMatch(remMatchB, true, MatchWinTypeEnum.PointsWin);
        remMatchB.Version = 1;

        var importer = MakeImporter();
        importer.Apply(target, ImportPlan.Proceed(remoteB));
        var local = tgtGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                            .First(m => m.MatchNumber == remMatchB.MatchNumber);
        local.Status.Should().Be(MatchStatusEnum.Completed);
        local.Version.Should().Be(1);

        // Step 2: A reverts and we now read directly from A — a peer C never
        // imported the original completion from. With version-based ordering,
        // A's V=2 Pending strictly exceeds C's V=1 Completed → revert applies.
        var (remoteA, remAGroup, _) = Mirror(target);
        // remoteA matches the post-revert state on the author: Pending with V=2
        // (one bump for the original Approve, one for the Reject — the author
        // truly went 0→1→2).
        var remMatchA = remAGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                                  .First(m => m.MatchNumber == remMatchB.MatchNumber);
        remMatchA.Status.Should().Be(MatchStatusEnum.Pending, "Mirror() regenerates a fresh Pending bracket");
        remMatchA.Version = 2;

        importer.Apply(target, ImportPlan.Proceed(remoteA));

        local.Status.Should().Be(MatchStatusEnum.Pending);
        local.Version.Should().Be(2);
    }

    // Case 3: the author Reverted and re-Approved between our import ticks.
    // Locally we are still on V=1 with the previous result; remote is at V=3
    // with a new result. The importer must roll back the local bracket and
    // apply the new completion atomically.
    [Fact]
    public void Edit_after_approve_replaces_local_completion_when_remote_version_is_higher()
    {
        var (target, tgtGroup, _) = BuildPair();
        var (remote, remGroup, remProc) = Mirror(target);

        // Apply an initial completion to local at V=1 (red wins).
        var initial = Mirror(target);
        var initialMatch = FirstPendingMatch(initial.Group);
        initialMatch.IsRedWon = true;
        initialMatch.WinType = MatchWinTypeEnum.PointsWin;
        initialMatch.PointsRed = 4; initialMatch.PointsBlue = 1;
        initial.Proc.CompleteMatch(initialMatch, true, MatchWinTypeEnum.PointsWin);
        initialMatch.Version = 1;

        var importer = MakeImporter();
        importer.Apply(target, ImportPlan.Proceed(initial.Remote));
        var local = tgtGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                            .First(m => m.MatchNumber == initialMatch.MatchNumber);
        local.IsRedWon.Should().BeTrue();
        local.Version.Should().Be(1);

        // Now the author has reverted+re-approved with the corrected result
        // (blue wins). Remote shows V=3 Completed — V=1 (approve) → V=2
        // (revert) → V=3 (re-approve).
        var remMatch = remGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                               .First(m => m.MatchNumber == initialMatch.MatchNumber);
        remMatch.IsRedWon = false;
        remMatch.WinType = MatchWinTypeEnum.PointsWin;
        remMatch.PointsRed = 1; remMatch.PointsBlue = 6;
        remProc.CompleteMatch(remMatch, false, MatchWinTypeEnum.PointsWin);
        remMatch.Version = 3;

        importer.Apply(target, ImportPlan.Proceed(remote));

        local.Status.Should().Be(MatchStatusEnum.Completed);
        local.IsRedWon.Should().BeFalse();
        local.PointsBlue.Should().Be(6);
        local.Version.Should().Be(3);
    }

    // Both Pending but remote.Version is higher (the author Approved+Reverted
    // entirely between ticks). The bracket needs no change locally; only the
    // version catches up so the next genuine completion is recognised.
    [Fact]
    public void Both_pending_with_higher_remote_version_only_syncs_version()
    {
        var (target, tgtGroup, _) = BuildPair();
        var (remote, remGroup, _) = Mirror(target);

        var remMatch = FirstPendingMatch(remGroup);
        remMatch.Version = 4; // 0→1 (approve) → 2 (revert) → 3 (re-approve) → 4 (revert)

        var importer = MakeImporter();
        var result = importer.Apply(target, ImportPlan.Proceed(remote));

        // No bracket transition fired, so the importer reports NoNewData. The
        // version still bumps so the peer is now caught up.
        var local = tgtGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                            .First(m => m.MatchNumber == remMatch.MatchNumber);
        local.Status.Should().Be(MatchStatusEnum.Pending);
        local.Version.Should().Be(4);
        result.Outcome.Should().Be(ImportOutcome.NoNewData);
    }

    // Equal versions with diverging state: this is the rare "two operators
    // approved the same match concurrently" race. By contract the local copy
    // wins — operators notice the divergence on the dashboard and resolve it
    // manually rather than have the import-tick race silently pick a winner.
    [Fact]
    public void Equal_version_keeps_local_state_unchanged()
    {
        var (target, tgtGroup, tgtProc) = BuildPair();

        var localMatch = FirstPendingMatch(tgtGroup);
        localMatch.IsRedWon = true;
        localMatch.WinType = MatchWinTypeEnum.PointsWin;
        localMatch.PointsRed = 5; localMatch.PointsBlue = 0;
        tgtProc.CompleteMatch(localMatch, true, MatchWinTypeEnum.PointsWin);
        localMatch.Version = 1;

        var (remote, remGroup, remProc) = Mirror(target);
        var remMatch = remGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                               .First(m => m.MatchNumber == localMatch.MatchNumber);
        // Remote also Approved at V=1, but with the opposite winner — this is
        // the divergence we refuse to silently overwrite.
        remMatch.IsRedWon = false;
        remMatch.WinType = MatchWinTypeEnum.PointsWin;
        remMatch.PointsRed = 0; remMatch.PointsBlue = 5;
        remProc.CompleteMatch(remMatch, false, MatchWinTypeEnum.PointsWin);
        remMatch.Version = 1;

        var importer = MakeImporter();
        importer.Apply(target, ImportPlan.Proceed(remote));

        localMatch.IsRedWon.Should().BeTrue("equal versions keep local state");
        localMatch.PointsRed.Should().Be(5);
        localMatch.Version.Should().Be(1);
    }

    // Stale remote (lower version) is ignored even if the local match is
    // already Completed and remote shows Pending. This guards against an old
    // .wrt copy on a peer that fell behind temporarily then re-announced.
    [Fact]
    public void Stale_remote_with_lower_version_is_ignored()
    {
        var (target, tgtGroup, tgtProc) = BuildPair();

        var localMatch = FirstPendingMatch(tgtGroup);
        localMatch.IsRedWon = true;
        localMatch.WinType = MatchWinTypeEnum.PointsWin;
        tgtProc.CompleteMatch(localMatch, true, MatchWinTypeEnum.PointsWin);
        localMatch.Version = 3;

        var (remote, remGroup, _) = Mirror(target);
        var remMatch = remGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                               .First(m => m.MatchNumber == localMatch.MatchNumber);
        // Remote is fresh-Pending at V=1 (stale snapshot from before the
        // author's first Approve).
        remMatch.Version = 1;

        var importer = MakeImporter();
        importer.Apply(target, ImportPlan.Proceed(remote));

        localMatch.Status.Should().Be(MatchStatusEnum.Completed);
        localMatch.Version.Should().Be(3);
    }

    // -------------------------------------------------------------------------
    // Per-group structural sync — TodoList #5 / #10.
    // -------------------------------------------------------------------------
    // Two independent counters per group:
    //   FieldsVersion  — timing / CarpetID / age / weight / female / name edits
    //                    that don't touch bracket shape; cascade timing into
    //                    pending matches.
    //   BracketVersion — bracket regeneration only; replaces wrestlers list +
    //                    bracket; preserves locally-newer match completions.
    // The two are independent so a peer doesn't lose its match work just
    // because secretary tweaked timing on a group.

    // Scenario 2: secretary edits group's MaxRoundSecond / MaxTimeoutSecond.
    // Peer must see the new values on the group AND on its pending matches
    // (cascade). Bracket structure stays intact so completed matches are
    // untouched.
    [Fact]
    public void FieldsVersion_propagates_timing_and_cascades_to_pending_matches()
    {
        var (target, tgtGroup, _) = BuildPair();
        var (remote, remGroup, _) = Mirror(target);

        // Mark a match Completed locally so we can verify cascade only touches Pending ones.
        var pendingMatch = FirstPendingMatch(tgtGroup);
        var matchToFinish = tgtGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                                     .First(m => m.MatchNumber != pendingMatch.MatchNumber);
        matchToFinish.Status = MatchStatusEnum.Completed;
        var originalCompletedTiming = matchToFinish.MaxRoundSecond;

        remGroup.MaxRoundSecond = 240;
        remGroup.MaxTimeoutSecond = 45;
        remGroup.MaxActionSecond = 25;
        remGroup.FieldsVersion = 1;

        MakeImporter().Apply(target, ImportPlan.Proceed(remote));

        tgtGroup.MaxRoundSecond.Should().Be(240);
        tgtGroup.MaxTimeoutSecond.Should().Be(45);
        tgtGroup.MaxActionSecond.Should().Be(25);
        tgtGroup.FieldsVersion.Should().Be(1);
        tgtGroup.BracketVersion.Should().Be(1, "BracketVersion was not bumped on remote");

        // Cascade: pending matches got new timing.
        var localPending = tgtGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                                    .First(m => m.MatchNumber == pendingMatch.MatchNumber);
        localPending.MaxRoundSecond.Should().Be(240);
        localPending.MaxTimeoutSecond.Should().Be(45);

        // Completed match was left alone (its history is fixed).
        matchToFinish.MaxRoundSecond.Should().Be(originalCompletedTiming);
    }

    // Scenario 5: secretary moves group from carpet 1 to carpet 2. CarpetID is
    // a "field" (no bracket change) but membership in target.Carpets[].Groups
    // must update so each carpet's schedule view sees the right groups.
    [Fact]
    public void FieldsVersion_propagates_carpet_reassignment_and_updates_membership()
    {
        var (target, tgtGroup, _) = BuildPair();
        var carpet1 = new Carpet { ID = Guid.NewGuid(), Name = "Ковёр 1" };
        var carpet2 = new Carpet { ID = Guid.NewGuid(), Name = "Ковёр 2" };
        carpet1.Groups.Add(tgtGroup);
        tgtGroup.CarpetID = carpet1.ID;
        target.Carpets.Add(carpet1);
        target.Carpets.Add(carpet2);

        var (remote, remGroup, _) = Mirror(target);
        // Mirror clears CarpetID — re-set on both sides to model that local started on carpet 1
        // and remote moved to carpet 2.
        remGroup.CarpetID = carpet2.ID;
        remGroup.FieldsVersion = 1;

        MakeImporter().Apply(target, ImportPlan.Proceed(remote));

        tgtGroup.CarpetID.Should().Be(carpet2.ID);
        carpet1.Groups.Should().BeEmpty("group moved off carpet 1");
        carpet2.Groups.Should().ContainSingle(g => g.ID == tgtGroup.ID, "group landed on carpet 2");
    }

    // Scenario from grilling: secretary regenerated group X's bracket after
    // wrestler reassignment. Peer C had completed match #1 in group X before
    // the regen landed. Match identity (BracketFullNumber) is preserved across
    // regen for unaffected positions — match-Version preserves C's work.
    [Fact]
    public void BracketVersion_preserves_locally_newer_match_completions()
    {
        var (target, tgtGroup, tgtProc) = BuildPair();
        var localMatch = FirstPendingMatch(tgtGroup);
        var preservedKey = localMatch.BracketFullNumber;
        localMatch.IsRedWon = true;
        localMatch.WinType = MatchWinTypeEnum.PointsWin;
        localMatch.PointsRed = 6; localMatch.PointsBlue = 1;
        tgtProc.CompleteMatch(localMatch, true, MatchWinTypeEnum.PointsWin);
        localMatch.Version = 1;

        // Remote regenerated bracket (BracketVersion bumped) but secretary
        // had no idea about C's local completion → remote.match.Version=0.
        var (remote, remGroup, _) = Mirror(target);
        remGroup.BracketVersion = 2;

        MakeImporter().Apply(target, ImportPlan.Proceed(remote));

        tgtGroup.BracketVersion.Should().Be(2);
        var preserved = tgtGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                                .First(m => m.BracketFullNumber == preservedKey);
        preserved.Status.Should().Be(MatchStatusEnum.Completed);
        preserved.IsRedWon.Should().BeTrue();
        preserved.PointsRed.Should().Be(6);
        preserved.Version.Should().Be(1);
    }

    // FieldsVersion and BracketVersion are independent — bumping one must not
    // trigger the other's apply path. Specifically a timing change must NOT
    // touch local bracket / wipe match progress.
    [Fact]
    public void FieldsVersion_does_not_touch_bracket()
    {
        var (target, tgtGroup, tgtProc) = BuildPair();
        var localMatch = FirstPendingMatch(tgtGroup);
        tgtProc.CompleteMatch(localMatch, true, MatchWinTypeEnum.PointsWin);
        localMatch.Version = 1;
        var localBracketRef = tgtGroup.Bracket;
        var localWrestlersRef = tgtGroup.Wrestlers;

        var (remote, remGroup, _) = Mirror(target);
        remGroup.MaxRoundSecond = 999;
        remGroup.FieldsVersion = 1;
        // BracketVersion not bumped on remote.

        MakeImporter().Apply(target, ImportPlan.Proceed(remote));

        ReferenceEquals(tgtGroup.Bracket, localBracketRef).Should().BeTrue("FieldsVersion path must not replace bracket");
        ReferenceEquals(tgtGroup.Wrestlers, localWrestlersRef).Should().BeTrue("FieldsVersion path must not replace wrestlers list");
        localMatch.Status.Should().Be(MatchStatusEnum.Completed);
    }

    // New-wrestler propagation. Secretary created a duplicate of a wrestler
    // when transferring from group A (DQ in source) to group B. The new
    // wrestler with a fresh ID must land on peer's Tournament.Wrestlers.
    [Fact]
    public void New_wrestler_in_remote_is_added_to_target()
    {
        var (target, _, _) = BuildPair();
        var (remote, _, _) = Mirror(target);

        var newId = Guid.NewGuid();
        remote.Wrestlers.Add(new Wrestler
        {
            ID = newId,
            LastName = "ДубликатФамилия",
            FirstName = "Имя",
            BirthDate = new DateTime(2005, 1, 1),
            Weight = 60,
            IsEntryFeePaid = true,
            IsWeightApproved = true,
            SeedNumber = 5
        });

        var result = MakeImporter().Apply(target, ImportPlan.Proceed(remote));

        result.Outcome.Should().Be(ImportOutcome.Imported);
        target.Wrestlers.Should().ContainSingle(w => w.ID == newId);
    }

    // Group present locally but not in remote.Groups — Apply must skip
    // gracefully, not crash with NRE.
    [Fact]
    public void Group_missing_from_remote_is_skipped()
    {
        var (target, tgtGroup, _) = BuildPair();

        // Build a remote that has the wrestlers but no groups.
        var remote = new WTournament(new GlobalSettings()) { ID = target.ID, Name = target.Name };
        foreach (var w in target.Wrestlers) remote.Wrestlers.Add(w);

        Action act = () => MakeImporter().Apply(target, ImportPlan.Proceed(remote));
        act.Should().NotThrow();
        target.Groups.Should().ContainSingle(g => g.ID == tgtGroup.ID);
    }

    // Secretary added a brand-new weight category mid-tournament. Peer that
    // didn't know the group existed must absorb it on next import — group,
    // wrestlers, optional bracket and carpet membership all wired in.
    [Fact]
    public void New_group_in_remote_is_added_to_target()
    {
        var (target, _, _) = BuildPair();

        // Remote starts as a clone of target then gets a fresh group with
        // its own wrestlers and a regenerated bracket — i.e. fully populated.
        var (remote, _, _) = Mirror(target);

        var newGroupId = Guid.NewGuid();
        var newGroupWrestlers = new List<Wrestler>();
        for (int i = 0; i < 4; i++)
        {
            newGroupWrestlers.Add(new Wrestler
            {
                ID = Guid.NewGuid(),
                LastName = "NW" + i, FirstName = "F" + i,
                BirthDate = new DateTime(2008, 1, 1), Weight = 75,
                IsEntryFeePaid = true, IsWeightApproved = true,
                GroupID = newGroupId, SeedNumber = i + 1
            });
            remote.Wrestlers.Add(newGroupWrestlers[i]);
        }
        var newGroup = new AgeWeightGroup
        {
            ID = newGroupId,
            BirthYearMin = 2007, BirthYearMax = 2008, WeightMax = 75,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30,
            Wrestlers = newGroupWrestlers
        };
        remote.Groups.Add(newGroup);
        new OlympicGroupBracketProcessor().Generate(remote, newGroup);
        AssignSequentialMatchNumbers(newGroup);

        var result = MakeImporter().Apply(target, ImportPlan.Proceed(remote));

        result.Outcome.Should().Be(ImportOutcome.Imported);
        target.Groups.Should().Contain(g => g.ID == newGroupId);
        var addedGroup = target.Groups.First(g => g.ID == newGroupId);
        addedGroup.Wrestlers.Should().HaveCount(4);
        addedGroup.Bracket.Should().NotBeNull();
        // Wrestlers in the new group must be the SAME instances as those
        // added to target.Wrestlers — otherwise the live object graph splits
        // and UI bindings diverge.
        foreach (var wr in addedGroup.Wrestlers)
        {
            target.Wrestlers.Should().Contain(w => ReferenceEquals(w, wr));
        }
    }

    // Same flow but the new group lands assigned to a carpet that already
    // exists locally — membership must wire up.
    [Fact]
    public void New_group_with_carpet_id_joins_existing_carpet_membership()
    {
        var (target, _, _) = BuildPair();
        var carpet = new Carpet { ID = Guid.NewGuid(), Name = "Ковёр 2" };
        target.Carpets.Add(carpet);

        var (remote, _, _) = Mirror(target);
        var remoteCarpetCopy = new Carpet { ID = carpet.ID, Name = carpet.Name };
        remote.Carpets.Add(remoteCarpetCopy);

        var newGroupId = Guid.NewGuid();
        var newGroup = new AgeWeightGroup
        {
            ID = newGroupId,
            BirthYearMin = 2010, BirthYearMax = 2011, WeightMax = 50,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30,
            CarpetID = carpet.ID,
            Wrestlers = new List<Wrestler>()
        };
        remoteCarpetCopy.Groups.Add(newGroup);
        remote.Groups.Add(newGroup);

        MakeImporter().Apply(target, ImportPlan.Proceed(remote));

        target.Groups.Should().Contain(g => g.ID == newGroupId);
        carpet.Groups.Should().ContainSingle(g => g.ID == newGroupId);
    }

    // Secretary added a new carpet (fourth mat brought in mid-tournament).
    [Fact]
    public void New_carpet_in_remote_is_added_to_target()
    {
        var (target, _, _) = BuildPair();
        var (remote, _, _) = Mirror(target);

        var newCarpetId = Guid.NewGuid();
        remote.Carpets.Add(new Carpet { ID = newCarpetId, Name = "Ковёр 4" });

        var result = MakeImporter().Apply(target, ImportPlan.Proceed(remote));

        result.Outcome.Should().Be(ImportOutcome.Imported);
        target.Carpets.Should().Contain(c => c.ID == newCarpetId && c.Name == "Ковёр 4");
    }

    // Adding a new group to a carpet causes CarpetMatchNumbersGenerator to
    // renumber EVERY match on that carpet (not just the new group's). The
    // peer must perform the same renumbering locally after Apply, otherwise
    // its UI shows stale numbers on the existing groups and subsequent imports
    // can't find matches by MatchNumber.
    [Fact]
    public void New_group_on_carpet_renumbers_existing_matches_on_same_carpet()
    {
        var (target, tgtGroup, _) = BuildPair();
        var carpet = new Carpet { ID = Guid.NewGuid(), Name = "Ковёр 1" };
        carpet.Groups.Add(tgtGroup);
        tgtGroup.CarpetID = carpet.ID;
        target.Carpets.Add(carpet);

        // Renumber locally to baseline so we can detect a shift.
        new CarpetMatchNumbersGenerator().Generate(target,
            new List<IGroupBracketProcessor> { new OlympicGroupBracketProcessor() });
        var firstQualMatchKey = tgtGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                                         .First(m => m.RoundNumber == 1).BracketFullNumber;

        // Remote: clone target (with the same carpet+group), then add a brand-
        // new group on the same carpet and renumber the carpet there too.
        var (remote, remGroup, _) = Mirror(target);
        var remCarpet = new Carpet { ID = carpet.ID, Name = carpet.Name };
        remCarpet.Groups.Add(remGroup);
        remGroup.CarpetID = carpet.ID;
        remote.Carpets.Add(remCarpet);

        var newGroupId = Guid.NewGuid();
        var newWrestlers = new List<Wrestler>();
        for (int i = 0; i < 4; i++)
        {
            newWrestlers.Add(new Wrestler
            {
                ID = Guid.NewGuid(),
                LastName = "NW" + i, FirstName = "F" + i,
                BirthDate = new DateTime(2008, 1, 1), Weight = 75,
                IsEntryFeePaid = true, IsWeightApproved = true,
                GroupID = newGroupId, SeedNumber = i + 1
            });
            remote.Wrestlers.Add(newWrestlers[i]);
        }
        var newGroup = new AgeWeightGroup
        {
            ID = newGroupId, BirthYearMin = 2007, BirthYearMax = 2008, WeightMax = 75,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30,
            CarpetID = carpet.ID, Wrestlers = newWrestlers
        };
        remote.Groups.Add(newGroup);
        remCarpet.Groups.Add(newGroup);
        new OlympicGroupBracketProcessor().Generate(remote, newGroup);
        new CarpetMatchNumbersGenerator().Generate(remote,
            new List<IGroupBracketProcessor> { new OlympicGroupBracketProcessor() });

        var remoteFirstQualNumber = remGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                                            .First(m => m.BracketFullNumber == firstQualMatchKey)
                                            .MatchNumber;

        MakeImporter().Apply(target, ImportPlan.Proceed(remote));

        // After Apply, the same first-qual match on the existing group should
        // carry the same MatchNumber as on remote — i.e. the peer ran its own
        // CarpetMatchNumbersGenerator pass.
        var localFirstQual = tgtGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                                     .First(m => m.BracketFullNumber == firstQualMatchKey);
        localFirstQual.MatchNumber.Should().Be(remoteFirstQualNumber, "peer must renumber after structural change");
        target.Groups.Should().Contain(g => g.ID == newGroupId);
    }

    // -------------------------------------------------------------------------
    // End-to-end scenarios from the user's punch-list (TodoList #5).
    // -------------------------------------------------------------------------
    // These exercise the full secretary-on-laptop-A → import-on-laptop-B flow,
    // not single isolated paths. Each scenario:
    //   1. builds an initial state shared by both peers,
    //   2. clones it via the production adapter (= "save and load on peer B"),
    //   3. mutates the admin copy through the operations the UI exposes,
    //   4. runs Apply on the peer copy,
    //   5. asserts both copies are equivalent.

    // Scenario 1 — pre-tournament wrestler transfer.
    // No matches played anywhere. Admin notices a wrestler is in the wrong
    // group, removes them, adds them to the right group, regenerates both
    // brackets, saves. Peer B imports → identical to admin.
    [Fact]
    public void EndToEnd_pre_tournament_wrestler_transfer_results_in_equivalent_tournaments()
    {
        var (admin, g1, g2, _) = BuildTwoGroupTournament(g1Size: 4, g2Size: 4);
        var peer = CloneViaAdapter(admin);

        // Admin moves the first wrestler of G1 into G2.
        var transferred = g1.Wrestlers[0];
        g1.Wrestlers.Remove(transferred);
        g2.Wrestlers.Add(transferred);
        transferred.GroupID = g2.ID;

        // Both groups get bracket regen — Generate() bumps each group's
        // BracketVersion. CarpetMatchNumbersGenerator runs because the
        // operation also reshuffles the carpet's match queue.
        new OlympicGroupBracketProcessor().Generate(admin, g1);
        new OlympicGroupBracketProcessor().Generate(admin, g2);
        new CarpetMatchNumbersGenerator().Generate(admin,
            new List<IGroupBracketProcessor> { new OlympicGroupBracketProcessor() });

        var result = MakeImporter().Apply(peer, ImportPlan.Proceed(admin));

        result.Outcome.Should().Be(ImportOutcome.Imported);
        AssertEquivalent(admin, peer);

        // Sanity: the transferred wrestler now belongs to G2 on both sides.
        var peerG2 = peer.Groups.First(g => g.ID == g2.ID);
        peerG2.Wrestlers.Should().Contain(w => w.ID == transferred.ID);
        var peerG1 = peer.Groups.First(g => g.ID == g1.ID);
        peerG1.Wrestlers.Should().NotContain(w => w.ID == transferred.ID);
    }

    // Scenario 2 — mid-tournament transfer with matches already started in
    // the source group. The transferred wrestler X has unplayed matches in
    // G1; admin DQs X in G1 (cascade auto-completes their pending matches),
    // creates a copy of X with a fresh ID in G2, regenerates G2's bracket.
    // Peer B imports.
    //
    // Expected outcome on peer:
    //   - the new duplicate Wrestler with the fresh ID is on peer.Wrestlers
    //   - G2's bracket replaced (now includes the duplicate wrestler)
    //   - G1's pending matches with X are completed by DQ-cascade
    //   - G1's pre-existing already-completed match (other wrestlers) preserved
    //   - both copies equivalent
    [Fact]
    public void EndToEnd_mid_tournament_transfer_with_dq_in_source_results_in_equivalent_tournaments()
    {
        var (admin, g1, g2, _) = BuildTwoGroupTournament(g1Size: 4, g2Size: 3);
        var processor = new OlympicGroupBracketProcessor();

        // One existing completed match in G1 between two wrestlers other than
        // the one we'll transfer (so DQ cascade doesn't touch this match).
        var transferredWrestler = g1.Wrestlers[0];
        var unrelatedMatch = g1.Bracket.Rounds.SelectMany(r => r.RoundMatches)
            .First(m => m.WrestlerInRed != null && m.WrestlerInBlue != null
                        && m.WrestlerInRed.ID != transferredWrestler.ID
                        && m.WrestlerInBlue.ID != transferredWrestler.ID);
        processor.Load(admin, g1);
        processor.CompleteMatch(unrelatedMatch, true, MatchWinTypeEnum.PointsWin);
        unrelatedMatch.Version = 1;
        var unrelatedKey = unrelatedMatch.BracketFullNumber;

        var peer = CloneViaAdapter(admin);

        // Admin: DQ wrestler X in G1 — pick a pending match X is in and
        // mark them as the loser by DQ. The processor's CompleteMatch
        // cascades, auto-completing any other pending matches involving X.
        var dqMatch = g1.Bracket.Rounds.SelectMany(r => r.RoundMatches)
            .First(m => m.Status == MatchStatusEnum.Pending
                        && m.WrestlerInRed != null && m.WrestlerInBlue != null
                        && (m.WrestlerInRed.ID == transferredWrestler.ID
                            || m.WrestlerInBlue.ID == transferredWrestler.ID));
        bool xIsRed = dqMatch.WrestlerInRed.ID == transferredWrestler.ID;
        processor.Load(admin, g1);
        processor.CompleteMatch(dqMatch, !xIsRed, MatchWinTypeEnum.DisqualifyWin);
        dqMatch.Version = 1;   // single Approve in MatchResultsViewModel = single bump
        var dqKey = dqMatch.BracketFullNumber;

        // Admin: create the duplicate of X with a fresh ID, register in G2.
        var duplicateId = Guid.NewGuid();
        var duplicate = new Wrestler
        {
            ID = duplicateId,
            LastName = transferredWrestler.LastName,
            FirstName = transferredWrestler.FirstName,
            BirthDate = transferredWrestler.BirthDate,
            Weight = transferredWrestler.Weight,
            IsEntryFeePaid = true, IsWeightApproved = true,
            GroupID = g2.ID,
            SeedNumber = g2.Wrestlers.Count + 1
        };
        admin.Wrestlers.Add(duplicate);
        g2.Wrestlers.Add(duplicate);

        // Regenerate G2 bracket with the new participant + renumber carpet.
        processor.Generate(admin, g2);
        new CarpetMatchNumbersGenerator().Generate(admin,
            new List<IGroupBracketProcessor> { new OlympicGroupBracketProcessor() });

        var result = MakeImporter().Apply(peer, ImportPlan.Proceed(admin));

        result.Outcome.Should().Be(ImportOutcome.Imported);

        // The duplicate wrestler exists on peer with the fresh ID.
        peer.Wrestlers.Should().Contain(w => w.ID == duplicateId);

        // G2 on peer now includes the duplicate.
        var peerG2 = peer.Groups.First(g => g.ID == g2.ID);
        peerG2.Wrestlers.Should().Contain(w => w.ID == duplicateId);

        // G1 on peer: the unrelated completed match is preserved.
        var peerG1 = peer.Groups.First(g => g.ID == g1.ID);
        var peerUnrelated = peerG1.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                                  .First(m => m.BracketFullNumber == unrelatedKey);
        peerUnrelated.Status.Should().Be(MatchStatusEnum.Completed);
        peerUnrelated.Version.Should().Be(1);

        // G1 on peer: the DQ match has the DQ recorded.
        var peerDq = peerG1.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                           .First(m => m.BracketFullNumber == dqKey);
        peerDq.Status.Should().Be(MatchStatusEnum.Completed);
        peerDq.WinType.Should().Be(MatchWinTypeEnum.DisqualifyWin);
        peerDq.Version.Should().Be(1);

        // X (original ID) still belongs to G1, not transferred at the entity
        // level (the workflow creates a duplicate instead).
        peer.Wrestlers.First(w => w.ID == transferredWrestler.ID).GroupID.Should().Be(g1.ID);

        // Whole-tournament equivalence — both copies converge.
        AssertEquivalent(admin, peer);
    }

    // Scenario 3 — secretary moves group between carpets mid-tournament
    // (TodoList #10). Tracks CarpetID change + carpet membership swap +
    // peer-side renumbering.
    [Fact]
    public void EndToEnd_carpet_move_results_in_equivalent_tournaments()
    {
        var (admin, g1, _, carpet1) = BuildTwoGroupTournament(g1Size: 4, g2Size: 4);
        var carpet2 = new Carpet { ID = Guid.NewGuid(), Name = "Ковёр 2" };
        admin.Carpets.Add(carpet2);

        var peer = CloneViaAdapter(admin);

        // Mimics CarpetsViewModel.UnbindGroup + BindGroup sequence on admin.
        carpet1.Groups.Remove(g1);
        g1.CarpetID = carpet2.ID;
        g1.CarpetLabel = carpet2.Name;
        g1.FieldsVersion++;
        carpet2.Groups.Add(g1);
        new CarpetMatchNumbersGenerator().Generate(admin,
            new List<IGroupBracketProcessor> { new OlympicGroupBracketProcessor() });

        var result = MakeImporter().Apply(peer, ImportPlan.Proceed(admin));

        result.Outcome.Should().Be(ImportOutcome.Imported);
        var peerG1 = peer.Groups.First(g => g.ID == g1.ID);
        peerG1.CarpetID.Should().Be(carpet2.ID);
        peer.Carpets.First(c => c.ID == carpet1.ID).Groups.Should().NotContain(g => g.ID == g1.ID);
        peer.Carpets.First(c => c.ID == carpet2.ID).Groups.Should().Contain(g => g.ID == g1.ID);
        AssertEquivalent(admin, peer);
    }

    // Scenario 4 — secretary changes group's round timing (TodoList #1
    // partial). Cascade hits pending matches; completed matches keep their
    // historical timing; bracket structure untouched.
    [Fact]
    public void EndToEnd_round_duration_change_cascades_to_pending_and_replicates()
    {
        var (admin, g1, _, _) = BuildTwoGroupTournament(g1Size: 4, g2Size: 4);

        // Complete one match in G1 BEFORE the timing change to verify cascade
        // doesn't touch its historical MaxRoundSecond.
        var processor = new OlympicGroupBracketProcessor();
        var completedMatch = g1.Bracket.Rounds.SelectMany(r => r.RoundMatches)
            .First(m => m.WrestlerInRed != null && m.WrestlerInBlue != null);
        processor.Load(admin, g1);
        processor.CompleteMatch(completedMatch, true, MatchWinTypeEnum.PointsWin);
        completedMatch.Version = 1;
        var completedKey = completedMatch.BracketFullNumber;
        int oldTiming = completedMatch.MaxRoundSecond;

        var peer = CloneViaAdapter(admin);

        // Admin: edit group timing + cascade locally + bump FieldsVersion
        // (mimics DetailsViewModel.EditGroup → ApplyTimingsToPendingMatches).
        g1.MaxRoundSecond = 240;
        g1.MaxTimeoutSecond = 45;
        foreach (var m in g1.Bracket.Rounds.SelectMany(r => r.RoundMatches))
        {
            if (m.Status == MatchStatusEnum.Completed) continue;
            m.MaxRoundSecond = 240;
            m.MaxTimeoutSecond = 45;
        }
        g1.FieldsVersion++;

        var result = MakeImporter().Apply(peer, ImportPlan.Proceed(admin));

        result.Outcome.Should().Be(ImportOutcome.Imported);
        var peerG1 = peer.Groups.First(g => g.ID == g1.ID);
        peerG1.MaxRoundSecond.Should().Be(240);
        peerG1.Bracket.Rounds.SelectMany(r => r.RoundMatches)
              .Where(m => m.Status == MatchStatusEnum.Pending)
              .Should().OnlyContain(m => m.MaxRoundSecond == 240);
        peerG1.Bracket.Rounds.SelectMany(r => r.RoundMatches)
              .First(m => m.BracketFullNumber == completedKey)
              .MaxRoundSecond.Should().Be(oldTiming, "completed match keeps historical timing");
        AssertEquivalent(admin, peer);
    }

    // Scenario 5 — parallel changes. Admin restructures G1 while peer C
    // independently completes a match in G2. Apply admin → peer must merge
    // both: G1 takes admin's structure, G2 keeps peer's local completion.
    [Fact]
    public void EndToEnd_parallel_changes_preserve_each_peers_work()
    {
        var (admin, g1, g2, _) = BuildTwoGroupTournament(g1Size: 4, g2Size: 4);
        var peer = CloneViaAdapter(admin);

        // Admin regenerates G1 — bumps BracketVersion. No knowledge of peer.
        new OlympicGroupBracketProcessor().Generate(admin, g1);

        // Peer completes a match in G2 — bumps WrestlingMatch.Version. No
        // knowledge of admin's G1 work.
        var peerG2 = peer.Groups.First(g => g.ID == g2.ID);
        var peerProcessor = new OlympicGroupBracketProcessor();
        peerProcessor.Load(peer, peerG2);
        var peerG2Match = peerG2.Bracket.Rounds.SelectMany(r => r.RoundMatches).First();
        peerProcessor.CompleteMatch(peerG2Match, true, MatchWinTypeEnum.PointsWin);
        peerG2Match.Version = 1;
        var peerG2Key = peerG2Match.BracketFullNumber;

        var result = MakeImporter().Apply(peer, ImportPlan.Proceed(admin));

        result.Outcome.Should().Be(ImportOutcome.Imported);
        // Admin's G1 BracketVersion bump took effect on peer.
        peer.Groups.First(g => g.ID == g1.ID).BracketVersion.Should().Be(g1.BracketVersion);
        // Peer's G2 completion survived.
        var preserved = peerG2.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                              .First(m => m.BracketFullNumber == peerG2Key);
        preserved.Status.Should().Be(MatchStatusEnum.Completed);
        preserved.Version.Should().Be(1);
    }

    // Scenario 6 — multiple structural ops bundled in a single push.
    // Admin adds a new group G3, transfers a wrestler from G1 to G3,
    // regenerates G1 (without the transferred wrestler) and G3 (with them).
    [Fact]
    public void EndToEnd_combined_new_group_and_wrestler_transfer_replicates()
    {
        var (admin, g1, _, carpet1) = BuildTwoGroupTournament(g1Size: 5, g2Size: 4);
        var peer = CloneViaAdapter(admin);

        // Create G3 with one initial wrestler so it can host a bracket.
        var g3Id = Guid.NewGuid();
        var seed = MakeWrestler(g3Id, "G3-seed", 1);
        admin.Wrestlers.Add(seed);
        var g3 = new AgeWeightGroup
        {
            ID = g3Id, BirthYearMin = 2007, BirthYearMax = 2008, WeightMax = 65,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30,
            CarpetID = carpet1.ID, Wrestlers = new List<Wrestler> { seed }
        };
        admin.Groups.Add(g3);
        carpet1.Groups.Add(g3);

        // Transfer wrestler[0] from G1 → G3.
        var transferred = g1.Wrestlers[0];
        g1.Wrestlers.Remove(transferred);
        g3.Wrestlers.Add(transferred);
        transferred.GroupID = g3Id;
        transferred.SeedNumber = 2;

        new OlympicGroupBracketProcessor().Generate(admin, g1);
        new OlympicGroupBracketProcessor().Generate(admin, g3);
        new CarpetMatchNumbersGenerator().Generate(admin,
            new List<IGroupBracketProcessor> { new OlympicGroupBracketProcessor() });

        MakeImporter().Apply(peer, ImportPlan.Proceed(admin));

        AssertEquivalent(admin, peer);
        peer.Groups.Should().Contain(g => g.ID == g3Id);
        var peerG3 = peer.Groups.First(g => g.ID == g3Id);
        peerG3.Wrestlers.Should().Contain(w => w.ID == transferred.ID);
        peer.Groups.First(g => g.ID == g1.ID).Wrestlers.Should().NotContain(w => w.ID == transferred.ID);
    }

    // Scenario 7 — peer was offline for an hour while admin made many edits
    // of different shapes. On reconnect, single Apply consumes all of them.
    [Fact]
    public void EndToEnd_offline_peer_catches_up_multiple_changes_in_single_apply()
    {
        var (admin, g1, g2, carpet1) = BuildTwoGroupTournament(g1Size: 4, g2Size: 4);
        var peer = CloneViaAdapter(admin);

        // 1. FieldsVersion bump on G1 (timing edit).
        g1.MaxRoundSecond = 200;
        foreach (var m in g1.Bracket.Rounds.SelectMany(r => r.RoundMatches))
            if (m.Status != MatchStatusEnum.Completed) m.MaxRoundSecond = 200;
        g1.FieldsVersion++;

        // 2. BracketVersion bump on G2 (regeneration).
        new OlympicGroupBracketProcessor().Generate(admin, g2);

        // 3. New group G3 with its own wrestlers and bracket.
        var g3Id = Guid.NewGuid();
        var g3Wrestlers = new List<Wrestler>();
        for (int i = 0; i < 4; i++)
        {
            g3Wrestlers.Add(MakeWrestler(g3Id, "G3-" + i, i + 1));
            admin.Wrestlers.Add(g3Wrestlers[i]);
        }
        var g3 = new AgeWeightGroup
        {
            ID = g3Id, BirthYearMin = 2009, BirthYearMax = 2010, WeightMax = 50,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30,
            CarpetID = carpet1.ID, Wrestlers = g3Wrestlers
        };
        admin.Groups.Add(g3);
        carpet1.Groups.Add(g3);
        new OlympicGroupBracketProcessor().Generate(admin, g3);

        // 4. New carpet.
        var carpet2 = new Carpet { ID = Guid.NewGuid(), Name = "Ковёр 2" };
        admin.Carpets.Add(carpet2);

        // 5. Move G1 onto the new carpet.
        carpet1.Groups.Remove(g1);
        g1.CarpetID = carpet2.ID;
        g1.CarpetLabel = carpet2.Name;
        carpet2.Groups.Add(g1);
        g1.FieldsVersion++;

        new CarpetMatchNumbersGenerator().Generate(admin,
            new List<IGroupBracketProcessor> { new OlympicGroupBracketProcessor() });

        var result = MakeImporter().Apply(peer, ImportPlan.Proceed(admin));

        result.Outcome.Should().Be(ImportOutcome.Imported);
        AssertEquivalent(admin, peer);
    }

    // Scenario 8 — versions survive a round-trip through the adapter (DTO).
    // Regression guard: if FieldsVersion / BracketVersion ever get dropped
    // from EntityToInfoAdapter, this test catches it before the next .wrt
    // save+reload silently zeros them on a peer.
    [Fact]
    public void Group_versions_survive_round_trip_through_adapter()
    {
        var (t, g1, g2, _) = BuildTwoGroupTournament(g1Size: 4, g2Size: 4);
        g1.FieldsVersion = 5;
        g1.BracketVersion = 7;
        g2.FieldsVersion = 3;
        g2.BracketVersion = 4;

        var roundtripped = CloneViaAdapter(t);

        var rg1 = roundtripped.Groups.First(g => g.ID == g1.ID);
        rg1.FieldsVersion.Should().Be(5);
        rg1.BracketVersion.Should().Be(7);
        var rg2 = roundtripped.Groups.First(g => g.ID == g2.ID);
        rg2.FieldsVersion.Should().Be(3);
        rg2.BracketVersion.Should().Be(4);
    }

    // Wrestler-pair safety: same BracketFullNumber after a bracket regen
    // does not mean the position carries the same opponents. If admin
    // re-seeded or swapped a wrestler in, re-applying a local-newer result
    // would credit A's prior win against B to a new match A-vs-X. Importer
    // must drop the old completion in that case.
    [Fact]
    public void BracketVersion_does_not_re_apply_completion_when_wrestler_pair_differs()
    {
        var (target, tgtGroup, tgtProc) = BuildPair();

        // Local: complete a match. Capture its wrestler pair and key.
        var localMatch = FirstPendingMatch(tgtGroup);
        var localKey = localMatch.BracketFullNumber;
        var originalRedId = localMatch.WrestlerInRed.ID;
        var originalBlueId = localMatch.WrestlerInBlue.ID;
        localMatch.IsRedWon = true;
        localMatch.WinType = MatchWinTypeEnum.PointsWin;
        localMatch.PointsRed = 5; localMatch.PointsBlue = 0;
        tgtProc.CompleteMatch(localMatch, true, MatchWinTypeEnum.PointsWin);
        localMatch.Version = 1;

        // Remote: bracket regenerated with a different wrestler in the same
        // position (admin swapped the original blue wrestler for a new one).
        var (remote, remGroup, _) = Mirror(target);
        var remMatch = remGroup.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                               .First(m => m.BracketFullNumber == localKey);
        // Substitute the blue wrestler with a new one (different ID).
        var newWrestler = new Wrestler
        {
            ID = Guid.NewGuid(), LastName = "Swap", FirstName = "F",
            BirthDate = new DateTime(2005, 1, 1), Weight = 60,
            IsEntryFeePaid = true, IsWeightApproved = true,
            GroupID = remGroup.ID, SeedNumber = 99
        };
        remote.Wrestlers.Add(newWrestler);
        remGroup.Wrestlers[remGroup.Wrestlers.IndexOf(
            remGroup.Wrestlers.First(w => w.ID == originalBlueId))] = newWrestler;
        remMatch.WrestlerInBlue = newWrestler;
        remGroup.BracketVersion = 2;

        MakeImporter().Apply(target, ImportPlan.Proceed(remote));

        // Position survived (BracketFullNumber matched), but the local
        // completion must NOT have been re-applied because the opponent
        // changed. The match should be Pending in the new bracket — drop the
        // result, leave the new pairing untouched.
        var preserved = target.Groups[0].Bracket.Rounds.SelectMany(r => r.RoundMatches)
                              .First(m => m.BracketFullNumber == localKey);
        preserved.Status.Should().Be(MatchStatusEnum.Pending,
            "old completion vs original blue wrestler must not transfer to a match against a different opponent");
        preserved.WrestlerInBlue.ID.Should().Be(newWrestler.ID);
        preserved.WrestlerInRed.ID.Should().Be(originalRedId);
    }

    // Conversely: same wrestler pair after regen — local-newer completion
    // IS re-applied (just a re-seed that didn't change the actual matchup
    // at this position).
    [Fact]
    public void BracketVersion_re_applies_completion_when_wrestler_pair_matches()
    {
        var (target, tgtGroup, tgtProc) = BuildPair();

        var localMatch = FirstPendingMatch(tgtGroup);
        var localKey = localMatch.BracketFullNumber;
        localMatch.IsRedWon = true;
        localMatch.WinType = MatchWinTypeEnum.PointsWin;
        localMatch.PointsRed = 7; localMatch.PointsBlue = 1;
        tgtProc.CompleteMatch(localMatch, true, MatchWinTypeEnum.PointsWin);
        localMatch.Version = 1;

        // Remote: bracket "regenerated" but with the SAME wrestler pair at
        // the same position. (BracketVersion bumped — we test the apply path.)
        var (remote, remGroup, _) = Mirror(target);
        remGroup.BracketVersion = 2;

        MakeImporter().Apply(target, ImportPlan.Proceed(remote));

        var preserved = target.Groups[0].Bracket.Rounds.SelectMany(r => r.RoundMatches)
                              .First(m => m.BracketFullNumber == localKey);
        preserved.Status.Should().Be(MatchStatusEnum.Completed,
            "same wrestler pair → completion stays valid");
        preserved.PointsRed.Should().Be(7);
        preserved.Version.Should().Be(1);
    }

    // Stale FieldsVersion / BracketVersion (remote behind local) must not
    // overwrite local state.
    [Fact]
    public void Stale_field_and_bracket_versions_are_ignored()
    {
        var (target, tgtGroup, _) = BuildPair();
        tgtGroup.FieldsVersion = 5;
        tgtGroup.BracketVersion = 5;
        tgtGroup.MaxRoundSecond = 300;

        var (remote, remGroup, _) = Mirror(target);
        remGroup.FieldsVersion = 2;
        remGroup.BracketVersion = 2;
        remGroup.MaxRoundSecond = 60;   // would clobber if FieldsVersion path fired

        MakeImporter().Apply(target, ImportPlan.Proceed(remote));

        tgtGroup.MaxRoundSecond.Should().Be(300);
        tgtGroup.FieldsVersion.Should().Be(5);
        tgtGroup.BracketVersion.Should().Be(5);
    }
}
