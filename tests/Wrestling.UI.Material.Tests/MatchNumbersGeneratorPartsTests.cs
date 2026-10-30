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

public class MatchNumbersGeneratorPartsTests
{
    private static List<IGroupBracketProcessor> BuiltInProcessors() => new()
    {
        new OlympicWithConsolationFromFinalistsGroupBracketProcessor(),
        new OlympicGroupBracketProcessor(),
        new RoundRobinGroupBracketProcessor(),
        new SubGroupsToOlympicBracketProcessor()
    };

    private static AgeWeightGroup BuildGroup(WTournament t, Mat mat, Guid? partId, int weightKg, int wrestlers)
    {
        var group = new AgeWeightGroup
        {
            ID = Guid.NewGuid(),
            BirthYearMin = 2005, BirthYearMax = 2006, WeightMax = weightKg,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30,
            MatID = mat.ID,
            PartID = partId
        };
        var ws = Enumerable.Range(0, wrestlers).Select(i => new Wrestler
        {
            ID = Guid.NewGuid(),
            FirstName = $"W{i}", LastName = $"Ф{i}",
            BirthDate = new DateTime(2005, 1, 1),
            Weight = weightKg, IsEntryFeePaid = true, IsWeightApproved = true,
            GroupID = group.ID,
            SeedNumber = i + 1
        }).ToList();
        group.Wrestlers = ws;

        t.Groups.Add(group);
        mat.Groups.Add(group);
        foreach (var w in ws) t.Wrestlers.Add(w);

        new OlympicGroupBracketProcessor().Generate(t, group);
        group.Bracket.BracketTypeCode = BracketTypeEnum.Olympic.ToString();
        return group;
    }

    [Fact]
    public void Numbering_restarts_from_1_for_each_part_on_same_mat()
    {
        var t = new WTournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };
        var partA = new TournamentPart { ID = Guid.NewGuid(), Name = "Часть 1" };
        var partB = new TournamentPart { ID = Guid.NewGuid(), Name = "Часть 2" };
        t.Parts.Add(partA);
        t.Parts.Add(partB);

        var mat = new Mat { ID = Guid.NewGuid(), Name = "Mat A", ActivePartID = partA.ID };
        t.Mats.Add(mat);

        var gA = BuildGroup(t, mat, partA.ID, 55, 4);
        var gB = BuildGroup(t, mat, partB.ID, 60, 4);

        new MatMatchNumbersGenerator().Generate(t, BuiltInProcessors());

        var aNumbers = gA.Bracket.Rounds.SelectMany(r => r.RoundMatches).Select(m => m.MatchNumber).ToList();
        var bNumbers = gB.Bracket.Rounds.SelectMany(r => r.RoundMatches).Select(m => m.MatchNumber).ToList();

        aNumbers.Should().Contain(1, "part 1 numbering starts at 1");
        bNumbers.Should().Contain(1, "part 2 numbering also restarts at 1 on the same mat");
    }

    [Fact]
    public void Numbering_independent_between_mats_within_same_part()
    {
        var t = new WTournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };
        var part = new TournamentPart { ID = Guid.NewGuid(), Name = "Часть 1" };
        t.Parts.Add(part);

        var matA = new Mat { ID = Guid.NewGuid(), Name = "Mat A", ActivePartID = part.ID };
        var matB = new Mat { ID = Guid.NewGuid(), Name = "Mat B", ActivePartID = part.ID };
        t.Mats.Add(matA);
        t.Mats.Add(matB);

        var gA = BuildGroup(t, matA, part.ID, 55, 4);
        var gB = BuildGroup(t, matB, part.ID, 60, 4);

        new MatMatchNumbersGenerator().Generate(t, BuiltInProcessors());

        gA.Bracket.Rounds.SelectMany(r => r.RoundMatches).Select(m => m.MatchNumber).Should().Contain(1);
        gB.Bracket.Rounds.SelectMany(r => r.RoundMatches).Select(m => m.MatchNumber).Should().Contain(1);
    }

    [Fact]
    public void Legacy_tournament_without_Parts_falls_back_to_single_slice_per_mat()
    {
        // Build a tournament without any Parts (mimics legacy test fixtures).
        // Generator must still produce non-zero MatchNumbers because the
        // defensive fallback treats the entire mat as one slice.
        var t = new WTournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };
        var mat = new Mat { ID = Guid.NewGuid(), Name = "Mat A" };
        t.Mats.Add(mat);
        var g = BuildGroup(t, mat, partId: null, 55, 4);

        new MatMatchNumbersGenerator().Generate(t, BuiltInProcessors());

        g.Bracket.Rounds.SelectMany(r => r.RoundMatches).Select(m => m.MatchNumber).Should().Contain(1);
    }
}
