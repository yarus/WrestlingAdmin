using System.Linq;
using FluentAssertions;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Entities.Tests;

// Comprehensive coverage for the mutual-DSQ feature (#12 in TodoList).
// UWW rules cited verbatim in CLAUDE.md / TodoList. These tests validate:
//   M1 — mutual DSQ in a non-SF/non-F round of an Olympic bracket: both
//        wrestlers DSQ'd, sibling-match winner gets auto-FreeWin in next.
//   M2 — mutual DSQ in semifinal: cascade to consolation is suppressed
//        (manual rebuild per UWW). Match itself completes normally.
//   M3 — mutual DSQ in final: no advancement; both DSQ'd, no rank.
//   M4 — round-robin mutual DSQ: both wrestlers' remaining pending matches
//        cascade to DisqualifyWin for opponents.
//   FinalPlace/IsDisqualified semantics and revert.
public class MutualDisqualifyTests
{
    // ---------- M1: Olympic, mutual DSQ in QF ----------

    [Fact]
    public void Olympic8_MutualDsq_in_QF_marks_both_disqualified_and_no_winner()
    {
        var (_, g, proc) = SetupOlympic(8);
        var r1 = g.Bracket.Rounds[0]; // QF
        var qf = r1.RoundMatches[0];
        var red = qf.WrestlerInRed;
        var blue = qf.WrestlerInBlue;

        proc.CompleteMatch(qf, null, MatchWinTypeEnum.MutualDisqualify);

        qf.Status.Should().Be(MatchStatusEnum.Completed);
        qf.IsRedWon.Should().BeNull();
        qf.WinType.Should().Be(MatchWinTypeEnum.MutualDisqualify);
        red!.IsDisqualified.Should().BeTrue();
        blue!.IsDisqualified.Should().BeTrue();
    }

    [Fact]
    public void Olympic8_MutualDsq_in_QF_when_sibling_already_completed_auto_FreeWin_in_SF()
    {
        var (_, g, proc) = SetupOlympic(8);
        var qf1 = g.Bracket.Rounds[0].RoundMatches[0];
        var qf2 = g.Bracket.Rounds[0].RoundMatches[1];

        // Complete qf2 first with a normal winner
        proc.CompleteMatch(qf2, true, MatchWinTypeEnum.PointsWin);
        var qf2Winner = qf2.WrestlerInRed;

        // Now mutual DSQ in qf1 — sibling is qf2 (completed).
        // The SF that qf1 and qf2 feed into should auto-FreeWin for qf2's winner.
        proc.CompleteMatch(qf1, null, MatchWinTypeEnum.MutualDisqualify);

        var sf = g.Bracket.Rounds[1].RoundMatches[0];
        sf.Status.Should().Be(MatchStatusEnum.Completed);
        sf.WinType.Should().Be(MatchWinTypeEnum.FreeWin);
        var sfWinner = sf.IsRedWon!.Value ? sf.WrestlerInRed : sf.WrestlerInBlue;
        sfWinner.Should().Be(qf2Winner);
    }

    [Fact]
    public void Olympic8_MutualDsq_in_QF_when_sibling_pending_does_not_complete_SF_yet()
    {
        var (_, g, proc) = SetupOlympic(8);
        var qf1 = g.Bracket.Rounds[0].RoundMatches[0];

        // Mutual DSQ in qf1 first, sibling qf2 still pending
        proc.CompleteMatch(qf1, null, MatchWinTypeEnum.MutualDisqualify);

        var sf = g.Bracket.Rounds[1].RoundMatches[0];
        sf.Status.Should().Be(MatchStatusEnum.Pending);
    }

    [Fact]
    public void Olympic8_MutualDsq_in_QF_then_sibling_completes_triggers_auto_FreeWin_in_SF()
    {
        var (_, g, proc) = SetupOlympic(8);
        var qf1 = g.Bracket.Rounds[0].RoundMatches[0];
        var qf2 = g.Bracket.Rounds[0].RoundMatches[1];

        // Mutual DSQ first
        proc.CompleteMatch(qf1, null, MatchWinTypeEnum.MutualDisqualify);

        // Sibling completes after — its ProceedToNextMatch should detect
        // sibling=mutual and auto-FreeWin SF for qf2's winner.
        proc.CompleteMatch(qf2, true, MatchWinTypeEnum.PointsWin);
        var qf2Winner = qf2.WrestlerInRed;

        var sf = g.Bracket.Rounds[1].RoundMatches[0];
        sf.Status.Should().Be(MatchStatusEnum.Completed);
        sf.WinType.Should().Be(MatchWinTypeEnum.FreeWin);
        var sfWinner = sf.IsRedWon!.Value ? sf.WrestlerInRed : sf.WrestlerInBlue;
        sfWinner.Should().Be(qf2Winner);
    }

