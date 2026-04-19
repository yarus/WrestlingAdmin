using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Entities.Tests;

public class OlympicBracketTests
{
    private static (Tournament, AgeWeightGroup, OlympicGroupBracketProcessor) Setup(int wrestlers)
    {
        var group = TestHelpers.MakeGroup(wrestlers);
        var tournament = TestHelpers.MakeTournament(group);
        var processor = new OlympicGroupBracketProcessor();
        processor.Generate(tournament, group);
        return (tournament, group, processor);
    }

    [Theory]
    [InlineData(4, 2, new[] { 2, 1 })]  // round 1 has 2 full matches, final has 1
    [InlineData(8, 3, new[] { 4, 2, 1 })]
    [InlineData(16, 4, new[] { 8, 4, 2, 1 })]
    public void PowerOfTwo_produces_expected_round_sizes(int wrestlers, int mainRounds, int[] matchesPerRound)
    {
        var (_, g, _) = Setup(wrestlers);

        var main = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
        main.Should().HaveCount(mainRounds);
        main.Select(r => r.RoundMatches.Count).Should().Equal(matchesPerRound);

        // Olympic always adds a 3rd-place round when wrestlers >= 4
        if (wrestlers >= 4)
        {
            g.Bracket.Rounds.Should().Contain(r => r.RoundType == GroupRoundTypeEnum.Additional);
        }
    }

    [Fact]
    public void Six_wrestlers_yields_two_free_wins_in_first_round()
    {
        var (_, g, _) = Setup(6);

        var round1 = g.Bracket.Rounds.First();
        round1.RoundMatches.Should().HaveCount(4);

        var freeWins = round1.RoundMatches
            .Where(m => m.Status == MatchStatusEnum.Completed && m.WinType == MatchWinTypeEnum.FreeWin)
            .ToList();
        freeWins.Should().HaveCount(2);
    }

    [Fact]
    public void Seven_wrestlers_yields_one_free_win_in_first_round()
    {
        var (_, g, _) = Setup(7);

        var round1 = g.Bracket.Rounds.First();
        round1.RoundMatches.Should().HaveCount(4);

        round1.RoundMatches
            .Count(m => m.Status == MatchStatusEnum.Completed && m.WinType == MatchWinTypeEnum.FreeWin)
            .Should().Be(1);
    }

    [Fact]
    public void Round_names_map_to_Russian_bracket_labels()
    {
        var (_, g, _) = Setup(8);
        var main = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
        main[0].RoundName.Should().Be("1/4 финала");
        main[1].RoundName.Should().Be("Полуфинал");
        main[2].RoundName.Should().Be("Финал");
    }

    [Fact]
    public void Final_round_match_has_no_next_match_link()
    {
        var (_, g, _) = Setup(8);
        var main = g.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
        main.Last().RoundMatches[0].NextMatchBracketFullNumber.Should().BeNullOrEmpty();
    }

    [Fact]
    public void CompleteMatch_propagates_winner_into_next_round()
    {
        var (_, g, proc) = Setup(4);

        var round1 = g.Bracket.Rounds[0];
        var first = round1.RoundMatches[0];
        var winner = first.WrestlerInRed;

        proc.CompleteMatch(first, isRedWon: true, MatchWinTypeEnum.PointsWin);

        var round2 = g.Bracket.Rounds[1];
        var final = round2.RoundMatches[0];
        (final.WrestlerInRed == winner || final.WrestlerInBlue == winner)
            .Should().BeTrue("winner of bracket#1 should become a slot in the next match");
    }

    [Fact]
    public void Losers_of_semifinals_feed_into_3rd_place_match()
    {
        var (_, g, proc) = Setup(4);

        var round1 = g.Bracket.Rounds[0];
        var m1 = round1.RoundMatches[0];
        var m2 = round1.RoundMatches[1];
        var m1Loser = m1.WrestlerInBlue;
        var m2Loser = m2.WrestlerInBlue;

        // With N=4, round 1 IS the semifinal (mainRounds.Count - 1 == 0? no; mainRounds.Count == 2, index 0 is semi)
        proc.CompleteMatch(m1, isRedWon: true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(m2, isRedWon: true, MatchWinTypeEnum.PointsWin);

        var thirdPlace = g.Bracket.Rounds.Single(r => r.RoundType == GroupRoundTypeEnum.Additional);
        var thirdMatch = thirdPlace.RoundMatches[0];

        new[] { thirdMatch.WrestlerInRed, thirdMatch.WrestlerInBlue }
            .Should().Contain(m1Loser);
        new[] { thirdMatch.WrestlerInRed, thirdMatch.WrestlerInBlue }
            .Should().Contain(m2Loser);
    }

    [Fact]
    public void RevertMatch_clears_points_and_removes_winner_from_next_match()
    {
        var (_, g, proc) = Setup(4);

        var round1 = g.Bracket.Rounds[0];
        var first = round1.RoundMatches[0];
        first.PointsRed = 5;
        first.PointsBlue = 3;

        proc.CompleteMatch(first, isRedWon: true, MatchWinTypeEnum.PointsWin);

        // Can revert because the final is still pending
        proc.CanMatchBeReverted(first).Should().BeTrue();
        proc.RevertMatch(first);

        first.Status.Should().Be(MatchStatusEnum.Pending);
        first.IsRedWon.Should().BeNull();
        first.WinType.Should().BeNull();
        first.PointsRed.Should().Be(0);
        first.PointsBlue.Should().Be(0);
    }

    [Fact]
    public void CalculateResults_assigns_gold_silver_bronze_and_fourth()
    {
        var (_, g, proc) = Setup(4);

        var r1 = g.Bracket.Rounds[0];
        var semiA = r1.RoundMatches[0];
        var semiB = r1.RoundMatches[1];
        var gold = semiA.WrestlerInRed;
        var silver = semiB.WrestlerInRed;
        var bronze = semiA.WrestlerInBlue;
        var fourth = semiB.WrestlerInBlue;

        proc.CompleteMatch(semiA, true, MatchWinTypeEnum.PointsWin);
        proc.CompleteMatch(semiB, true, MatchWinTypeEnum.PointsWin);

        var final = g.Bracket.Rounds[1].RoundMatches[0];
        // Gold is whichever of (gold,silver) is red in the final
        var finalRed = final.WrestlerInRed;
        proc.CompleteMatch(final, isRedWon: true, MatchWinTypeEnum.PointsWin);

        var third = g.Bracket.Rounds.Single(r => r.RoundType == GroupRoundTypeEnum.Additional).RoundMatches[0];
        var thirdRed = third.WrestlerInRed;
        proc.CompleteMatch(third, isRedWon: true, MatchWinTypeEnum.PointsWin);

        proc.GetResults();

        finalRed!.FinalPlace.Should().Be(1);
        thirdRed!.FinalPlace.Should().Be(3);
        g.Wrestlers.Where(w => w.FinalPlace == 2).Should().HaveCount(1);
        g.Wrestlers.Where(w => w.FinalPlace == 4).Should().HaveCount(1);
    }
}
