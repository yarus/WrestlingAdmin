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
    public void RoundRobin_MutualDsq_cascades_NoShow_to_both_wrestlers_other_matches()
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

        // All other matches involving A or B should be auto-completed as NoShow
        // — UWW: a DSQ'd wrestler doesn't appear for remaining matches.
        var aOrBMatches = allMatches
            .Where(m => m != mutualMatch && (m.WrestlerInRed!.SameAs(a) || m.WrestlerInBlue!.SameAs(a)
                                            || m.WrestlerInRed.SameAs(b) || m.WrestlerInBlue.SameAs(b)))
            .ToList();
        aOrBMatches.Should().NotBeEmpty();
        aOrBMatches.Should().OnlyContain(m => m.Status == MatchStatusEnum.Completed && m.WinType == MatchWinTypeEnum.NoShow);
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

    // ---------- ConsolationFromFinalists: SF mutual DSQ ----------

    // UWW: when both SF wrestlers are mutually DSQ'd, the QF losers play
    // a rematch in the same SF slot. The bracket must remain playable.
    [Fact]
    public void ConsolationFinalists_MutualDsq_in_SF_rebuilds_SF_with_QF_losers()
    {
        var group = TestHelpers.MakeGroup(8);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        // Drive the four QFs with red winning each.
        var qfs = group.Bracket.Rounds[0].RoundMatches.ToList();
        foreach (var qf in qfs) proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);

        var sfRound = group.Bracket.Rounds[1];
        var sf1 = sfRound.RoundMatches[0];
        // QF losers feeding sf1 (the wrestlers who lost in QFs whose
        // NextMatchBracketFullNumber points to sf1).
        var sf1Sources = qfs.Where(q => q.NextMatchBracketFullNumber == sf1.BracketFullNumber).ToList();
        var qfLoser1 = sf1Sources[0].IsRedWon!.Value ? sf1Sources[0].WrestlerInBlue : sf1Sources[0].WrestlerInRed;
        var qfLoser2 = sf1Sources[1].IsRedWon!.Value ? sf1Sources[1].WrestlerInBlue : sf1Sources[1].WrestlerInRed;
        var origSf1Red = sf1.WrestlerInRed;
        var origSf1Blue = sf1.WrestlerInBlue;

        // Mutual DSQ in SF1.
        proc.CompleteMatch(sf1, null, MatchWinTypeEnum.MutualDisqualify);

        // SF1 must now be playable: Status=Pending, WinType cleared, wrestlers
        // replaced with the two QF losers.
        sf1.Status.Should().Be(MatchStatusEnum.Pending);
        sf1.WinType.Should().BeNull();
        sf1.IsRedWon.Should().BeNull();
        new[] { sf1.WrestlerInRed, sf1.WrestlerInBlue }
            .Should().BeEquivalentTo(new[] { qfLoser1, qfLoser2 });

        // Original SF wrestlers keep IsDisqualified=true.
        origSf1Red!.IsDisqualified.Should().BeTrue();
        origSf1Blue!.IsDisqualified.Should().BeTrue();
    }

    [Fact]
    public void ConsolationFinalists_MutualDsq_in_SF_then_SF_replay_advances_winner_to_final()
    {
        var group = TestHelpers.MakeGroup(8);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        var qfs = group.Bracket.Rounds[0].RoundMatches.ToList();
        foreach (var qf in qfs) proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);

        var sf1 = group.Bracket.Rounds[1].RoundMatches[0];
        var sf2 = group.Bracket.Rounds[1].RoundMatches[1];

        // Mutual DSQ on SF1 → rebuilt with QF losers. Replay it normally.
        proc.CompleteMatch(sf1, null, MatchWinTypeEnum.MutualDisqualify);
        var sf1NewRedWinner = sf1.WrestlerInRed;
        proc.CompleteMatch(sf1, true, MatchWinTypeEnum.PointsWin);

        // Complete SF2 normally so the bracket reaches the final.
        proc.CompleteMatch(sf2, true, MatchWinTypeEnum.PointsWin);
        var sf2Winner = sf2.WrestlerInRed;

        var finalMatch = group.Bracket.Rounds[2].RoundMatches[0];
        new[] { finalMatch.WrestlerInRed, finalMatch.WrestlerInBlue }
            .Should().Contain(sf1NewRedWinner)
            .And.Contain(sf2Winner);
    }

    [Fact]
    public void ConsolationFinalists_RebuiltSF_can_be_reverted_back_to_mutualDsq_state()
    {
        var group = TestHelpers.MakeGroup(8);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        var qfs = group.Bracket.Rounds[0].RoundMatches.ToList();
        foreach (var qf in qfs) proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);

        var sf1 = group.Bracket.Rounds[1].RoundMatches[0];
        var origSf1Red = sf1.WrestlerInRed;
        var origSf1Blue = sf1.WrestlerInBlue;

        proc.CompleteMatch(sf1, null, MatchWinTypeEnum.MutualDisqualify);

        // Rebuilt SF1 must be revertable in its Pending state.
        proc.CanMatchBeReverted(sf1).Should().BeTrue();
        proc.RevertMatch(sf1);

        // After revert, SF1 is back in the original mutual-DSQ Completed state.
        sf1.Status.Should().Be(MatchStatusEnum.Completed);
        sf1.WinType.Should().Be(MatchWinTypeEnum.MutualDisqualify);
        sf1.IsRedWon.Should().BeNull();
        sf1.WrestlerInRed.Should().Be(origSf1Red);
        sf1.WrestlerInBlue.Should().Be(origSf1Blue);
        // IsDisqualified flags persist — second revert clears them via
        // standard mutual-DSQ revert path.
        origSf1Red!.IsDisqualified.Should().BeTrue();
        origSf1Blue!.IsDisqualified.Should().BeTrue();

        // Standard revert from mutual-DSQ Completed clears the DSQ flags.
        proc.RevertMatch(sf1);
        origSf1Red.IsDisqualified.Should().BeFalse();
        origSf1Blue.IsDisqualified.Should().BeFalse();
        sf1.Status.Should().Be(MatchStatusEnum.Pending);
        sf1.WinType.Should().BeNull();
    }

    [Fact]
    public void ConsolationFinalists_QF_revert_blocked_while_downstream_SF_is_rebuilt()
    {
        var group = TestHelpers.MakeGroup(8);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        var qfs = group.Bracket.Rounds[0].RoundMatches.ToList();
        foreach (var qf in qfs) proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);

        var sf1 = group.Bracket.Rounds[1].RoundMatches[0];
        proc.CompleteMatch(sf1, null, MatchWinTypeEnum.MutualDisqualify);

        // The QF that fed sf1 must NOT be revertable while sf1 is still in
        // rebuilt-Pending state — otherwise base revert would corrupt sf1.
        var qfFeedingSf1 = qfs.First(q => q.NextMatchBracketFullNumber == sf1.BracketFullNumber);
        proc.CanMatchBeReverted(qfFeedingSf1).Should().BeFalse();

        // QFs feeding the OTHER (untouched) SF stay revertable.
        var sf2 = group.Bracket.Rounds[1].RoundMatches[1];
        var qfFeedingSf2 = qfs.First(q => q.NextMatchBracketFullNumber == sf2.BracketFullNumber);
        proc.CanMatchBeReverted(qfFeedingSf2).Should().BeTrue();
    }

    // ---------- ClearWrestlerDisqualify: manual DSQ-clear from bracket UI ----------

    [Fact]
    public void ClearWrestlerDisqualify_with_direct_mutual_DSQ_match_reverts_it()
    {
        var group = TestHelpers.MakeGroup(4);
        var t = TestHelpers.MakeTournament(group);
        var proc = new RoundRobinGroupBracketProcessor();
        proc.Generate(t, group);

        var match = group.Bracket.Rounds[0].RoundMatches[0];
        var red = match.WrestlerInRed;
        var blue = match.WrestlerInBlue;
        proc.CompleteMatch(match, null, MatchWinTypeEnum.MutualDisqualify);
        red!.IsDisqualified.Should().BeTrue();
        blue!.IsDisqualified.Should().BeTrue();

        proc.ClearWrestlerDisqualify(red);

        // Match itself reverted, flags cleared on both wrestlers (single revert
        // of mutual DSQ clears both via base RevertMatch).
        red.IsDisqualified.Should().BeFalse();
        blue.IsDisqualified.Should().BeFalse();
        match.Status.Should().Be(MatchStatusEnum.Pending);
        match.WinType.Should().BeNull();
    }

    [Fact]
    public void ClearWrestlerDisqualify_on_rebuilt_SF_runs_two_step_revert()
    {
        var group = TestHelpers.MakeGroup(8);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        var qfs = group.Bracket.Rounds[0].RoundMatches.ToList();
        foreach (var qf in qfs) proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);

        var sf1 = group.Bracket.Rounds[1].RoundMatches[0];
        var origRed = sf1.WrestlerInRed;
        var origBlue = sf1.WrestlerInBlue;
        proc.CompleteMatch(sf1, null, MatchWinTypeEnum.MutualDisqualify);

        // After auto-rebuild SF1.WinType is null; the wrestler-X overlay on
        // origRed's QF cell is what the operator clicks. ClearWrestlerDisqualify
        // must find SF1 via FindIndirectMutualDisqualifyMatch and run the
        // two-step revert (un-rebuild + standard mutual-DSQ revert).
        proc.ClearWrestlerDisqualify(origRed);

        origRed!.IsDisqualified.Should().BeFalse();
        origBlue!.IsDisqualified.Should().BeFalse();
        sf1.Status.Should().Be(MatchStatusEnum.Pending);
        sf1.WinType.Should().BeNull();
        sf1.WrestlerInRed.Should().Be(origRed);
        sf1.WrestlerInBlue.Should().Be(origBlue);
    }

    [Fact]
    public void ClearWrestlerDisqualify_with_no_originating_match_just_clears_flag()
    {
        // Simulates the «stuck flag» case: a wrestler ended up with
        // IsDisqualified=true but no match in the current bracket carries the
        // mutual-DSQ marker (e.g. bracket regenerated after the DSQ was set).
        var group = TestHelpers.MakeGroup(4);
        var t = TestHelpers.MakeTournament(group);
        var proc = new RoundRobinGroupBracketProcessor();
        proc.Generate(t, group);

        var lonelyWrestler = group.Wrestlers.First();
        lonelyWrestler.IsDisqualified = true;

        proc.ClearWrestlerDisqualify(lonelyWrestler);

        lonelyWrestler.IsDisqualified.Should().BeFalse();
    }

    // Regression: when one SF was already mutual-DSQ'd (and rebuilt + replayed),
    // a subsequent mutual DSQ on the OTHER SF used to trigger the base
    // mutual-DSQ-sibling-completed propagation, auto-FreeWin'ing the Final
    // for the first SF's winner BEFORE the second SF rebuild could produce
    // a real result. Final must wait for the rebuilt SF2 outcome.
    [Fact]
    public void ConsolationFinalists_Both_SFs_MutualDsq_does_not_auto_complete_Final()
    {
        var group = TestHelpers.MakeGroup(8);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        var qfs = group.Bracket.Rounds[0].RoundMatches.ToList();
        foreach (var qf in qfs) proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);

        var sf1 = group.Bracket.Rounds[1].RoundMatches[0];
        var sf2 = group.Bracket.Rounds[1].RoundMatches[1];

        // SF1 mutual DSQ → rebuilt with QF losers → operator plays it.
        proc.CompleteMatch(sf1, null, MatchWinTypeEnum.MutualDisqualify);
        proc.CompleteMatch(sf1, true, MatchWinTypeEnum.PointsWin);

        // SF2 mutual DSQ → rebuild fires; Final must NOT be auto-completed
        // for SF1's winner — it has to wait for SF2 rebuild to play out.
        proc.CompleteMatch(sf2, null, MatchWinTypeEnum.MutualDisqualify);

        var finalMatch = group.Bracket.Rounds[2].RoundMatches[0];
        finalMatch.Status.Should().Be(MatchStatusEnum.Pending,
            because: "Final must wait for the rebuilt SF2 to be played");

        // SF2 should be in the rebuilt-Pending state, ready to play.
        sf2.Status.Should().Be(MatchStatusEnum.Pending);
        sf2.WinType.Should().BeNull();
    }

    // When upstream mutual DSQ leaves a consolation/bronze match with a
    // single wrestler (the other slot can never be filled), that wrestler
    // should auto-FreeWin — UWW expects no «hanging» pending matches that
    // are physically impossible to play. Applies to ANY additional round
    // (Утешение Круг 1, 2, ..., 3-е место).
    [Fact]
    public void ConsolationFinalists_LoadResolves_single_wrestler_AdditionalMatches_via_FreeWin()
    {
        // Simulate a saved bracket where an additional-round match got stuck
        // in Pending with only one wrestler (e.g. legacy state from before
        // the auto-FreeWin sweep was added). Load() must auto-resolve.
        var group = TestHelpers.MakeGroup(8);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        var addRounds = group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        var bronzeRound = addRounds.Last();
        var stuckBronze = bronzeRound.RoundMatches[0];
        // Place a wrestler manually into one slot — leave the other empty.
        stuckBronze.WrestlerInRed = group.Wrestlers.First();
        stuckBronze.Status = MatchStatusEnum.Pending;

        // A fresh processor instance loading the saved bracket should sweep
        // and auto-FreeWin the stuck match.
        var loadedProc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        loadedProc.Load(t, group);

        stuckBronze.Status.Should().Be(MatchStatusEnum.Completed);
        stuckBronze.WinType.Should().Be(MatchWinTypeEnum.FreeWin);
        stuckBronze.IsRedWon.Should().BeTrue(because: "lone wrestler is in the red slot");
    }

    // Both feeder slots empty (every upstream feeder DSQ'd or FreeWin-solo).
    // Once all main rounds are settled, an empty Additional match can never
    // be filled — sweep marks it Completed with no winner so IsBracketCompleted
    // flips true. No propagation: there's nothing to advance.
    [Fact]
    public void ConsolationFinalists_LoadResolves_empty_AdditionalMatches_via_NoWinner_FreeWin()
    {
        var group = TestHelpers.MakeGroup(8);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        // Force all main matches into Completed — simulates a tournament
        // where the main bracket fully resolved but Additional slots ended
        // up empty (DSQ-saturated upstream).
        foreach (var mainMatch in group.Bracket.Rounds
                     .Where(r => r.RoundType == GroupRoundTypeEnum.Main)
                     .SelectMany(r => r.RoundMatches))
        {
            mainMatch.Status = MatchStatusEnum.Completed;
            mainMatch.WinType = MatchWinTypeEnum.PointsWin;
            mainMatch.IsRedWon = true;
        }

        var addRounds = group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        var bronzeRound = addRounds.Last();
        var emptyBronze = bronzeRound.RoundMatches[0];
        emptyBronze.WrestlerInRed = null;
        emptyBronze.WrestlerInBlue = null;
        emptyBronze.Status = MatchStatusEnum.Pending;

        var loadedProc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        loadedProc.Load(t, group);

        emptyBronze.Status.Should().Be(MatchStatusEnum.Completed);
        emptyBronze.WinType.Should().Be(MatchWinTypeEnum.FreeWin);
        emptyBronze.IsRedWon.Should().BeNull(because: "no wrestlers, no winner");
    }

    // Mid-flow guard: an empty Additional match must NOT be flagged Completed
    // while a main-round match is still Pending. Otherwise the next
    // ProceedToAdditionalBracket call would try to fill an already-Completed
    // slot, corrupting the bracket. (Covers the regression introduced when
    // empty-match resolution first landed without the all-main-completed gate.)
    [Fact]
    public void ConsolationFinalists_EmptyAdditional_left_alone_while_main_round_pending()
    {
        var group = TestHelpers.MakeGroup(8);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        // Complete all 4 QFs and only the first SF — second SF stays Pending.
        foreach (var qf in group.Bracket.Rounds[0].RoundMatches.ToList())
            proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);
        var sf1 = group.Bracket.Rounds[1].RoundMatches[0];
        proc.CompleteMatch(sf1, true, MatchWinTypeEnum.PointsWin);

        // After SF1, lower-bronze (BracketNumber=2) is still empty — SF2 + its
        // upstream loser will eventually fill it. Sweep must leave it Pending.
        var addRounds = group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        var lowerBronze = addRounds.Last().RoundMatches[1];
        lowerBronze.Status.Should().Be(MatchStatusEnum.Pending);
        lowerBronze.WrestlerInRed.Should().BeNull();
        lowerBronze.WrestlerInBlue.Should().BeNull();
    }

    // ---------- ConsolationFromFinalists: mutual DSQ in early rounds ----------
    //
    // Base ProceedToNextMatch handles mutual DSQ propagation in Olympic-style
    // brackets: both DSQ'd, sibling match auto-FreeWin's the next round for
    // its winner. The ConsolationFromFinalists processor inherits this — these
    // tests pin down that QF/R16/R32 mutual DSQ does NOT trigger the SF-rebuild
    // path (that's reserved for SF only) and the bracket stays playable.

    [Fact]
    public void ConsolationFinalists_MutualDsq_in_QF_marks_both_disqualified()
    {
        var group = TestHelpers.MakeGroup(8);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        var qf1 = group.Bracket.Rounds[0].RoundMatches[0];
        var red = qf1.WrestlerInRed;
        var blue = qf1.WrestlerInBlue;

        proc.CompleteMatch(qf1, null, MatchWinTypeEnum.MutualDisqualify);

        qf1.Status.Should().Be(MatchStatusEnum.Completed);
        qf1.WinType.Should().Be(MatchWinTypeEnum.MutualDisqualify);
        red!.IsDisqualified.Should().BeTrue();
        blue!.IsDisqualified.Should().BeTrue();
    }

    [Fact]
    public void ConsolationFinalists_MutualDsq_in_QF_when_sibling_completes_auto_FreeWins_SF()
    {
        var group = TestHelpers.MakeGroup(8);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        var qf1 = group.Bracket.Rounds[0].RoundMatches[0];
        var qf2 = group.Bracket.Rounds[0].RoundMatches[1];

        // Mutual DSQ in qf1 first, then complete qf2 normally.
        proc.CompleteMatch(qf1, null, MatchWinTypeEnum.MutualDisqualify);
        proc.CompleteMatch(qf2, true, MatchWinTypeEnum.PointsWin);
        var qf2Winner = qf2.WrestlerInRed;

        // The SF that qf1 + qf2 feed into should be auto-FreeWin'd for qf2's
        // winner — even though this is the ConsolationFromFinalists processor.
        var sf = group.Bracket.Rounds[1].RoundMatches[0];
        sf.Status.Should().Be(MatchStatusEnum.Completed);
        sf.WinType.Should().Be(MatchWinTypeEnum.FreeWin);
        var sfWinner = sf.IsRedWon!.Value ? sf.WrestlerInRed : sf.WrestlerInBlue;
        sfWinner.Should().Be(qf2Winner);
    }

    [Fact]
    public void ConsolationFinalists_MutualDsq_in_QF_does_NOT_trigger_SF_rebuild()
    {
        // Sanity check: SF rebuild is reserved for mutual DSQ AT THE SF level.
        // QF mutual DSQ propagates through ProceedToNextMatch, not through
        // TryRebuildSemifinalAfterMutualDsq.
        var group = TestHelpers.MakeGroup(8);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        var qf1 = group.Bracket.Rounds[0].RoundMatches[0];
        proc.CompleteMatch(qf1, null, MatchWinTypeEnum.MutualDisqualify);

        var sf = group.Bracket.Rounds[1].RoundMatches[0];
        // SF has no wrestlers yet (qf2 sibling still pending) — and crucially
        // it is NOT in the rebuilt-Pending state (no QF losers placed there).
        sf.WrestlerInRed.Should().BeNull();
        sf.WrestlerInBlue.Should().BeNull();
        sf.WinType.Should().BeNull();
    }

    [Fact]
    public void ConsolationFinalists_MutualDsq_in_R16_marks_both_disqualified()
    {
        // 16-wrestler bracket: Main rounds = R16, QF, SF, Final.
        var group = TestHelpers.MakeGroup(16);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        var mainRounds = group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
        mainRounds.Should().HaveCount(4); // R16, QF, SF, Final

        var r16Match = mainRounds[0].RoundMatches[0];
        var red = r16Match.WrestlerInRed;
        var blue = r16Match.WrestlerInBlue;

        proc.CompleteMatch(r16Match, null, MatchWinTypeEnum.MutualDisqualify);

        red!.IsDisqualified.Should().BeTrue();
        blue!.IsDisqualified.Should().BeTrue();
    }

    [Fact]
    public void ConsolationFinalists_MutualDsq_in_R16_when_sibling_completes_auto_FreeWins_QF()
    {
        var group = TestHelpers.MakeGroup(16);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        var mainRounds = group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
        var r16_1 = mainRounds[0].RoundMatches[0];
        var r16_2 = mainRounds[0].RoundMatches[1];

        proc.CompleteMatch(r16_1, null, MatchWinTypeEnum.MutualDisqualify);
        proc.CompleteMatch(r16_2, true, MatchWinTypeEnum.PointsWin);
        var r16_2Winner = r16_2.WrestlerInRed;

        // QF that r16_1 + r16_2 feed into auto-FreeWin'd for r16_2's winner.
        var qf = mainRounds[1].RoundMatches[0];
        qf.Status.Should().Be(MatchStatusEnum.Completed);
        qf.WinType.Should().Be(MatchWinTypeEnum.FreeWin);
        var qfWinner = qf.IsRedWon!.Value ? qf.WrestlerInRed : qf.WrestlerInBlue;
        qfWinner.Should().Be(r16_2Winner);

        // The SF this QF feeds into should NOT be completed yet (other QF still pending).
        var sf = mainRounds[2].RoundMatches.First(s => s.BracketFullNumber == qf.NextMatchBracketFullNumber);
        sf.Status.Should().Be(MatchStatusEnum.Pending);
    }

    [Fact]
    public void ConsolationFinalists_MutualDsq_in_R16_FreeWin_QF_winner_propagates_to_SF()
    {
        var group = TestHelpers.MakeGroup(16);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        var mainRounds = group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
        var r16_1 = mainRounds[0].RoundMatches[0];
        var r16_2 = mainRounds[0].RoundMatches[1];

        // Mutual DSQ on r16_1; sibling r16_2 completes → QF auto-FreeWin'd.
        proc.CompleteMatch(r16_1, null, MatchWinTypeEnum.MutualDisqualify);
        proc.CompleteMatch(r16_2, true, MatchWinTypeEnum.PointsWin);
        var r16_2Winner = r16_2.WrestlerInRed;

        var freeWinQf = mainRounds[1].RoundMatches.First(q => q.WinType == MatchWinTypeEnum.FreeWin);
        var qfWinner = freeWinQf.IsRedWon!.Value ? freeWinQf.WrestlerInRed : freeWinQf.WrestlerInBlue;
        qfWinner.Should().Be(r16_2Winner);

        // The QF FreeWin's winner is already propagated to its downstream SF.
        var sfFedByFreeWinQf = mainRounds[2].RoundMatches
            .First(sf => sf.BracketFullNumber == freeWinQf.NextMatchBracketFullNumber);
        new[] { sfFedByFreeWinQf.WrestlerInRed, sfFedByFreeWinQf.WrestlerInBlue }
            .Should().Contain(qfWinner);
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

    // Regression: subgroup-stage mutual DSQ used to promote disqualified
    // wrestlers to the SF because GetResults() ordered by nullable FinalPlace
    // (nulls first) and the promotion code took resultsA[0]/[1] blindly.
    [Fact]
    public void SubGroupsToOlympic_MutualDsq_in_subgroup_does_not_promote_DSQ_wrestlers_to_semifinal()
    {
        // 6 wrestlers split into A/B subgroups of 3 each.
        var group = TestHelpers.MakeGroup(6);
        var t = TestHelpers.MakeTournament(group);
        var proc = new SubGroupsToOlympicBracketProcessor();
        proc.Generate(t, group);

        // Mutual DSQ in subgroup A's first match. Cascade auto-completes the
        // two DSQ wrestlers' remaining subgroup-A matches.
        var allMain = group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).SelectMany(r => r.RoundMatches).ToList();
        var mutualMatch = allMain[0];
        var redDsq = mutualMatch.WrestlerInRed;
        var blueDsq = mutualMatch.WrestlerInBlue;
        proc.CompleteMatch(mutualMatch, null, MatchWinTypeEnum.MutualDisqualify);

        // Complete every remaining pending main-round match normally.
        foreach (var m in allMain.Where(x => x.Status == MatchStatusEnum.Pending).ToList())
        {
            proc.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);
        }

        // Now the SF round should be populated. DSQ wrestlers must NOT appear.
        var sfRound = group.Bracket.Rounds.First(r => r.RoundType == GroupRoundTypeEnum.Additional);
        var sfWrestlers = sfRound.RoundMatches
            .SelectMany(m => new[] { m.WrestlerInRed, m.WrestlerInBlue })
            .Where(w => w != null)
            .ToList();
        sfWrestlers.Should().NotContain(redDsq);
        sfWrestlers.Should().NotContain(blueDsq);
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

    // ---------- Consolation final rebuild (UWW: bronze winners promoted) ----------
    //
    // 8-wrestler consolation layout:
    //   Main: QF (4) → SF (2) → F (1)
    //   Additional: 1 round = "3-е место" with 2 matches (upper, lower).
    // Per UWW rule, when both finalists are mutually DSQ'd, the two bronze
    // medalists move into the (now reset) final and play for places 1-2; the
    // bronze losers take the two 3rd places; everyone below shifts up.

    private static (Tournament, AgeWeightGroup, OlympicWithConsolationFromFinalistsGroupBracketProcessor) SetupConsolation(int wrestlers)
    {
        var group = TestHelpers.MakeGroup(wrestlers);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);
        return (t, group, proc);
    }

    private static void DriveConsolationToBronzeReady(AgeWeightGroup g, OlympicWithConsolationFromFinalistsGroupBracketProcessor proc)
    {
        // Drive QF and SF with red wins. Final and bronze matches stay pending.
        foreach (var qf in g.Bracket.Rounds[0].RoundMatches.ToList())
            proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);
        foreach (var sf in g.Bracket.Rounds[1].RoundMatches.ToList())
            proc.CompleteMatch(sf, true, MatchWinTypeEnum.PointsWin);
    }

    [Fact]
    public void Consolation_MutualDsq_in_Final_then_bronzes_completes_rebuilds_final_with_bronze_winners()
    {
        var (_, g, proc) = SetupConsolation(8);
        DriveConsolationToBronzeReady(g, proc);

        var finalMatch = g.Bracket.Rounds[2].RoundMatches[0];
        var origRed = finalMatch.WrestlerInRed;
        var origBlue = finalMatch.WrestlerInBlue;

        proc.CompleteMatch(finalMatch, null, MatchWinTypeEnum.MutualDisqualify);
        // Bronzes are still pending — rebuild not yet fired.
        finalMatch.WinType.Should().Be(MatchWinTypeEnum.MutualDisqualify);

        var bronzeRound = g.Bracket.Rounds.Last(r => r.RoundType == GroupRoundTypeEnum.Additional);
        var b1 = bronzeRound.RoundMatches[0];
        var b2 = bronzeRound.RoundMatches[1];
        proc.CompleteMatch(b1, true, MatchWinTypeEnum.PointsWin);
        // After only one bronze, rebuild still must not fire.
        finalMatch.WinType.Should().Be(MatchWinTypeEnum.MutualDisqualify);
        proc.CompleteMatch(b2, true, MatchWinTypeEnum.PointsWin);

        // Now both bronzes done — rebuild should have fired.
        finalMatch.Status.Should().Be(MatchStatusEnum.Pending);
        finalMatch.WinType.Should().BeNull();
        finalMatch.IsRedWon.Should().BeNull();
        finalMatch.PointsRed.Should().Be(0);
        finalMatch.PointsBlue.Should().Be(0);
        finalMatch.WrestlerInRed.Should().Be(b1.WrestlerInRed); // bronze1 winner (red wins)
        finalMatch.WrestlerInBlue.Should().Be(b2.WrestlerInRed); // bronze2 winner (red wins)

        // Original DSQ'd finalists must keep IsDisqualified flag.
        origRed!.IsDisqualified.Should().BeTrue();
        origBlue!.IsDisqualified.Should().BeTrue();
    }

    [Fact]
    public void Consolation_bronzes_first_then_MutualDsq_in_Final_rebuilds_immediately()
    {
        var (_, g, proc) = SetupConsolation(8);
        DriveConsolationToBronzeReady(g, proc);

        var bronzeRound = g.Bracket.Rounds.Last(r => r.RoundType == GroupRoundTypeEnum.Additional);
        var b1 = bronzeRound.RoundMatches[0];
        var b2 = bronzeRound.RoundMatches[1];
        proc.CompleteMatch(b1, true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(b2, true, MatchWinTypeEnum.PointsWin);

        var finalMatch = g.Bracket.Rounds[2].RoundMatches[0];
        proc.CompleteMatch(finalMatch, null, MatchWinTypeEnum.MutualDisqualify);

        // Rebuild fires inside the same CompleteMatch call.
        finalMatch.Status.Should().Be(MatchStatusEnum.Pending);
        finalMatch.WinType.Should().BeNull();
        finalMatch.WrestlerInRed.Should().Be(b1.WrestlerInRed);
        finalMatch.WrestlerInBlue.Should().Be(b2.WrestlerInRed);
    }

    [Fact]
    public void Consolation_rebuilt_final_replayed_yields_places_1_2_3_3()
    {
        var (_, g, proc) = SetupConsolation(8);
        DriveConsolationToBronzeReady(g, proc);

        var finalMatch = g.Bracket.Rounds[2].RoundMatches[0];
        var origRed = finalMatch.WrestlerInRed;
        var origBlue = finalMatch.WrestlerInBlue;

        proc.CompleteMatch(finalMatch, null, MatchWinTypeEnum.MutualDisqualify);
        var bronzeRound = g.Bracket.Rounds.Last(r => r.RoundType == GroupRoundTypeEnum.Additional);
        var b1 = bronzeRound.RoundMatches[0];
        var b2 = bronzeRound.RoundMatches[1];
        proc.CompleteMatch(b1, true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(b2, true, MatchWinTypeEnum.PointsWin);

        // Rebuilt final: replay with red winning.
        var newGold = finalMatch.WrestlerInRed;     // bronze1 winner
        var newSilver = finalMatch.WrestlerInBlue;  // bronze2 winner
        var bronze1Loser = b1.WrestlerInBlue;       // lost to newGold
        var bronze2Loser = b2.WrestlerInBlue;       // lost to newSilver
        proc.CompleteMatch(finalMatch, true, MatchWinTypeEnum.PointsWin);

        proc.GetResults();

        newGold!.FinalPlace.Should().Be(1);
        newSilver!.FinalPlace.Should().Be(2);
        bronze1Loser!.FinalPlace.Should().Be(3, "5th-place finishers move up to 3rd per UWW");
        bronze2Loser!.FinalPlace.Should().Be(3);
        origRed!.FinalPlace.Should().BeNull("DSQ'd original finalists stay placeless");
        origBlue!.FinalPlace.Should().BeNull();

        // Remaining wrestlers (8 - 6 = 2) take 5th and 6th — points-based start
        // is 5 (not 7) after the rebuild because the «остальные поднимаются»
        // shift removes 5th-place slot.
        var ranked = g.Wrestlers.Where(w => !w.IsDisqualified && w.FinalPlace.HasValue).OrderBy(w => w.FinalPlace).ToList();
        ranked.Should().HaveCount(6);
        ranked.Select(w => w.FinalPlace).Should().BeEquivalentTo(new int?[] { 1, 2, 3, 3, 5, 6 });
    }

    [Fact]
    public void Consolation_bronze_revert_allowed_when_rebuilt_final_is_pending()
    {
        var (_, g, proc) = SetupConsolation(8);
        DriveConsolationToBronzeReady(g, proc);

        proc.CompleteMatch(g.Bracket.Rounds[2].RoundMatches[0], null, MatchWinTypeEnum.MutualDisqualify);
        var bronzeRound = g.Bracket.Rounds.Last(r => r.RoundType == GroupRoundTypeEnum.Additional);
        proc.CompleteMatch(bronzeRound.RoundMatches[0], true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(bronzeRound.RoundMatches[1], true, MatchWinTypeEnum.PointsWin);

        // Rebuilt final is Pending — operator can revert a bronze.
        proc.CanMatchBeReverted(bronzeRound.RoundMatches[0]).Should().BeTrue();
        proc.CanMatchBeReverted(bronzeRound.RoundMatches[1]).Should().BeTrue();
    }

    [Fact]
    public void Consolation_bronze_revert_blocked_when_rebuilt_final_is_completed()
    {
        var (_, g, proc) = SetupConsolation(8);
        DriveConsolationToBronzeReady(g, proc);

        proc.CompleteMatch(g.Bracket.Rounds[2].RoundMatches[0], null, MatchWinTypeEnum.MutualDisqualify);
        var bronzeRound = g.Bracket.Rounds.Last(r => r.RoundType == GroupRoundTypeEnum.Additional);
        proc.CompleteMatch(bronzeRound.RoundMatches[0], true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(bronzeRound.RoundMatches[1], true, MatchWinTypeEnum.PointsWin);

        // Replay the rebuilt final
        proc.CompleteMatch(g.Bracket.Rounds[2].RoundMatches[0], true, MatchWinTypeEnum.PointsWin);

        // Rebuilt final has been completed → bronze revert blocked. Operator
        // must revert the final first.
        proc.CanMatchBeReverted(bronzeRound.RoundMatches[0]).Should().BeFalse();
        proc.CanMatchBeReverted(bronzeRound.RoundMatches[1]).Should().BeFalse();
    }

    [Fact]
    public void Consolation_bronze_revert_unrebuilds_final_back_to_mutual_DSQ()
    {
        var (_, g, proc) = SetupConsolation(8);
        DriveConsolationToBronzeReady(g, proc);

        // Capture original SF winners before mutual DSQ — these are the
        // original finalists that should be restored on un-rebuild.
        var sfRound = g.Bracket.Rounds[1];
        var origRed = sfRound.RoundMatches[0].WrestlerInRed; // SF1 red won
        var origBlue = sfRound.RoundMatches[1].WrestlerInRed; // SF2 red won

        var finalMatch = g.Bracket.Rounds[2].RoundMatches[0];
        proc.CompleteMatch(finalMatch, null, MatchWinTypeEnum.MutualDisqualify);
        var bronzeRound = g.Bracket.Rounds.Last(r => r.RoundType == GroupRoundTypeEnum.Additional);
        proc.CompleteMatch(bronzeRound.RoundMatches[0], true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(bronzeRound.RoundMatches[1], true, MatchWinTypeEnum.PointsWin);

        // Sanity: rebuild fired
        finalMatch.WrestlerInRed.Should().Be(bronzeRound.RoundMatches[0].WrestlerInRed);
        finalMatch.Status.Should().Be(MatchStatusEnum.Pending);

        // Revert one bronze
        proc.RevertMatch(bronzeRound.RoundMatches[0]);

        // Final should be un-rebuilt: original DSQ'd finalists back, mutual
        // DSQ Completed state restored.
        finalMatch.WrestlerInRed.Should().Be(origRed);
        finalMatch.WrestlerInBlue.Should().Be(origBlue);
        finalMatch.Status.Should().Be(MatchStatusEnum.Completed);
        finalMatch.WinType.Should().Be(MatchWinTypeEnum.MutualDisqualify);
        finalMatch.IsRedWon.Should().BeNull();
        origRed!.IsDisqualified.Should().BeTrue();
        origBlue!.IsDisqualified.Should().BeTrue();

        // Reverted bronze itself is back to pending
        bronzeRound.RoundMatches[0].Status.Should().Be(MatchStatusEnum.Pending);
    }

    [Fact]
    public void Consolation_full_revert_replay_sequence_leaves_consistent_state()
    {
        // Reproduces the operator's reported sequence (2026-05-08):
        //   1. revert both bronzes
        //   2. revert the final's mutual DSQ
        //   3. re-complete both bronzes
        //   4. re-mark the final mutual DSQ
        // After step 4 the rebuild must fire, the final must end up Pending
        // with the bronze winners populated, and the original DSQ'd finalists
        // must keep IsDisqualified=true. This is what the VM relies on; if
        // any of those invariants fail the operator gets kicked to home.
        var (_, g, proc) = SetupConsolation(8);
        DriveConsolationToBronzeReady(g, proc);

        var sfRound = g.Bracket.Rounds[1];
        var origRed = sfRound.RoundMatches[0].WrestlerInRed;   // SF1 winner
        var origBlue = sfRound.RoundMatches[1].WrestlerInRed;  // SF2 winner

        var finalMatch = g.Bracket.Rounds[2].RoundMatches[0];
        var bronzeRound = g.Bracket.Rounds.Last(r => r.RoundType == GroupRoundTypeEnum.Additional);
        var bronze1 = bronzeRound.RoundMatches[0];
        var bronze2 = bronzeRound.RoundMatches[1];

        // Initial: mutual DSQ in final + both bronzes done → rebuild fires
        proc.CompleteMatch(finalMatch, null, MatchWinTypeEnum.MutualDisqualify);
        proc.CompleteMatch(bronze1, true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(bronze2, true, MatchWinTypeEnum.PointsWin);
        finalMatch.Status.Should().Be(MatchStatusEnum.Pending, "rebuild fired");

        // Step 1: revert both bronzes (first revert un-rebuilds the final)
        proc.RevertMatch(bronze1);
        finalMatch.WinType.Should().Be(MatchWinTypeEnum.MutualDisqualify, "un-rebuild restored mutual DSQ");
        proc.RevertMatch(bronze2);

        // Step 2: revert the final mutual DSQ
        proc.CanMatchBeReverted(finalMatch).Should().BeTrue();
        proc.RevertMatch(finalMatch);
        origRed!.IsDisqualified.Should().BeFalse("revert clears DSQ flags on originals");
        origBlue!.IsDisqualified.Should().BeFalse();

        // Step 3: re-complete both bronzes
        proc.CompleteMatch(bronze1, true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(bronze2, true, MatchWinTypeEnum.PointsWin);
        finalMatch.WinType.Should().BeNull("rebuild guard: only fires when final is mutual DSQ");

        // Step 4: re-mark the final mutual DSQ — rebuild must fire again
        proc.CompleteMatch(finalMatch, null, MatchWinTypeEnum.MutualDisqualify);

        // Final invariants — the VM's ApproveAsync depends on these holding:
        finalMatch.Status.Should().Be(MatchStatusEnum.Pending, "rebuild reset to pending for replay");
        finalMatch.WinType.Should().BeNull();
        finalMatch.WrestlerInRed.Should().Be(bronze1.WrestlerInRed, "bronze1 winner promoted");
        finalMatch.WrestlerInBlue.Should().Be(bronze2.WrestlerInRed, "bronze2 winner promoted");
        origRed.IsDisqualified.Should().BeTrue("originals are DSQ'd by step-4 mutual DSQ");
        origBlue.IsDisqualified.Should().BeTrue();
    }

    [Fact]
    public void Consolation_replay_bronze_after_revert_re_triggers_rebuild_with_new_winner()
    {
        var (_, g, proc) = SetupConsolation(8);
        DriveConsolationToBronzeReady(g, proc);

        var finalMatch = g.Bracket.Rounds[2].RoundMatches[0];
        proc.CompleteMatch(finalMatch, null, MatchWinTypeEnum.MutualDisqualify);
        var bronzeRound = g.Bracket.Rounds.Last(r => r.RoundType == GroupRoundTypeEnum.Additional);
        proc.CompleteMatch(bronzeRound.RoundMatches[0], true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(bronzeRound.RoundMatches[1], true, MatchWinTypeEnum.PointsWin);

        // Revert bronze1 and replay it with the OTHER side winning
        proc.RevertMatch(bronzeRound.RoundMatches[0]);
        var newBronze1Winner = bronzeRound.RoundMatches[0].WrestlerInBlue;
        proc.CompleteMatch(bronzeRound.RoundMatches[0], false, MatchWinTypeEnum.PointsWin);

        // Rebuild auto-fires: new bronze1 winner takes the finalist slot.
        finalMatch.Status.Should().Be(MatchStatusEnum.Pending);
        finalMatch.WrestlerInRed.Should().Be(newBronze1Winner);
        finalMatch.WrestlerInBlue.Should().Be(bronzeRound.RoundMatches[1].WrestlerInRed);
    }

    [Fact]
    public void Consolation_rebuilt_final_can_be_reverted_back_to_pending_with_bronze_winners()
    {
        var (_, g, proc) = SetupConsolation(8);
        DriveConsolationToBronzeReady(g, proc);

        var finalMatch = g.Bracket.Rounds[2].RoundMatches[0];
        proc.CompleteMatch(finalMatch, null, MatchWinTypeEnum.MutualDisqualify);
        var bronzeRound = g.Bracket.Rounds.Last(r => r.RoundType == GroupRoundTypeEnum.Additional);
        proc.CompleteMatch(bronzeRound.RoundMatches[0], true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(bronzeRound.RoundMatches[1], true, MatchWinTypeEnum.PointsWin);

        var newRed = finalMatch.WrestlerInRed;
        var newBlue = finalMatch.WrestlerInBlue;

        // Replay rebuilt final
        proc.CompleteMatch(finalMatch, true, MatchWinTypeEnum.PointsWin);
        proc.CanMatchBeReverted(finalMatch).Should().BeTrue();
        proc.RevertMatch(finalMatch);

        // After revert: pending again, bronze winners still in place.
        finalMatch.Status.Should().Be(MatchStatusEnum.Pending);
        finalMatch.WrestlerInRed.Should().Be(newRed);
        finalMatch.WrestlerInBlue.Should().Be(newBlue);
    }

    [Fact]
    public void Consolation_bronze_mutual_DSQ_does_not_trigger_rebuild()
    {
        // Edge case: if a bronze match is itself mutual DSQ, neither side
        // has a candidate winner — the rebuild bails out and stays pending.
        var (_, g, proc) = SetupConsolation(8);
        DriveConsolationToBronzeReady(g, proc);

        var finalMatch = g.Bracket.Rounds[2].RoundMatches[0];
        proc.CompleteMatch(finalMatch, null, MatchWinTypeEnum.MutualDisqualify);

        var bronzeRound = g.Bracket.Rounds.Last(r => r.RoundType == GroupRoundTypeEnum.Additional);
        proc.CompleteMatch(bronzeRound.RoundMatches[0], null, MatchWinTypeEnum.MutualDisqualify);
        proc.CompleteMatch(bronzeRound.RoundMatches[1], true, MatchWinTypeEnum.PointsWin);

        // Final stayed mutual DSQ — not rebuilt.
        finalMatch.WinType.Should().Be(MatchWinTypeEnum.MutualDisqualify);
        finalMatch.Status.Should().Be(MatchStatusEnum.Completed);
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