    [Fact]
    public void Olympic8_MutualDsq_FreeWin_propagates_through_subsequent_rounds()
    {
        var (_, g, proc) = SetupOlympic(8);
        var qf1 = g.Bracket.Rounds[0].RoundMatches[0];
        var qf2 = g.Bracket.Rounds[0].RoundMatches[1];

        proc.CompleteMatch(qf1, null, MatchWinTypeEnum.MutualDisqualify);
        proc.CompleteMatch(qf2, true, MatchWinTypeEnum.PointsWin);
        // SF is now completed via FreeWin; qf2's winner should be in the final slot.
        var sf = g.Bracket.Rounds[1].RoundMatches[0];
        var finalMatch = g.Bracket.Rounds[2].RoundMatches[0];

        var sfWinner = sf.IsRedWon!.Value ? sf.WrestlerInRed : sf.WrestlerInBlue;
        new[] { finalMatch.WrestlerInRed, finalMatch.WrestlerInBlue }.Should().Contain(sfWinner);
    }

    // ---------- M2: Mutual DSQ in semifinal — alert path ----------

    [Fact]
    public void Olympic4_MutualDsq_in_SF_does_not_propagate_to_final()
    {
        var (_, g, proc) = SetupOlympic(4);
        var sf = g.Bracket.Rounds[0].RoundMatches[0]; // SF
        proc.CompleteMatch(sf, null, MatchWinTypeEnum.MutualDisqualify);

        var finalMatch = g.Bracket.Rounds[1].RoundMatches[0];
        // No wrestler from this SF should be in the final.
        new[] { finalMatch.WrestlerInRed, finalMatch.WrestlerInBlue }
            .Should().NotContain(sf.WrestlerInRed);
        new[] { finalMatch.WrestlerInRed, finalMatch.WrestlerInBlue }
            .Should().NotContain(sf.WrestlerInBlue);
    }

    [Fact]
    public void Olympic4_MutualDsq_in_SF_does_not_populate_third_place_match()
    {
        var (_, g, proc) = SetupOlympic(4);
        var sf = g.Bracket.Rounds[0].RoundMatches[0];
        proc.CompleteMatch(sf, null, MatchWinTypeEnum.MutualDisqualify);

        var thirdPlace = g.Bracket.Rounds.Single(r => r.RoundType == GroupRoundTypeEnum.Additional).RoundMatches[0];
        // Per M2, consolation rebuild is manual — no auto-fill from mutual SF.
        new[] { thirdPlace.WrestlerInRed, thirdPlace.WrestlerInBlue }
            .Should().NotContain(sf.WrestlerInRed);
        new[] { thirdPlace.WrestlerInRed, thirdPlace.WrestlerInBlue }
            .Should().NotContain(sf.WrestlerInBlue);
    }

    [Fact]
    public void Olympic4_MutualDsq_in_SF_does_not_auto_FreeWin_third_place_when_other_SF_completes()
    {
        var (_, g, proc) = SetupOlympic(4);
        var sf1 = g.Bracket.Rounds[0].RoundMatches[0];
        var sf2 = g.Bracket.Rounds[0].RoundMatches[1];

        proc.CompleteMatch(sf1, null, MatchWinTypeEnum.MutualDisqualify);
        proc.CompleteMatch(sf2, true, MatchWinTypeEnum.PointsWin);

        var thirdPlace = g.Bracket.Rounds.Single(r => r.RoundType == GroupRoundTypeEnum.Additional).RoundMatches[0];
        thirdPlace.Status.Should().NotBe(MatchStatusEnum.Completed,
            because: "M2: third-place auto-FreeWin must be suppressed when an SF was mutual DSQ");
    }

    // ---------- M3: Mutual DSQ in final ----------

