using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.UI.Material.Model;
using Xunit;
using WTournament = Wrestling.Entities.Tournament;

namespace Wrestling.UI.Material.Tests;

public class MatchNumbersGeneratorTests
{
    private static List<IGroupBracketProcessor> BuiltInProcessors() => new()
    {
        new OlympicWithConsolationFromFinalistsGroupBracketProcessor(),
        new OlympicGroupBracketProcessor(),
        new RoundRobinGroupBracketProcessor(),
        new SubGroupsToOlympicBracketProcessor()
    };

    private static (WTournament, AgeWeightGroup, Carpet) BuildOneGroup(int wrestlers)
    {
        var group = new AgeWeightGroup
        {
            ID = Guid.NewGuid(),
            BirthYearMin = 2005, BirthYearMax = 2006, WeightMax = 60,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30
        };
        var ws = Enumerable.Range(0, wrestlers).Select(i => new Wrestler
        {
            ID = Guid.NewGuid(),
            FirstName = $"W{i}", LastName = $"Ф{i}",
            BirthDate = new DateTime(2005, 1, 1),
            Weight = 60, IsEntryFeePaid = true, IsWeightApproved = true,
            GroupID = group.ID,
            SeedNumber = i + 1
        }).ToList();
        group.Wrestlers = ws;

        var carpet = new Carpet { ID = Guid.NewGuid(), Name = "Carpet A" };
        carpet.Groups.Add(group);

        var t = new WTournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };
        t.Groups.Add(group);
        foreach (var w in ws) t.Wrestlers.Add(w);
        t.Carpets.Add(carpet);

        var proc = new OlympicGroupBracketProcessor();
        proc.Generate(t, group);
        group.Bracket.BracketTypeCode = BracketTypeEnum.Olympic.ToString();

        return (t, group, carpet);
    }

    [Fact]
    public void UniqueMatchNumbers_assigns_global_running_numbers_from_1()
    {
        var (t, group, _) = BuildOneGroup(4);
        var gen = new UniqueMatchNumbersGenerator();

        gen.Generate(t, BuiltInProcessors());

        var allMatches = group.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();
        var numbers = allMatches.Select(m => m.MatchNumber).ToList();
        numbers.Should().OnlyHaveUniqueItems();
        numbers.Should().OnlyContain(n => n >= 1 && n <= allMatches.Count);
    }

    [Fact]
    public void UniqueMatchNumbers_places_semifinals_before_third_place_and_final()
    {
        var (t, group, _) = BuildOneGroup(4);
        new UniqueMatchNumbersGenerator().Generate(t, BuiltInProcessors());

        var semis = group.Bracket.Rounds[0].RoundMatches;
        var thirdPlace = group.Bracket.Rounds.Single(r => r.RoundType == GroupRoundTypeEnum.Additional).RoundMatches[0];
        var final = group.Bracket.Rounds[1].RoundMatches[0];

        semis.Max(m => m.MatchNumber).Should().BeLessThan(final.MatchNumber);
        semis.Max(m => m.MatchNumber).Should().BeLessThan(thirdPlace.MatchNumber);
    }

    [Fact]
    public void CarpetMatchNumbers_restarts_from_1_for_each_carpet()
    {
        // Two identical groups, each on its own carpet
        var (t1, g1, _) = BuildOneGroup(4);

        var group2 = new AgeWeightGroup
        {
            ID = Guid.NewGuid(),
            BirthYearMin = 2005, BirthYearMax = 2006, WeightMax = 70,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30
        };
        var ws2 = Enumerable.Range(0, 4).Select(i => new Wrestler
        {
            ID = Guid.NewGuid(), FirstName = $"B{i}", LastName = $"F{i}",
            BirthDate = new DateTime(2005, 1, 1), Weight = 70,
            IsEntryFeePaid = true, IsWeightApproved = true,
            GroupID = group2.ID, SeedNumber = i + 1
        }).ToList();
        group2.Wrestlers = ws2;

        var carpet2 = new Carpet { ID = Guid.NewGuid(), Name = "Carpet B" };
        carpet2.Groups.Add(group2);
        t1.Groups.Add(group2);
        foreach (var w in ws2) t1.Wrestlers.Add(w);
        t1.Carpets.Add(carpet2);

        new OlympicGroupBracketProcessor().Generate(t1, group2);
        group2.Bracket.BracketTypeCode = BracketTypeEnum.Olympic.ToString();

        new CarpetMatchNumbersGenerator().Generate(t1, BuiltInProcessors());

        var g1Matches = g1.Bracket.Rounds.SelectMany(r => r.RoundMatches).Select(m => m.MatchNumber).ToList();
        var g2Matches = group2.Bracket.Rounds.SelectMany(r => r.RoundMatches).Select(m => m.MatchNumber).ToList();

        g1Matches.Should().Contain(1, "carpet A numbering starts at 1");
        g2Matches.Should().Contain(1, "carpet B numbering also starts at 1 (per carpet)");
    }
}
