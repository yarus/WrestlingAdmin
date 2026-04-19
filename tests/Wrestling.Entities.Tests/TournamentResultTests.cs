using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Entities.Results;
using Xunit;

namespace Wrestling.Entities.Tests;

public class TournamentResultTests
{
    [Fact]
    public void Wins_and_losses_count_completed_matches_only()
    {
        var group = TestHelpers.MakeGroup(4);
        var tournament = TestHelpers.MakeTournament(group);
        var proc = new OlympicGroupBracketProcessor();
        proc.Generate(tournament, group);

        var r1 = group.Bracket.Rounds[0];
        var semiA = r1.RoundMatches[0];
        proc.CompleteMatch(semiA, isRedWon: true, MatchWinTypeEnum.PointsWin);

        var winnerResult = new TournamentResult(group, semiA.WrestlerInRed!);
        var loserResult = new TournamentResult(group, semiA.WrestlerInBlue!);

        winnerResult.Wins.Should().BeGreaterOrEqualTo(1);
        loserResult.Loses.Should().BeGreaterOrEqualTo(1);
    }

    [Theory]
    [InlineData(1, 25)]
    [InlineData(2, 20)]
    [InlineData(3, 15)]
    [InlineData(4, 12)]
    [InlineData(5, 10)]
    [InlineData(11, 0)]
    [InlineData(null, 0)]
    public void TotalPoints_from_final_place_uses_local_event_table(int? place, int expected)
    {
        var group = TestHelpers.MakeGroup(1);
        var wrestler = group.Wrestlers[0];
        wrestler.FinalPlace = place;

        var result = new TournamentResult(group, wrestler);
        result.TotalPoints.Should().Be(expected);
    }

    [Fact]
    public void AllGainedPoints_sums_match_points_across_both_corners()
    {
        var group = TestHelpers.MakeGroup(2);
        var tournament = TestHelpers.MakeTournament(group);
        var proc = new RoundRobinGroupBracketProcessor();
        proc.Generate(tournament, group);

        var match = group.Bracket.Rounds[0].RoundMatches[0];
        match.PointsRed = 5;
        match.PointsBlue = 3;
        proc.CompleteMatch(match, isRedWon: true, MatchWinTypeEnum.PointsWinWithPoints);

        var redResult = new TournamentResult(group, match.WrestlerInRed!);
        redResult.AllGainedPoints.Should().Be(5);
        redResult.AllLostPoints.Should().Be(3);
    }
}