    [Fact]
    public void Olympic4_MutualDsq_in_Final_marks_both_disqualified_and_no_rank()
    {
        var (_, g, proc) = SetupOlympic(4);
        // Drive both SFs to completion first
        proc.CompleteMatch(g.Bracket.Rounds[0].RoundMatches[0], true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(g.Bracket.Rounds[0].RoundMatches[1], true, MatchWinTypeEnum.PointsWin);

        var finalMatch = g.Bracket.Rounds[1].RoundMatches[0];
        var finalists = new[] { finalMatch.WrestlerInRed, finalMatch.WrestlerInBlue };

        proc.CompleteMatch(finalMatch, null, MatchWinTypeEnum.MutualDisqualify);

        finalists[0]!.IsDisqualified.Should().BeTrue();
        finalists[1]!.IsDisqualified.Should().BeTrue();

        proc.GetResults();
        finalists[0]!.FinalPlace.Should().BeNull("DSQ wrestler stays «без места»");
        finalists[1]!.FinalPlace.Should().BeNull();
    }

    // ---------- M4: Round-robin ----------

    [Fact]
    public void RoundRobin_MutualDsq_cascades_DisqualifyWin_to_both_wrestlers_other_matches()
    {
        var group = TestHelpers.MakeGroup(4);
        var t = TestHelpers.MakeTournament(group);
        var proc = new RoundRobinGroupBracketProcessor();
        proc.Generate(t, group);

        var allMatches = group.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();
        // Pick A vs B as the mutual DSQ pair
        var mutualMatch = allMatches[0];
        var a = mutualMatch.WrestlerInRed;
        var b = mutualMatch.WrestlerInBlue;

        proc.CompleteMatch(mutualMatch, null, MatchWinTypeEnum.MutualDisqualify);

        // All other matches involving A or B should be auto-completed as DisqualifyWin
        var aOrBMatches = allMatches
            .Where(m => m != mutualMatch && (m.WrestlerInRed!.SameAs(a) || m.WrestlerInBlue!.SameAs(a)
                                            || m.WrestlerInRed.SameAs(b) || m.WrestlerInBlue.SameAs(b)))
            .ToList();
        aOrBMatches.Should().NotBeEmpty();
        aOrBMatches.Should().OnlyContain(m => m.Status == MatchStatusEnum.Completed && m.WinType == MatchWinTypeEnum.DisqualifyWin);
    }

    [Fact]
    public void RoundRobin_MutualDsq_other_wrestlers_not_involved_keep_original_pending_status_for_their_pair()
    {
        // 4 wrestlers: A, B, C, D. Mutual DSQ A-B. C vs D should be unaffected.
        var group = TestHelpers.MakeGroup(4);
        var t = TestHelpers.MakeTournament(group);
        var proc = new RoundRobinGroupBracketProcessor();
        proc.Generate(t, group);

        var allMatches = group.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();
        var mutualMatch = allMatches[0];
        var a = mutualMatch.WrestlerInRed;
        var b = mutualMatch.WrestlerInBlue;

        proc.CompleteMatch(mutualMatch, null, MatchWinTypeEnum.MutualDisqualify);

        var cdMatch = allMatches.FirstOrDefault(m => m != mutualMatch
            && !m.WrestlerInRed!.SameAs(a) && !m.WrestlerInBlue!.SameAs(a)
            && !m.WrestlerInRed.SameAs(b) && !m.WrestlerInBlue.SameAs(b));
        cdMatch.Should().NotBeNull("4-wrestler round-robin has C vs D pair");
        cdMatch!.Status.Should().Be(MatchStatusEnum.Pending);
    }

    [Fact]
    public void RoundRobin_MutualDsq_DSQ_wrestlers_get_FinalPlace_null_after_recalc()
    {
        var group = TestHelpers.MakeGroup(4);
        var t = TestHelpers.MakeTournament(group);
        var proc = new RoundRobinGroupBracketProcessor();
        proc.Generate(t, group);

        var allMatches = group.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();
        var mutualMatch = allMatches[0];
        var a = mutualMatch.WrestlerInRed;
        var b = mutualMatch.WrestlerInBlue;
        var cdMatch = allMatches.FirstOrDefault(m => m != mutualMatch
            && !m.WrestlerInRed!.SameAs(a) && !m.WrestlerInBlue!.SameAs(a)
            && !m.WrestlerInRed.SameAs(b) && !m.WrestlerInBlue.SameAs(b));

        proc.CompleteMatch(mutualMatch, null, MatchWinTypeEnum.MutualDisqualify);
        // Complete C vs D normally so the round-robin is fully resolved
        proc.CompleteMatch(cdMatch!, true, MatchWinTypeEnum.PointsWin);

        proc.GetResults();

        a!.FinalPlace.Should().BeNull();
        b!.FinalPlace.Should().BeNull();
        // C and D should have a place
        var cd = group.Wrestlers.Where(w => !w.IsDisqualified).ToList();
        cd.Should().HaveCount(2);
        cd.Should().OnlyContain(w => w.FinalPlace.HasValue);
    }

    // ---------- TournamentResult.Wins/Losses respect mutual DSQ ----------

    [Fact]
    public void TournamentResult_Wins_does_not_count_mutual_DSQ_match()
    {
        var group = TestHelpers.MakeGroup(2);
        var t = TestHelpers.MakeTournament(group);
        // 2-wrestler round-robin = single match
        var proc = new RoundRobinGroupBracketProcessor();
        proc.Generate(t, group);

        var match = group.Bracket.Rounds[0].RoundMatches[0];
        var a = match.WrestlerInRed;

        proc.CompleteMatch(match, null, MatchWinTypeEnum.MutualDisqualify);

        var stat = new Wrestling.Entities.Results.TournamentResult(group, a);
        stat.Wins.Should().Be(0, "mutual DSQ has no winner");
        stat.Loses.Should().Be(0, "mutual DSQ has no loser either");
    }

    // ---------- Revert ----------

    [Fact]
    public void Revert_clears_IsDisqualified_for_both_wrestlers()
    {
        var group = TestHelpers.MakeGroup(2);
        var t = TestHelpers.MakeTournament(group);
        var proc = new RoundRobinGroupBracketProcessor();
        proc.Generate(t, group);

        var match = group.Bracket.Rounds[0].RoundMatches[0];
        var a = match.WrestlerInRed;
        var b = match.WrestlerInBlue;

        proc.CompleteMatch(match, null, MatchWinTypeEnum.MutualDisqualify);
        a!.IsDisqualified.Should().BeTrue();
        b!.IsDisqualified.Should().BeTrue();

        proc.RevertMatch(match);
        a.IsDisqualified.Should().BeFalse();
        b.IsDisqualified.Should().BeFalse();
        match.Status.Should().Be(MatchStatusEnum.Pending);
        match.WinType.Should().BeNull();
        match.IsRedWon.Should().BeNull();
    }

    [Fact]
    public void Olympic_MutualDsq_blocks_revert_when_FreeWin_already_propagated()
    {
        var (_, g, proc) = SetupOlympic(8);
        var qf1 = g.Bracket.Rounds[0].RoundMatches[0];
        var qf2 = g.Bracket.Rounds[0].RoundMatches[1];

        // sibling completes first (normal win)
        proc.CompleteMatch(qf2, true, MatchWinTypeEnum.PointsWin);
        // mutual DSQ in qf1 — triggers SF auto-FreeWin
        proc.CompleteMatch(qf1, null, MatchWinTypeEnum.MutualDisqualify);

        // SF now Completed → can't revert qf1 directly.
        proc.CanMatchBeReverted(qf1).Should().BeFalse(
            "SF (next-match) was auto-completed via FreeWin — operator must revert it first");
    }

    // ---------- IsDisqualified flag default and round-trip ----------

    [Fact]
    public void IsDisqualified_default_false_on_new_wrestler()
    {
        var w = new Wrestler();
        w.IsDisqualified.Should().BeFalse();
    }

    [Fact]
    public void Sync_copies_IsDisqualified()
    {
        var src = new Wrestler { ID = System.Guid.NewGuid(), IsDisqualified = true, LastName = "X", FirstName = "Y" };
        var dst = new Wrestler();
        dst.Sync(src);
        dst.IsDisqualified.Should().BeTrue();
    }

    [Fact]
    public void Clone_copies_IsDisqualified()
    {
        var src = new Wrestler { ID = System.Guid.NewGuid(), IsDisqualified = true, LastName = "X", FirstName = "Y" };
        var copy = (Wrestler)src.Clone();
        copy.IsDisqualified.Should().BeTrue();
    }

    // ---------- IsMutualDisqualify property ----------

    [Fact]
    public void IsMutualDisqualify_is_true_only_when_WinType_is_MutualDisqualify()
    {
        var m = new WrestlingMatch { WinType = MatchWinTypeEnum.MutualDisqualify };
        m.IsMutualDisqualify.Should().BeTrue();

        m.WinType = MatchWinTypeEnum.DisqualifyWin;
        m.IsMutualDisqualify.Should().BeFalse();
    }

    // ---------- OlympicWithConsolation: smoke ----------

    [Fact]
    public void OlympicWithConsolation_MutualDsq_in_main_round_marks_both_disqualified()
    {
        var group = TestHelpers.MakeGroup(8);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        var qf1 = group.Bracket.Rounds[0].RoundMatches[0];
        var red = qf1.WrestlerInRed;
        var blue = qf1.WrestlerInBlue;
        proc.CompleteMatch(qf1, null, MatchWinTypeEnum.MutualDisqualify);

        red!.IsDisqualified.Should().BeTrue();
        blue!.IsDisqualified.Should().BeTrue();
        qf1.IsRedWon.Should().BeNull();
    }

    // ---------- SubGroupsToOlympic: smoke ----------

    [Fact]
    public void SubGroupsToOlympic_MutualDsq_in_subgroup_match_marks_both_disqualified()
    {
        var group = TestHelpers.MakeGroup(6); // 3 per subgroup
        var t = TestHelpers.MakeTournament(group);
        var proc = new SubGroupsToOlympicBracketProcessor();
        proc.Generate(t, group);

        var firstMatch = group.Bracket.Rounds[0].RoundMatches[0];
        var red = firstMatch.WrestlerInRed;
        var blue = firstMatch.WrestlerInBlue;
        proc.CompleteMatch(firstMatch, null, MatchWinTypeEnum.MutualDisqualify);

        red!.IsDisqualified.Should().BeTrue();
        blue!.IsDisqualified.Should().BeTrue();
    }

    [Fact]
    public void SubGroupsToOlympic_MutualDsq_in_semifinal_does_not_advance_to_third_place()
    {
        // 6 wrestlers split into A/B subgroups of 3 each. Drive subgroups
        // to completion, then mutual DSQ a semifinal.
        var group = TestHelpers.MakeGroup(6);
        var t = TestHelpers.MakeTournament(group);
        var proc = new SubGroupsToOlympicBracketProcessor();
        proc.Generate(t, group);

        // Complete all main-round matches with deterministic winners
        foreach (var m in group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).SelectMany(r => r.RoundMatches).ToList())
        {
            if (m.Status == MatchStatusEnum.Pending) proc.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);
        }

