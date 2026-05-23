using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities.Bracket;
using Wrestling.Entities.Bracket.Seeding;
using Xunit;

namespace Wrestling.Entities.Tests;

public class SeedingStrategyTests
{
    private static Wrestler Make(string last, Guid? teamId = null, string city = null, string level = null)
    {
        return new Wrestler
        {
            ID = Guid.NewGuid(),
            LastName = last,
            FirstName = "X",
            BirthDate = new DateTime(2010, 1, 1),
            TeamID = teamId,
            TeamCity = city,
            Level = WrestlerLevelLabels.FromString(level),
        };
    }

    private static AgeWeightGroup MakeGroupWithWrestlers(IEnumerable<Wrestler> wrestlers, string bracketCode = null)
    {
        var g = new AgeWeightGroup
        {
            ID = Guid.NewGuid(),
            BirthYearMin = 2010, BirthYearMax = 2011, WeightMax = 40,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30,
        };
        var list = new List<Wrestler>(wrestlers);
        foreach (var w in list) w.GroupID = g.ID;
        g.Wrestlers = list;
        if (bracketCode != null)
        {
            g.Bracket = new GroupBracket { BracketTypeCode = bracketCode, BracketTypeLabel = bracketCode };
        }
        return g;
    }

    [Fact]
    public void Seed_AssignsContiguous1ToN_ForOlympicGroup()
    {
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var wrestlers = Enumerable.Range(1, 8)
            .Select(i => Make($"W{i:D2}", i <= 4 ? teamA : teamB, city: i <= 4 ? "CityA" : "CityB"))
            .ToList();
        var g = MakeGroupWithWrestlers(wrestlers, BracketTypeEnum.OlympicConsilationFinalists.ToString());

        new ClubCityLevelSeedingStrategy().Seed(g);

        var seeds = g.Wrestlers.Select(w => w.SeedNumber).ToList();
        seeds.Should().BeEquivalentTo(Enumerable.Range(1, 8).Select(i => (int?)i));
        // Seed does not mutate IsSeedFixed — callers (DrawViewModel.RegenerateBrackets /
        // .GenerateBracket) lock wrestlers only after an explicit draw action.
        g.Wrestlers.Should().OnlyContain(w => w.IsSeedFixed == false);
    }

    [Fact]
    public void Seed_LockedWrestlers_KeepTheirSlot()
    {
        var wrestlers = Enumerable.Range(1, 8).Select(i => Make($"W{i:D2}")).ToList();
        // Lock W03 at seed 3
        wrestlers[2].IsSeedFixed = true;
        wrestlers[2].SeedNumber = 3;
        var g = MakeGroupWithWrestlers(wrestlers, BracketTypeEnum.OlympicConsilationFinalists.ToString());

        new ClubCityLevelSeedingStrategy().Seed(g);

        var w03 = g.Wrestlers.Single(w => w.LastName == "W03");
        w03.SeedNumber.Should().Be(3);
    }

    [Fact]
    public void Seed_SpreadsSameClubAcrossBracket_ForOlympic()
    {
        // 4 wrestlers from clubA, 4 from clubB: ideal placement has each pair
        // of round-1 slots mixing one from each club → no round-1 same-club match.
        var clubA = Guid.NewGuid();
        var clubB = Guid.NewGuid();
        var wrestlers = new List<Wrestler>
        {
            Make("A1", clubA), Make("A2", clubA), Make("A3", clubA), Make("A4", clubA),
            Make("B1", clubB), Make("B2", clubB), Make("B3", clubB), Make("B4", clubB),
        };
        var g = MakeGroupWithWrestlers(wrestlers, BracketTypeEnum.OlympicConsilationFinalists.ToString());

        new ClubCityLevelSeedingStrategy().Seed(g);

        // Round-1 pairs for N=8: (1,2)(3,4)(5,6)(7,8). Each pair should have
        // one wrestler from clubA and one from clubB.
        var bySeed = g.Wrestlers.ToDictionary(w => w.SeedNumber!.Value);
        for (int i = 1; i <= 7; i += 2)
        {
            (bySeed[i].TeamID == bySeed[i + 1].TeamID).Should().BeFalse(
                $"seeds {i} and {i + 1} (round-1 pair) must come from different clubs");
        }
    }

