using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Entities.Tests;

public class SubGroupsReplacementTests
{
    private static (Tournament T, AgeWeightGroup G, SubGroupsToOlympicBracketProcessor P) Setup(int count)
    {
        var g = TestHelpers.MakeGroup(count);
        var t = TestHelpers.MakeTournament(g);
        var p = new SubGroupsToOlympicBracketProcessor();
        p.Generate(t, g);
        foreach (var m in g.Bracket.MainRounds().SelectMany(r => r.RoundMatches).ToList())
            if (m.IsMatchCanStart) p.CompleteMatch(m, m.WrestlerInRed.SeedNumber < m.WrestlerInBlue.SeedNumber, MatchWinTypeEnum.PointsWin);
        return (t, g, p);
    }

    [Theory]
    [InlineData(6, MatchWinTypeEnum.MutualDisqualify)]
    [InlineData(6, MatchWinTypeEnum.MutualNoShow)]
    [InlineData(7, MatchWinTypeEnum.MutualDisqualify)]
    [InlineData(7, MatchWinTypeEnum.MutualNoShow)]
    public void Mutual_semifinal_uses_the_next_available_wrestler_from_each_pool(int count, MatchWinTypeEnum type)
    {
        var (_, g, p) = Setup(count);
        var semis = p.GetSemiFinalRound(g).RoundMatches;
        var first = semis[0];
        var originalRed = first.WrestlerInRed;
        var originalBlue = first.WrestlerInBlue;
        var reserved = semis.SelectMany(m => new[] { m.WrestlerInRed, m.WrestlerInBlue }).ToHashSet();
        var poolACount = count == 7 ? 4 : 3;
        var candidatesA = g.Wrestlers.Where(w => w.SeedNumber <= poolACount && !reserved.Contains(w)).ToList();
        var candidateB = g.Wrestlers.Single(w => w.SeedNumber > poolACount && !reserved.Contains(w));
        var version = g.BracketVersion;
        var identities = g.Bracket.Rounds.SelectMany(r => r.RoundMatches).Select(m => (m.BracketFullNumber, m.MatchNumber)).ToList();
        first.Version++;

        p.CompleteMatch(first, null, type);

        first.Status.Should().Be(MatchStatusEnum.Pending);
        candidatesA.Should().Contain(first.WrestlerInRed);
        first.WrestlerInRed.SeedNumber.Should().Be(3, "the next pool finisher must precede the fourth finisher");
        first.WrestlerInBlue.Should().BeSameAs(candidateB);
        first.PreviousResults.Should().ContainSingle().Which.Should().Match<WrestlingMatch>(m =>
            m.WrestlerInRed == originalRed && m.WrestlerInBlue == originalBlue && m.WinType == type
            && m.Status == MatchStatusEnum.Completed && m.IsRedWon == null);
        g.BracketVersion.Should().Be(version + 1);
        g.Bracket.Rounds.SelectMany(r => r.RoundMatches).Select(m => (m.BracketFullNumber, m.MatchNumber)).Should().Equal(identities);
        p.CanMatchBeReverted(g.Bracket.MainRounds().First().RoundMatches[0]).Should().BeFalse();
    }

    [Fact]
    public void Exhausted_pools_leave_an_empty_semifinal_and_the_other_finalist_gets_a_pass()
    {
        var (_, g, p) = Setup(6);
        var semis = p.GetSemiFinalRound(g).RoundMatches;
        var reserved = semis.SelectMany(m => new[] { m.WrestlerInRed, m.WrestlerInBlue }).ToHashSet();
        foreach (var candidate in g.Wrestlers.Where(w => !reserved.Contains(w))) candidate.IsNoShow = true;
        p.CompleteMatch(semis[0], null, MatchWinTypeEnum.MutualNoShow);
        semis[0].WinType.Should().Be(MatchWinTypeEnum.FreeWin);
        semis[0].WrestlerInRed.Should().BeNull();
        semis[0].WrestlerInBlue.Should().BeNull();

        p.CompleteMatch(semis[1], true, MatchWinTypeEnum.PointsWin);

        var final = p.GetFinalRound(g).RoundMatches.Single();
        final.WinType.Should().Be(MatchWinTypeEnum.FreeWin);
        new[] { final.WrestlerInRed, final.WrestlerInBlue }.Should().Contain(semis[1].WrestlerInRed);
        g.IsBracketCompleted.Should().BeTrue();
    }

    [Fact]
    public void Repeated_mutual_outcomes_do_not_reuse_eliminated_or_reserved_wrestlers()
    {
        var (_, g, p) = Setup(7);
        var sf = p.GetSemiFinalRound(g).RoundMatches[0];
        var other = p.GetSemiFinalRound(g).RoundMatches[1];
        p.CompleteMatch(sf, null, MatchWinTypeEnum.MutualDisqualify);
        var replacementPair = new[] { sf.WrestlerInRed, sf.WrestlerInBlue };
        p.CompleteMatch(sf, null, MatchWinTypeEnum.MutualNoShow);

        sf.PreviousResults.Should().HaveCount(2);
        sf.WinType.Should().Be(MatchWinTypeEnum.FreeWin);
        var survivor = sf.IsRedWon == true ? sf.WrestlerInRed : sf.WrestlerInBlue;
        replacementPair.Should().NotContain(survivor);
        new[] { other.WrestlerInRed, other.WrestlerInBlue }.Should().NotContain(survivor);
        survivor.IsPlaceless.Should().BeFalse();
    }