        var addRounds = group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        var sfRound = addRounds[0];
        var sf1 = sfRound.RoundMatches[0];

        proc.CompleteMatch(sf1, null, MatchWinTypeEnum.MutualDisqualify);

        // 3rd-place match (last additional round) should not have either of sf1's wrestlers
        var thirdPlace = addRounds[addRounds.Count - 1].RoundMatches[0];
        new[] { thirdPlace.WrestlerInRed, thirdPlace.WrestlerInBlue }.Should().NotContain(sf1.WrestlerInRed);
        new[] { thirdPlace.WrestlerInRed, thirdPlace.WrestlerInBlue }.Should().NotContain(sf1.WrestlerInBlue);
    }

    // ---------- Cascade matches don't create paradoxes ----------

    [Fact]
    public void RoundRobin_after_MutualDsq_DSQ_wrestlers_excluded_from_FinalPlace_assignment()
    {
        var group = TestHelpers.MakeGroup(3);
        var t = TestHelpers.MakeTournament(group);
        var proc = new RoundRobinGroupBracketProcessor();
        proc.Generate(t, group);

        var allMatches = group.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();
        var mutualMatch = allMatches[0];
        var a = mutualMatch.WrestlerInRed;
        var b = mutualMatch.WrestlerInBlue;

        proc.CompleteMatch(mutualMatch, null, MatchWinTypeEnum.MutualDisqualify);
        // Remaining matches against C are auto-cascaded.

        proc.GetResults();

        // C should have a finalPlace (1st), A and B should be null
        var c = group.Wrestlers.First(w => !w.SameAs(a) && !w.SameAs(b));
        c.FinalPlace.Should().NotBeNull();
        a!.FinalPlace.Should().BeNull();
        b!.FinalPlace.Should().BeNull();
    }

    // ---------- Helper ----------

    private static (Tournament, AgeWeightGroup, OlympicGroupBracketProcessor) SetupOlympic(int wrestlers)
    {
        var group = TestHelpers.MakeGroup(wrestlers);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicGroupBracketProcessor();
        proc.Generate(t, group);
        return (t, group, proc);
    }
}