    [Fact]
    public void Seed_SubGroups_SplitsSameClubAcrossAAndB()
    {
        // 2 wrestlers from the same club should end up in different subgroups.
        var club = Guid.NewGuid();
        var wrestlers = new List<Wrestler>
        {
            Make("S1"), Make("S2"), Make("S3"),
            Make("S4"), Make("X1", club), Make("X2", club),
        };
        var g = MakeGroupWithWrestlers(wrestlers, BracketTypeEnum.SubGroupsIntoOlympic.ToString());

        new ClubCityLevelSeedingStrategy().Seed(g);

        var bySeed = g.Wrestlers.ToDictionary(w => w.SeedNumber!.Value);
        // Subgroup A = seeds 1..3, Subgroup B = seeds 4..6
        var xSeeds = new[] { "X1", "X2" }.Select(n => bySeed.Values.Single(w => w.LastName == n).SeedNumber!.Value).OrderBy(s => s).ToList();
        bool splitAcrossSubgroups = xSeeds[0] <= 3 && xSeeds[1] >= 4;
        splitAcrossSubgroups.Should().BeTrue("two same-club wrestlers must land in different subgroups");
    }

    [Fact]
    public void Seed_RoundRobin_SortsByLevelDescending()
    {
        var wrestlers = new List<Wrestler>
        {
            Make("Low", level: "б/р"),
            Make("Mid", level: "II юн"),
            Make("Top", level: "КМС"),
        };
        var g = MakeGroupWithWrestlers(wrestlers, BracketTypeEnum.RoundRobin.ToString());

        new ClubCityLevelSeedingStrategy().Seed(g);

        g.Wrestlers[0].LastName.Should().Be("Top");
        g.Wrestlers[1].LastName.Should().Be("Mid");
        g.Wrestlers[2].LastName.Should().Be("Low");
    }

    [Fact]
    public void Seed_RoundRobin_AdultRanksOutrankJuniorRanks()
    {
        // Even the lowest adult rank (III) should outrank the highest junior
        // rank (I юн) — this is how the official Russian classification works.
        var wrestlers = new List<Wrestler>
        {
            Make("JuniorI", level: "I юн"),
            Make("AdultIII", level: "III"),
            Make("AdultI", level: "I"),
            Make("JuniorIII", level: "III юн"),
            Make("MSMK", level: "МСМК"),
        };
        var g = MakeGroupWithWrestlers(wrestlers, BracketTypeEnum.RoundRobin.ToString());

        new ClubCityLevelSeedingStrategy().Seed(g);

        // Expected order (strongest → weakest): МСМК, I, III, I юн, III юн
        g.Wrestlers[0].LastName.Should().Be("MSMK");
        g.Wrestlers[1].LastName.Should().Be("AdultI");
        g.Wrestlers[2].LastName.Should().Be("AdultIII");
        g.Wrestlers[3].LastName.Should().Be("JuniorI");
        g.Wrestlers[4].LastName.Should().Be("JuniorIII");
    }

    [Fact]
    public void Seed_SingleWrestler_IsIdempotent()
    {
        var g = MakeGroupWithWrestlers(new[] { Make("Only") });

        new ClubCityLevelSeedingStrategy().Seed(g);

        g.Wrestlers.Should().ContainSingle().Which.SeedNumber.Should().Be(1);
        // Seed leaves IsSeedFixed untouched (see notes on ClubCityLevelSeedingStrategy).
        g.Wrestlers[0].IsSeedFixed.Should().BeFalse();
    }

    [Fact]
    public void DepthOfEncounter_Symmetric_And_MatchesKnownPairs_ForN8()
    {
        // N=8, pairs: (1,2)(3,4) | (5,6)(7,8). Upper half vs lower half = final.
        BracketStructure.DepthOfEncounter(1, 2, 8).Should().Be(1);
        BracketStructure.DepthOfEncounter(3, 4, 8).Should().Be(1);
        BracketStructure.DepthOfEncounter(1, 3, 8).Should().Be(2);
        BracketStructure.DepthOfEncounter(1, 5, 8).Should().Be(3);
        BracketStructure.DepthOfEncounter(4, 8, 8).Should().Be(3);
        // Symmetry
        BracketStructure.DepthOfEncounter(2, 1, 8).Should().Be(1);
    }

    [Fact]
    public void DepthOfEncounter_WithFreeWins_N10_FreeWinnersMeetInRound2()
    {
        // N=10: 6 free wins + 2 pairs (7-8, 9-10). Free-winner slot 1 and 2
        // meet in round 2.
        BracketStructure.DepthOfEncounter(1, 2, 10).Should().Be(2);
        // Slots 7 and 8 are in the same round-1 pair.
        BracketStructure.DepthOfEncounter(7, 8, 10).Should().Be(1);
        // Free-winner 1 meets pair-winner of (7-8) only in round 4 (final).
        BracketStructure.DepthOfEncounter(1, 7, 10).Should().Be(4);
    }

    [Fact]
    public void ShuffleStrategy_AlsoProducesContiguousSeeds()
    {
        var g = MakeGroupWithWrestlers(Enumerable.Range(1, 5).Select(i => Make($"W{i}")));
        new ShuffleSeedingStrategy().Seed(g);
        g.Wrestlers.Select(w => w.SeedNumber).Should().BeEquivalentTo(Enumerable.Range(1, 5).Select(i => (int?)i));
    }
}
