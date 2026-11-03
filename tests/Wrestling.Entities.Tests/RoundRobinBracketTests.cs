using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Entities.Tests;

public class RoundRobinBracketTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Two_bronzes_option_changes_only_fourth_place_and_can_be_reversed(int count)
    {
        var group = TestHelpers.MakeGroup(count);
        var tournament = TestHelpers.MakeTournament(group);
        var processor = new RoundRobinGroupBracketProcessor();
        processor.Generate(tournament, group);
        foreach (var match in group.Bracket.Rounds.SelectMany(r => r.RoundMatches))
            processor.CompleteMatch(match, match.WrestlerInRed.SeedNumber < match.WrestlerInBlue.SeedNumber, MatchWinTypeEnum.PointsWin);

        processor.GetResults().ToList();
        var ordered = group.Wrestlers.OrderBy(w => w.SeedNumber).ToList();
        ordered.Select(w => w.FinalPlace).Should().Equal(Enumerable.Range(1, count).Select(i => (int?)i));

        tournament.Settings.IsRoundRobinTwoThirdPlaces = true;
        processor.GetResults().ToList();
        ordered.Select(w => w.FinalPlace).Should().Equal(Enumerable.Range(1, count).Select(i => (int?)(i == 4 ? 3 : i)));
        processor.GetResults().ToList();
        ordered.Count(w => w.FinalPlace == 3).Should().Be(count >= 4 ? 2 : count == 3 ? 1 : 0);

        tournament.Settings.IsRoundRobinTwoThirdPlaces = false;
        processor.GetResults().ToList();
        ordered.Select(w => w.FinalPlace).Should().Equal(Enumerable.Range(1, count).Select(i => (int?)i));
    }

    [Fact]
    public void Two_bronzes_does_not_award_provisional_places_in_unfinished_round_robin()
    {
        var group = TestHelpers.MakeGroup(4);
        var tournament = TestHelpers.MakeTournament(group);
        tournament.Settings.IsRoundRobinTwoThirdPlaces = true;
        var processor = new RoundRobinGroupBracketProcessor();
        processor.Generate(tournament, group);
        processor.CompleteMatch(group.Bracket.Rounds[0].RoundMatches[0], true, MatchWinTypeEnum.PointsWin);
        processor.GetResults().Should().OnlyContain(r => !r.Wrestler.FinalPlace.HasValue);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    public void Two_bronzes_setting_does_not_change_subgroup_advancement_or_placements(int count)
    {
        var group = TestHelpers.MakeGroup(count);
        var tournament = TestHelpers.MakeTournament(group);
        var processor = new SubGroupsToOlympicBracketProcessor();
        processor.Generate(tournament, group);
        for (var step = 0; step < 40; step++)
        {
            var match = group.Bracket.Rounds.SelectMany(r => r.RoundMatches).FirstOrDefault(m => m.IsMatchCanStart);
            if (match == null) break;
            processor.CompleteMatch(match, true, MatchWinTypeEnum.PointsWin);
        }
        group.IsBracketCompleted.Should().BeTrue();
        processor.GetResults().ToList();
        var places = group.Wrestlers.ToDictionary(w => w.ID, w => w.FinalPlace);
        tournament.Settings.IsRoundRobinTwoThirdPlaces = true;
        processor.GetResults().ToList();
        group.Wrestlers.ToDictionary(w => w.ID, w => w.FinalPlace).Should().BeEquivalentTo(places);
    }

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

    // Genuine 2-way head-to-head: a 4-wrestler round-robin where {a,b} tie at
    // 2 wins / 6 CP and {c,d} tie at 1 win / 3 CP. Every other UWW criterion is
    // equal (all plain PointsWin, no points recorded → Tushe/Domination/points
    // for+against all zero), so ONLY the direct result can separate each pair.
    // Seeds are arranged to CONTRADICT the head-to-head outcome (b and d hold
    // the lower seeds), so a pass proves head-to-head overrides the SeedNumber
    // fallback — which the previous 3-wrestler version never actually reached.
    [Fact]
    public void Two_way_tie_on_all_measurable_criteria_breaks_by_head_to_head_not_seed()
    {
        var (g, proc) = Setup(4);

        var w = g.Wrestlers.OrderBy(x => x.SeedNumber).ToList();
        var a = w[0];
        var b = w[1];
        var c = w[2];
        var d = w[3];

        // Seed order would put b ahead of a and d ahead of c — the opposite of
        // the head-to-head results below.
        a.SeedNumber = 2; b.SeedNumber = 1;
        c.SeedNumber = 4; d.SeedNumber = 3;

        var matches = g.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();

        // a: beats b, beats c, loses to d  → 2 wins, 6 CP
        // b: beats c, beats d, loses to a  → 2 wins, 6 CP
        // c: beats d, loses to a, loses to b → 1 win, 3 CP
        // d: beats a, loses to b, loses to c → 1 win, 3 CP
        CompleteByPair(matches, proc, a, b, MatchWinTypeEnum.PointsWin);
        CompleteByPair(matches, proc, a, c, MatchWinTypeEnum.PointsWin);
        CompleteByPair(matches, proc, d, a, MatchWinTypeEnum.PointsWin);
        CompleteByPair(matches, proc, b, c, MatchWinTypeEnum.PointsWin);
        CompleteByPair(matches, proc, b, d, MatchWinTypeEnum.PointsWin);
        CompleteByPair(matches, proc, c, d, MatchWinTypeEnum.PointsWin);

        proc.GetResults();

        a.FinalPlace.Should().Be(1, "a beat b head-to-head; the pair ties on wins and CP, so head-to-head decides despite b's lower seed");
        b.FinalPlace.Should().Be(2);
        c.FinalPlace.Should().Be(3, "c beat d head-to-head; the pair ties on wins and CP");
        d.FinalPlace.Should().Be(4);
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
