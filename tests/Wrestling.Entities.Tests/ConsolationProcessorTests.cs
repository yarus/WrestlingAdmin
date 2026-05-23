using System.Linq;
using FluentAssertions;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Entities.Tests;

// Coverage for OlympicWithConsolationFromFinalistsGroupBracketProcessor.
// Bracket layout for 8 wrestlers:
//   Main: QF (4 matches) → SF (2) → F (1)
//   Additional: 2 rounds — round 1 has 2 matches (Утешение Круг 1), round 2
//                          has 2 matches (3-е место, two bronzes).
// Loser of each SF feeds into the matching side of the consolation final;
// previous-round losers of the SF winner feed into Утешение Круг 1.
public class ConsolationProcessorTests
{
    private static (Tournament, AgeWeightGroup, OlympicWithConsolationFromFinalistsGroupBracketProcessor) Setup(int wrestlers)
    {
        var group = TestHelpers.MakeGroup(wrestlers);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);
        return (t, group, proc);
    }

    [Fact]
    public void Generate_8_wrestlers_produces_3_main_rounds_and_1_additional_round()
    {
        // additionalRoundsCount = mainRounds.Count - 2 = 3 - 2 = 1.
        // The single additional round has 2 matches (two bronzes).
        var (_, g, _) = Setup(8);

        var main = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
        var add = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();

        main.Should().HaveCount(3);
        main[0].RoundMatches.Should().HaveCount(4);
        main[1].RoundMatches.Should().HaveCount(2);
        main[2].RoundMatches.Should().HaveCount(1);

        add.Should().HaveCount(1);
        add[0].RoundMatches.Should().HaveCount(2);
    }

    [Fact]
    public void Generate_below_4_wrestlers_skips_additional_rounds()
    {
        var group = TestHelpers.MakeGroup(2);
        var t = TestHelpers.MakeTournament(group);
        var proc = new OlympicWithConsolationFromFinalistsGroupBracketProcessor();
        proc.Generate(t, group);

        g_NoAdditional(group);

        static void g_NoAdditional(AgeWeightGroup g) =>
            g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).Should().BeEmpty();
    }

    [Fact]
    public void Last_additional_round_matches_have_empty_NextMatchBracketFullNumber()
    {
        var (_, g, _) = Setup(8);
        var addLast = g.Bracket.Rounds.Last(r => r.RoundType == GroupRoundTypeEnum.Additional);
        addLast.RoundMatches.Should().OnlyContain(m => string.IsNullOrEmpty(m.NextMatchBracketFullNumber));
    }

    [Fact]
    public void Final_winner_gets_FinalPlace_1_and_loser_gets_2()
    {
        var (_, g, proc) = Setup(8);
        // Drive QF → SF → F with red always winning.
        foreach (var qf in g.Bracket.Rounds[0].RoundMatches.ToList())
            proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);
        foreach (var sf in g.Bracket.Rounds[1].RoundMatches.ToList())
            proc.CompleteMatch(sf, true, MatchWinTypeEnum.PointsWin);

        var finalMatch = g.Bracket.Rounds[2].RoundMatches[0];
        var goldExpected = finalMatch.WrestlerInRed;
        var silverExpected = finalMatch.WrestlerInBlue;
        proc.CompleteMatch(finalMatch, true, MatchWinTypeEnum.PointsWin);

        proc.GetResults();

        goldExpected!.FinalPlace.Should().Be(1);
        silverExpected!.FinalPlace.Should().Be(2);
    }

    [Fact]
    public void Semifinal_loser_is_placed_into_consolation_final()
    {
        var (_, g, proc) = Setup(8);
        // Complete QFs (red wins each)
        foreach (var qf in g.Bracket.Rounds[0].RoundMatches.ToList())
            proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);

        var sf1 = g.Bracket.Rounds[1].RoundMatches[0];
        var sf1Loser = sf1.WrestlerInBlue;
        proc.CompleteMatch(sf1, true, MatchWinTypeEnum.PointsWin);

        var addLast = g.Bracket.Rounds.Last(r => r.RoundType == GroupRoundTypeEnum.Additional);
        var allAddSlots = addLast.RoundMatches
            .SelectMany(m => new[] { m.WrestlerInRed, m.WrestlerInBlue })
            .ToList();
        allAddSlots.Should().Contain(sf1Loser, "SF loser feeds into consolation final");
    }

    [Fact]
    public void Bronze_match_winner_gets_FinalPlace_3_and_loser_gets_5()
    {
        var (_, g, proc) = Setup(8);
        foreach (var qf in g.Bracket.Rounds[0].RoundMatches.ToList())
            proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);
        foreach (var sf in g.Bracket.Rounds[1].RoundMatches.ToList())
            proc.CompleteMatch(sf, true, MatchWinTypeEnum.PointsWin);

        // Final
        proc.CompleteMatch(g.Bracket.Rounds[2].RoundMatches[0], true, MatchWinTypeEnum.PointsWin);

        // Consolation rounds: complete in order.
        var addRounds = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        foreach (var round in addRounds)
            foreach (var m in round.RoundMatches.ToList())
                if (m.Status == MatchStatusEnum.Pending && m.WrestlerInRed != null && m.WrestlerInBlue != null)
                    proc.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);

        proc.GetResults();

        var bronzeFinal1 = addRounds.Last().RoundMatches[0];
        var bronze1Winner = bronzeFinal1.WrestlerInRed;
        var bronze1Loser = bronzeFinal1.WrestlerInBlue;
        bronze1Winner!.FinalPlace.Should().Be(3);
        bronze1Loser!.FinalPlace.Should().Be(5);
    }

    [Fact]
    public void Revert_semifinal_clears_consolation_branch()
    {
        var (_, g, proc) = Setup(8);
        foreach (var qf in g.Bracket.Rounds[0].RoundMatches.ToList())
            proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);

        var sf1 = g.Bracket.Rounds[1].RoundMatches[0];
        proc.CompleteMatch(sf1, true, MatchWinTypeEnum.PointsWin);

        proc.CanMatchBeReverted(sf1).Should().BeTrue();
        proc.RevertMatch(sf1);

        // After revert, the entire isUpperBracket=true side of consolation is cleared.
        var addRounds = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        // BracketNumber 1 is the upper-bracket side
        var upperConsolationMatches = addRounds.SelectMany(r => r.RoundMatches).Where(m => m.BracketNumber == 1).ToList();
        upperConsolationMatches.Should().OnlyContain(m =>
            m.Status == MatchStatusEnum.Pending && m.WrestlerInRed == null && m.WrestlerInBlue == null);
    }

    [Fact]
    public void Cannot_revert_semifinal_when_consolation_match_already_completed()
    {
        var (_, g, proc) = Setup(8);
        foreach (var qf in g.Bracket.Rounds[0].RoundMatches.ToList())
            proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);

        var sf1 = g.Bracket.Rounds[1].RoundMatches[0];
        proc.CompleteMatch(sf1, true, MatchWinTypeEnum.PointsWin);

        // Complete a consolation match on the upper side.
        var addRounds = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        var upperFirst = addRounds[0].RoundMatches.First(m => m.BracketNumber == 1
                                                             && m.WrestlerInRed != null && m.WrestlerInBlue != null);
        proc.CompleteMatch(upperFirst, true, MatchWinTypeEnum.PointsWin);

        proc.CanMatchBeReverted(sf1).Should().BeFalse(
            "revert blocked while a downstream consolation match is completed");
    }

    [Fact]
    public void Get3rdPlaceRound_returns_last_additional_round()
    {
        var (_, g, proc) = Setup(8);
        var third = proc.Get3rdPlaceRound(g);
        third.Should().NotBeNull();
        third!.RoundMatches.Should().HaveCount(2);
        third.RoundMatches.Should().OnlyContain(m => string.IsNullOrEmpty(m.NextMatchBracketFullNumber));
    }

    [Fact]
    public void Generate_with_16_wrestlers_creates_2_consolation_rounds()
    {
        // mainRounds.Count = 4 (1/8, QF, SF, F).
        // additionalRoundsCount = 4 - 2 = 2.
        var (_, g, _) = Setup(16);

        var main = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
        var add = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();

        main.Should().HaveCount(4);
        add.Should().HaveCount(2);
        add.Should().OnlyContain(r => r.RoundMatches.Count == 2);
    }

    [Fact]
    public void Every_wrestler_gets_a_FinalPlace_when_bracket_fully_completed()
    {
        // 8-wrestler consolation distribution: 1, 2, 3, 3, 5, 5, 7, 8.
        // Two QF losers (those who lost to the SF loser) skip consolation
        // entirely and are ranked 7-8 by classification points.
        var (_, g, proc) = Setup(8);

        foreach (var qf in g.Bracket.Rounds[0].RoundMatches.ToList())
            proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);
        foreach (var sf in g.Bracket.Rounds[1].RoundMatches.ToList())
            proc.CompleteMatch(sf, true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(g.Bracket.Rounds[2].RoundMatches[0], true, MatchWinTypeEnum.PointsWin);

        var addRounds = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
        foreach (var round in addRounds)
            foreach (var m in round.RoundMatches.ToList())
                if (m.Status == MatchStatusEnum.Pending && m.WrestlerInRed != null && m.WrestlerInBlue != null)
                    proc.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);

        proc.GetResults();

        var places = g.Wrestlers.Select(w => w.FinalPlace).ToList();
        places.Should().OnlyContain(p => p.HasValue && p.Value >= 1 && p.Value <= 8);
        // Two duplicates (3 and 5 each appear twice for two-bronze format).
        places.Distinct().Should().HaveCount(6);
        places.Count(p => p == 3).Should().Be(2);
        places.Count(p => p == 5).Should().Be(2);
    }

    // UWW: when a finalist is DSQ'd in the final, the DSQ-side bronze winner
    // is promoted to silver, the DSQ-side bronze loser to bronze. So:
    //   1 → final winner
    //   2 → bronze winner of DSQ side
    //   3 → bronze winner of other side  +  bronze loser of DSQ side
    //   5 → bronze loser of other side (single 5th)
    // DSQ'd finalist stays placeless with IsDisqualified=true.
    [Fact]
    public void Final_single_DSQ_promotes_DSQ_side_bronze_winner_to_silver_and_bronze_loser_to_third()
    {
        var (_, g, proc) = Setup(8);

        // Drive QF → SF → F with red winning each (deterministic).
        foreach (var qf in g.Bracket.Rounds[0].RoundMatches.ToList())
            proc.CompleteMatch(qf, true, MatchWinTypeEnum.PointsWin);
        foreach (var sf in g.Bracket.Rounds[1].RoundMatches.ToList())
            proc.CompleteMatch(sf, true, MatchWinTypeEnum.PointsWin);

        var finalMatch = g.Bracket.Rounds[2].RoundMatches[0];
        var goldExpected = finalMatch.WrestlerInRed;
        var dsqFinalist = finalMatch.WrestlerInBlue;

        // Red wins by DSQ — Blue is now disqualified.
        proc.CompleteMatch(finalMatch, true, MatchWinTypeEnum.DisqualifyWin);

        dsqFinalist!.IsDisqualified.Should().BeTrue("single DSQ marks the loser disqualified");

        // Find DSQ-side bronze (the bronze that received the DSQ finalist's SF loser).
        var sfRound = g.Bracket.Rounds[1];
        var dsqSf = sfRound.RoundMatches.First(sf =>
            (sf.IsRedWon!.Value ? sf.WrestlerInRed : sf.WrestlerInBlue)!.SameAs(dsqFinalist));
        var dsqSfLoser = dsqSf.IsRedWon!.Value ? dsqSf.WrestlerInBlue : dsqSf.WrestlerInRed;

        var bronzeRound = g.Bracket.Rounds.Last(r => r.RoundType == GroupRoundTypeEnum.Additional);
        var dsqBronze = bronzeRound.RoundMatches.First(b =>
            (b.WrestlerInRed != null && b.WrestlerInRed.SameAs(dsqSfLoser))
            || (b.WrestlerInBlue != null && b.WrestlerInBlue.SameAs(dsqSfLoser)));
        var otherBronze = bronzeRound.RoundMatches.First(b => b != dsqBronze);

        // Complete bronzes — red wins each.
        proc.CompleteMatch(dsqBronze, true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(otherBronze, true, MatchWinTypeEnum.PointsWin);

        var dsqBronzeWinner = dsqBronze.WrestlerInRed;
        var dsqBronzeLoser = dsqBronze.WrestlerInBlue;
        var otherBronzeWinner = otherBronze.WrestlerInRed;
        var otherBronzeLoser = otherBronze.WrestlerInBlue;

        proc.GetResults();

        goldExpected!.FinalPlace.Should().Be(1);
        dsqFinalist.FinalPlace.Should().BeNull(because: "DSQ'd wrestlers stay placeless");
        dsqBronzeWinner!.FinalPlace.Should().Be(2, because: "promoted from 3rd via UWW final-DSQ rule");
        dsqBronzeLoser!.FinalPlace.Should().Be(3, because: "promoted from 5th to vacated 3rd");
        otherBronzeWinner!.FinalPlace.Should().Be(3);
        otherBronzeLoser!.FinalPlace.Should().Be(5);

        // Only one 5th place.
        g.Wrestlers.Count(w => w.FinalPlace == 5).Should().Be(1);
    }

    [Fact]
    public void Single_DSQ_in_QF_marks_loser_as_disqualified()
    {
        var (_, g, proc) = Setup(8);
        var qf1 = g.Bracket.Rounds[0].RoundMatches[0];
        var loser = qf1.WrestlerInBlue;

        proc.CompleteMatch(qf1, true, MatchWinTypeEnum.DisqualifyWin);

        loser!.IsDisqualified.Should().BeTrue();
        qf1.WrestlerInRed!.IsDisqualified.Should().BeFalse(because: "winner stays clean");
    }

    [Fact]
    public void Reverting_single_DSQ_clears_IsDisqualified()
    {
        var (_, g, proc) = Setup(8);
        var qf1 = g.Bracket.Rounds[0].RoundMatches[0];
        var loser = qf1.WrestlerInBlue;

        proc.CompleteMatch(qf1, true, MatchWinTypeEnum.DisqualifyWin);
        loser!.IsDisqualified.Should().BeTrue();

        proc.RevertMatch(qf1);
        loser.IsDisqualified.Should().BeFalse();
    }
}
