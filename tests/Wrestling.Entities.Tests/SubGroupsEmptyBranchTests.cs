using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Entities.Tests;

public class SubGroupsEmptyBranchTests
{
    public static IEnumerable<object[]> MutualOutcomes => new[]
    {
        new object[] { MatchWinTypeEnum.MutualDisqualify },
        new object[] { MatchWinTypeEnum.MutualNoShow },
        new object[] { MatchWinTypeEnum.MutualInjury }
    };

    private static (Tournament T, AgeWeightGroup G, SubGroupsToOlympicBracketProcessor P) Setup()
    {
        var g = TestHelpers.MakeGroup(6);
        var t = TestHelpers.MakeTournament(g);
        var p = new SubGroupsToOlympicBracketProcessor();
        p.Generate(t, g);
        return (t, g, p);
    }

    private static List<WrestlingMatch> Matches(AgeWeightGroup g) => g.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();

    private static void CompletePools(AgeWeightGroup g, SubGroupsToOlympicBracketProcessor p, MatchWinTypeEnum type, bool bothPools = false)
    {
        var first = g.Bracket.MainRounds().First().RoundMatches;
        p.CompleteMatch(first[0], null, type);
        if (bothPools) p.CompleteMatch(first[1], null, type);
        while (g.Bracket.MainRounds().SelectMany(r => r.RoundMatches).Any(m => m.IsMatchCanStart))
        {
            var m = g.Bracket.MainRounds().SelectMany(r => r.RoundMatches).First(m => m.IsMatchCanStart);
            p.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);
        }
    }

    [Theory]
    [MemberData(nameof(MutualOutcomes))]
    public void One_survivor_in_a_pool_does_not_leave_an_unplayable_bronze_match(MatchWinTypeEnum type)
    {
        var (t, g, p) = Setup();
        CompletePools(g, p, type);
        int played = 0;
        while (Matches(g).Any(m => m.IsMatchCanStart) && played++ < 20)
            p.CompleteMatch(Matches(g).First(m => m.IsMatchCanStart), true, MatchWinTypeEnum.PointsWin);

        g.IsBracketCompleted.Should().BeTrue();
        var bronze = p.Get3rdPlaceRound(g).RoundMatches.Single();
        bronze.WinType.Should().Be(MatchWinTypeEnum.FreeWin);
        p.GetResults();
        var winner = bronze.IsRedWon == true ? bronze.WrestlerInRed : bronze.WrestlerInBlue;
        winner.FinalPlace.Should().Be(3);
        g.Wrestlers.Where(w => w.IsPlaceless).Should().OnlyContain(w => !w.FinalPlace.HasValue);
    }

    [Theory]
    [InlineData(MatchWinTypeEnum.MutualDisqualify)]
    [InlineData(MatchWinTypeEnum.MutualNoShow)]
    public void Two_depleted_pools_resolve_the_empty_bronze_without_a_fictitious_winner(MatchWinTypeEnum type)
    {
        var (t, g, p) = Setup();
        CompletePools(g, p, type, true);
        var bronze = p.Get3rdPlaceRound(g).RoundMatches.Single();
        bronze.Status.Should().Be(MatchStatusEnum.Completed);
        bronze.IsRedWon.Should().BeNull();
        bronze.WrestlerInRed.Should().BeNull();
        bronze.WrestlerInBlue.Should().BeNull();
        bronze.HasViewableResults.Should().BeFalse();
        var final = p.GetFinalRound(g).RoundMatches.Single();
        final.IsMatchCanStart.Should().BeTrue();
        p.CompleteMatch(final, true, MatchWinTypeEnum.PointsWin);
        p.GetResults();
        g.IsBracketCompleted.Should().BeTrue();
        g.Wrestlers.Count(w => w.FinalPlace == 3).Should().Be(0);
        g.Wrestlers.Where(w => !w.IsPlaceless).Select(w => w.FinalPlace).Should().BeEquivalentTo(new int?[] { 1, 2 });
    }

    [Fact]
    public void A_pending_source_prevents_early_completion_of_a_medal_match()
    {
        var (_, g, p) = Setup();
        foreach (var m in g.Bracket.MainRounds().SelectMany(r => r.RoundMatches))
            if (m.IsMatchCanStart) p.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);
        var sf = p.GetSemiFinalRound(g).RoundMatches;
        p.CompleteMatch(sf[0], true, MatchWinTypeEnum.PointsWin);
        p.Load(TestHelpers.MakeTournament(g), g);

        p.GetFinalRound(g).RoundMatches.Single().Status.Should().Be(MatchStatusEnum.Pending);
        p.Get3rdPlaceRound(g).RoundMatches.Single().Status.Should().Be(MatchStatusEnum.Pending);
        sf[1].IsMatchCanStart.Should().BeTrue();
    }

    [Fact]
    public void Reverting_a_semifinal_removes_a_dependent_automatic_bronze_and_can_be_replayed()
    {
        var (_, g, p) = Setup();
        CompletePools(g, p, MatchWinTypeEnum.MutualDisqualify);
        var sf = p.GetSemiFinalRound(g).RoundMatches.Single(m => m.IsMatchCanStart);
        p.CompleteMatch(sf, true, MatchWinTypeEnum.PointsWin);
        var bronze = p.Get3rdPlaceRound(g).RoundMatches.Single();
        bronze.Status.Should().Be(MatchStatusEnum.Completed);

        p.CanMatchBeReverted(sf).Should().BeTrue();
        p.RevertMatch(sf);
        bronze.Status.Should().Be(MatchStatusEnum.Pending);
        bronze.WinType.Should().BeNull();
        p.CompleteMatch(sf, false, MatchWinTypeEnum.PointsWin);
        bronze.Status.Should().Be(MatchStatusEnum.Completed);
        new[] { bronze.WrestlerInRed, bronze.WrestlerInBlue }.Should().Contain(sf.WrestlerInRed).And.NotContain(sf.WrestlerInBlue);
    }

    [Fact]
    public void Loading_a_legacy_hanging_bronze_repairs_only_the_resolved_branch()
    {
        var (t, g, p) = Setup();
        CompletePools(g, p, MatchWinTypeEnum.MutualDisqualify);
        var sf = p.GetSemiFinalRound(g).RoundMatches.Single(m => m.IsMatchCanStart);
        p.CompleteMatch(sf, true, MatchWinTypeEnum.PointsWin);
        var bronze = p.Get3rdPlaceRound(g).RoundMatches.Single();
        bronze.Status = MatchStatusEnum.Pending; bronze.WinType = null; bronze.IsRedWon = null;
        var identities = Matches(g).Select(m => (m.BracketFullNumber, m.MatchNumber)).ToList();
        var version = g.BracketVersion;

        p.Load(t, g);
        p.Load(t, g);

        bronze.Status.Should().Be(MatchStatusEnum.Completed);
        bronze.WinType.Should().Be(MatchWinTypeEnum.FreeWin);
        Matches(g).Select(m => (m.BracketFullNumber, m.MatchNumber)).Should().Equal(identities);
        g.BracketVersion.Should().Be(version);
        p.GetFinalRound(g).RoundMatches.Single().IsMatchCanStart.Should().BeTrue();
    }

    [Fact]
    public void Reverting_a_pool_result_clears_automatic_semifinals_without_regenerating_the_draw()
    {
        var (_, g, p) = Setup();
        var original = g.Bracket.MainRounds().First().RoundMatches[0];
        CompletePools(g, p, MatchWinTypeEnum.MutualDisqualify);
        p.GetSemiFinalRound(g).RoundMatches.Should().Contain(m => m.WinType == MatchWinTypeEnum.FreeWin);
        var bracket = g.Bracket;

        p.CanMatchBeReverted(original).Should().BeTrue();
        p.RevertMatch(original);

        g.Bracket.Should().BeSameAs(bracket);
        original.Status.Should().Be(MatchStatusEnum.Pending);
        original.WrestlerInRed.IsDisqualified.Should().BeFalse();
        original.WrestlerInBlue.IsDisqualified.Should().BeFalse();
        g.Bracket.AdditionalRounds().SelectMany(r => r.RoundMatches).Should().OnlyContain(m =>
            m.Status == MatchStatusEnum.Pending && m.WrestlerInRed == null && m.WrestlerInBlue == null && m.WinType == null);
        g.Bracket.CompletedMatchesCount.Should().Be(Matches(g).Count(m => m.IsMatchCompleted));
    }

    [Fact]
    public void Mutual_no_show_in_the_final_promotes_the_bronze_pair_like_mutual_disqualification()
    {
        var (_, g, p) = Setup();
        foreach (var m in g.Bracket.MainRounds().SelectMany(r => r.RoundMatches))
            if (m.IsMatchCanStart) p.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);
        foreach (var sf in p.GetSemiFinalRound(g).RoundMatches)
            p.CompleteMatch(sf, true, MatchWinTypeEnum.PointsWin);
        var final = p.GetFinalRound(g).RoundMatches.Single();
        p.CompleteMatch(final, null, MatchWinTypeEnum.MutualNoShow);
        var bronze = p.Get3rdPlaceRound(g).RoundMatches.Single();
        p.CompleteMatch(bronze, true, MatchWinTypeEnum.PointsWin);
        p.GetResults();

        bronze.WrestlerInRed.FinalPlace.Should().Be(1);
        bronze.WrestlerInBlue.FinalPlace.Should().Be(2);
        final.WrestlerInRed.FinalPlace.Should().BeNull();
        final.WrestlerInBlue.FinalPlace.Should().BeNull();
    }

    [Fact]
    public void Warnings_disqualification_in_the_final_promotes_the_bronze_pair()
    {
        var (_, g, p) = Setup();
        foreach (var m in g.Bracket.MainRounds().SelectMany(r => r.RoundMatches))
            if (m.IsMatchCanStart) p.CompleteMatch(m, true, MatchWinTypeEnum.PointsWin);
        foreach (var sf in p.GetSemiFinalRound(g).RoundMatches)
            p.CompleteMatch(sf, true, MatchWinTypeEnum.PointsWin);
        var final = p.GetFinalRound(g).RoundMatches.Single();
        p.CompleteMatch(final, true, MatchWinTypeEnum.WarningsLimit);
        var bronze = p.Get3rdPlaceRound(g).RoundMatches.Single();
        p.CompleteMatch(bronze, true, MatchWinTypeEnum.PointsWin);
        p.GetResults();

        final.WrestlerInRed.FinalPlace.Should().Be(1);
        final.WrestlerInBlue.FinalPlace.Should().BeNull();
        bronze.WrestlerInRed.FinalPlace.Should().Be(2);
        bronze.WrestlerInBlue.FinalPlace.Should().Be(3);
    }
}
