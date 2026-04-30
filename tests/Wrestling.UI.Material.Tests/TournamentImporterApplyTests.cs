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
            new List<IGroupBracketProcessor> { new OlympicGroupBracketProcessor() });

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
}
