using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities.Bracket;
using Xunit;

namespace Wrestling.Entities.Tests;

public class SharedInjuryPlaceTests
{
    public static IEnumerable<object[]> MedalCases()
    {
        foreach (var format in new[] { "subgroups6", "subgroups7", "olympic", "consolation" })
        foreach (var bronze in new[] { false, true })
            yield return new object[] { format, bronze };
    }

    private static (Tournament T, AgeWeightGroup G, IGroupBracketProcessor P) Setup(string format)
    {
        var g = TestHelpers.MakeGroup(format == "subgroups6" ? 6 : format == "subgroups7" ? 7 : 8);
        var t = TestHelpers.MakeTournament(g);
        IGroupBracketProcessor p = format switch
        {
            "olympic" => new OlympicGroupBracketProcessor(),
            "consolation" => new OlympicWithConsolationFromFinalistsGroupBracketProcessor(),
            _ => new SubGroupsToOlympicBracketProcessor()
        };
        p.Generate(t, g);
        return (t, g, p);
    }

    private static void Finish(Tournament t, AgeWeightGroup g, IGroupBracketProcessor p, WrestlingMatch target, MatchWinTypeEnum outcome)
    {
        for (var step = 0; step < 100; step++)
        {
            var available = g.Bracket.Rounds.SelectMany(r => r.RoundMatches).Where(m => m.IsMatchCanStart).ToList();
            if (available.Count == 0) break;
            var match = available.Contains(target) ? target : available.First();
            p.CompleteMatch(match, match == target ? null : true, match == target ? outcome : MatchWinTypeEnum.PointsWin);
            p.Load(t, g);
            p.GetResults().ToList();
        }
        g.IsBracketCompleted.Should().BeTrue();
        p.GetResults().ToList();
    }

    [Theory]
    [MemberData(nameof(MedalCases))]
    public void Mutual_injury_shares_the_medal_place_and_can_be_reverted(string format, bool bronze)
    {
        var (t, g, p) = Setup(format);
        var target = bronze ? p.Get3rdPlaceRound(g).RoundMatches.First() : p.GetFinalRound(g).RoundMatches.Single();
        Finish(t, g, p, target, MatchWinTypeEnum.MutualInjury);

        target.WrestlerInRed.FinalPlace.Should().Be(bronze ? 3 : 1);
        target.WrestlerInBlue.FinalPlace.Should().Be(bronze ? 3 : 1);
        g.Wrestlers.Should().OnlyContain(w => !w.IsPlaceless && w.FinalPlace >= 1 && w.FinalPlace <= g.Wrestlers.Count);
        p.GetResults().ToList();
        target.WrestlerInRed.FinalPlace.Should().Be(target.WrestlerInBlue.FinalPlace);

        p.CanMatchBeReverted(target).Should().BeTrue();
        p.RevertMatch(target);
        p.CompleteMatch(target, true, MatchWinTypeEnum.PointsWin);
        p.GetResults().ToList();
        target.WrestlerInRed.FinalPlace.Should().Be(bronze ? 3 : 1);
        target.WrestlerInBlue.FinalPlace.Should().Be(bronze ? (format == "consolation" ? 5 : 4) : 2);
    }

    [Theory]
    [InlineData(MatchWinTypeEnum.MutualInjury)]
    [InlineData(MatchWinTypeEnum.MutualDisqualify)]
    [InlineData(MatchWinTypeEnum.MutualNoShow)]
    public void Depleted_consolation_does_not_assign_places_beyond_the_category_size(MatchWinTypeEnum outcome)
    {
        var (t, g, p) = Setup("consolation");
        var target = g.Bracket.MainRounds().First().RoundMatches.First(m => m.IsMatchCanStart);
        Finish(t, g, p, target, outcome);
        g.Wrestlers.Where(w => !w.IsPlaceless).Should().OnlyContain(w => w.FinalPlace >= 1 && w.FinalPlace <= g.Wrestlers.Count);
        g.Wrestlers.Should().NotContain(w => w.IsPlaceless && w.FinalPlace.HasValue);
    }
}