    [Theory]
    [InlineData(MatchWinTypeEnum.MutualDisqualify)]
    [InlineData(MatchWinTypeEnum.MutualNoShow)]
    public void Undoing_an_unplayed_replacement_restores_the_original_pair_and_clears_its_exclusion(MatchWinTypeEnum type)
    {
        var (_, g, p) = Setup(6);
        var sf = p.GetSemiFinalRound(g).RoundMatches[0];
        var red = sf.WrestlerInRed;
        var blue = sf.WrestlerInBlue;
        var version = g.BracketVersion;
        p.CompleteMatch(sf, null, type);

        p.CanMatchBeReverted(sf).Should().BeTrue();
        p.RevertMatch(sf);

        sf.Status.Should().Be(MatchStatusEnum.Pending);
        sf.WrestlerInRed.Should().BeSameAs(red);
        sf.WrestlerInBlue.Should().BeSameAs(blue);
        red.IsPlaceless.Should().BeFalse();
        blue.IsPlaceless.Should().BeFalse();
        sf.PreviousResults.Should().BeEmpty();
        g.BracketVersion.Should().Be(version + 2);
    }

    [Fact]
    public void One_exhausted_pool_gives_the_available_replacement_a_semifinal_pass()
    {
        var (_, g, p) = Setup(6);
        var semis = p.GetSemiFinalRound(g).RoundMatches;
        g.Wrestlers.Single(w => w.SeedNumber == 6).IsNoShow = true;
        p.CompleteMatch(semis[0], null, MatchWinTypeEnum.MutualDisqualify);
        semis[0].WinType.Should().Be(MatchWinTypeEnum.FreeWin);
        semis[0].WrestlerInRed.SeedNumber.Should().Be(3);
        semis[0].WrestlerInBlue.Should().BeNull();
        p.GetFinalRound(g).RoundMatches.Single().Status.Should().Be(MatchStatusEnum.Pending);
    }

    [Fact]
    public void A_replacement_preserves_the_other_semifinals_played_result()
    {
        var (_, g, p) = Setup(6);
        var semis = p.GetSemiFinalRound(g).RoundMatches;
        semis[1].PointsRed = 6;
        semis[1].PointsBlue = 2;
        p.CompleteMatch(semis[1], true, MatchWinTypeEnum.PointsWinWithPoints);
        var winner = semis[1].WrestlerInRed;
        p.CompleteMatch(semis[0], null, MatchWinTypeEnum.MutualNoShow);

        semis[1].Status.Should().Be(MatchStatusEnum.Completed);
        semis[1].PointsRed.Should().Be(6);
        semis[1].PointsBlue.Should().Be(2);
        semis[1].WrestlerInRed.Should().BeSameAs(winner);
        new[] { semis[0].WrestlerInRed, semis[0].WrestlerInBlue }.Should().NotContain(winner);
    }

    [Fact]
    public void Disqualification_can_be_cleared_through_multiple_replacement_results()
    {
        var (_, g, p) = Setup(7);
        var semi = p.GetSemiFinalRound(g).RoundMatches[0];
        var firstRed = semi.WrestlerInRed;
        var firstBlue = semi.WrestlerInBlue;
        p.CompleteMatch(semi, null, MatchWinTypeEnum.MutualDisqualify);
        var secondRed = semi.WrestlerInRed;
        var secondBlue = semi.WrestlerInBlue;
        p.CompleteMatch(semi, null, MatchWinTypeEnum.MutualDisqualify);

        p.ClearWrestlerDisqualify(firstRed);

        semi.PreviousResults.Should().BeEmpty();
        semi.Status.Should().Be(MatchStatusEnum.Pending);
        semi.WrestlerInRed.Should().BeSameAs(firstRed);
        semi.WrestlerInBlue.Should().BeSameAs(firstBlue);
        new[] { firstRed, firstBlue, secondRed, secondBlue }.Should().OnlyContain(w => !w.IsDisqualified);
    }

    [Fact]
    public void A_played_final_blocks_undoing_a_replacement_and_keeps_the_history_intact()
    {
        var (_, g, p) = Setup(6);
        var semis = p.GetSemiFinalRound(g).RoundMatches;
        var excluded = semis[0].WrestlerInRed;
        p.CompleteMatch(semis[0], null, MatchWinTypeEnum.MutualDisqualify);
        p.CompleteMatch(semis[0], true, MatchWinTypeEnum.PointsWin);
        p.CompleteMatch(semis[1], true, MatchWinTypeEnum.PointsWin);
        p.CompleteMatch(p.GetFinalRound(g).RoundMatches.Single(), true, MatchWinTypeEnum.PointsWin);

        p.CanMatchBeReverted(semis[0]).Should().BeFalse();
        p.CanClearReplacementDisqualification(excluded).Should().BeFalse();
        var action = () => p.ClearWrestlerDisqualify(excluded);
        action.Should().Throw<System.InvalidOperationException>();
        excluded.IsDisqualified.Should().BeTrue();
        semis[0].PreviousResults.Should().ContainSingle();
    }

    [Fact]
    public void A_waiting_replacement_does_not_award_final_places_from_pool_standings()
    {
        var (_, g, p) = Setup(6);
        p.CompleteMatch(p.GetSemiFinalRound(g).RoundMatches[0], null, MatchWinTypeEnum.MutualNoShow);

        p.GetResults();

        g.Wrestlers.Should().OnlyContain(w => !w.FinalPlace.HasValue);
    }
}
