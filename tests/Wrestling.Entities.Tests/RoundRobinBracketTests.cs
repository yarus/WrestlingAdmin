using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Entities.Tests;

public class RoundRobinBracketTests
{
    private static (AgeWeightGroup, RoundRobinGroupBracketProcessor) Setup(int wrestlers)
    {
        var group = TestHelpers.MakeGroup(wrestlers);
        var tournament = TestHelpers.MakeTournament(group);
        var proc = new RoundRobinGroupBracketProcessor();
        proc.Generate(tournament, group);
        return (group, proc);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 3)]
    [InlineData(4, 6)]
    [InlineData(5, 10)]
    public void Round_robin_generates_N_choose_2_matches(int wrestlers, int expectedMatches)
    {
        var (g, _) = Setup(wrestlers);
        g.Bracket.MatchesCount.Should().Be(expectedMatches);
    }

    [Fact]
    public void Every_pair_of_wrestlers_meets_exactly_once()
    {
        var (g, _) = Setup(4);

        var matches = g.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();
        var pairs = matches
            .Select(m => new[] { m.WrestlerInRed!.ID, m.WrestlerInBlue!.ID }.OrderBy(id => id).ToArray())
            .Select(ids => $"{ids[0]}|{ids[1]}")
            .ToList();

        pairs.Should().OnlyHaveUniqueItems();
        pairs.Should().HaveCount(6);
    }

    [Fact]
    public void Results_rank_wrestlers_by_wins_desc()
    {
        var (g, proc) = Setup(3);

        // Deterministic outcomes: first listed wrestler always wins the red side
        foreach (var match in g.Bracket.Rounds.SelectMany(r => r.RoundMatches))
        {
            proc.CompleteMatch(match, isRedWon: true, MatchWinTypeEnum.PointsWin);
        }

        proc.GetResults();

        var ranked = g.Wrestlers.Where(w => w.FinalPlace.HasValue).OrderBy(w => w.FinalPlace).ToList();
        ranked.Should().HaveCount(3);
        ranked[0].FinalPlace.Should().Be(1);
        ranked[1].FinalPlace.Should().Be(2);
        ranked[2].FinalPlace.Should().Be(3);
    }

    [Fact]
    public void Bye_matches_are_removed_for_odd_wrestler_count()
    {
        var (g, _) = Setup(3);

        var matches = g.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();
        matches.Should().NotContain(m =>
            (m.WrestlerInRed != null && m.WrestlerInRed.LastName == "Bye") ||
            (m.WrestlerInBlue != null && m.WrestlerInBlue.LastName == "Bye"));
    }
}
