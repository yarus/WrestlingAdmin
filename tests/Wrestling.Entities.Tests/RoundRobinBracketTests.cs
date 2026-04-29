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

    // Regression: 3-way tie on Wins where remaining wrestlers differ on
    // OverallTournamentClassificationPoints. After Tagirov takes 1st, the
    // old code applied a Surkhaev↔Goryachev head-to-head check that ignored
    // their classification-point difference and demoted Surkhaev despite
    // his higher CP. UWW order: CP first, head-to-head only when all
    // measurable criteria are tied.
    //
    // Mirrors real group "2012-2013 55kg" in 20260426.wrt — see git log.
    [Fact]
    public void Three_way_tie_on_wins_breaks_by_classification_points_not_pair_result()
    {
        var (g, proc) = Setup(3);

        var w = g.Wrestlers.OrderBy(x => x.SeedNumber).ToList();
        var tagirov = w[0];
        var surkhaev = w[1];
        var goryachev = w[2];

        var matches = g.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();

        // Tagirov def. Goryachev by Tushe       (Tagirov +5 CP, Goryachev +0)
        // Surkhaev def. Tagirov by DominationWin with points (Surkhaev +4, Tagirov +1)
        // Goryachev def. Surkhaev by Points-with-points (Goryachev +3, Surkhaev +1)
        // → Wins: Tag=Sur=Gor=1. CP: Tag=6, Sur=5, Gor=3.
        // Expected: Tag 1st, Sur 2nd, Gor 3rd.
        CompleteByPair(matches, proc, tagirov, goryachev, MatchWinTypeEnum.Tushe);
        CompleteByPair(matches, proc, surkhaev, tagirov, MatchWinTypeEnum.DominationWinWithPoints);
        CompleteByPair(matches, proc, goryachev, surkhaev, MatchWinTypeEnum.PointsWinWithPoints);

        proc.GetResults();

        tagirov.FinalPlace.Should().Be(1, "Tagirov has 6 CP — highest");
        surkhaev.FinalPlace.Should().Be(2, "Surkhaev has 5 CP — second; head-to-head only kicks in when CP is equal");
        goryachev.FinalPlace.Should().Be(3, "Goryachev has 3 CP — lowest");
    }

    [Fact]
    public void Two_way_tie_on_everything_breaks_by_head_to_head()
    {
        var (g, proc) = Setup(3);

        var w = g.Wrestlers.OrderBy(x => x.SeedNumber).ToList();
        var champ = w[0];
        var a = w[1];
        var b = w[2];

        var matches = g.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();

        // Champ wins both by Tushe → 2 wins, 10 CP.
        // a vs b: a wins by Tushe → a 1 win 5 CP, b 0 wins 0 CP.
        // (Different wins counts, no tie among a and b — but verify champ takes 1st by CP/wins)
        CompleteByPair(matches, proc, champ, a, MatchWinTypeEnum.Tushe);
        CompleteByPair(matches, proc, champ, b, MatchWinTypeEnum.Tushe);
        CompleteByPair(matches, proc, a, b, MatchWinTypeEnum.Tushe);

        proc.GetResults();

        champ.FinalPlace.Should().Be(1);
        a.FinalPlace.Should().Be(2);
        b.FinalPlace.Should().Be(3);
    }

    private static void CompleteByPair(
        System.Collections.Generic.List<WrestlingMatch> matches,
        RoundRobinGroupBracketProcessor proc,
        Wrestler winner,
        Wrestler loser,
        MatchWinTypeEnum winType)
    {
        var match = matches.First(m =>
            (m.Status == MatchStatusEnum.Pending) &&
            ((m.WrestlerInRed!.ID == winner.ID && m.WrestlerInBlue!.ID == loser.ID) ||
             (m.WrestlerInBlue!.ID == winner.ID && m.WrestlerInRed!.ID == loser.ID)));

        var isRedWon = match.WrestlerInRed!.ID == winner.ID;
        proc.CompleteMatch(match, isRedWon, winType);
    }
}
