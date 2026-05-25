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

public class MatRedistributionServicePartsTests
{
    private static List<IGroupBracketProcessor> BuiltInProcessors() => new()
    {
        new OlympicWithConsolationFromFinalistsGroupBracketProcessor(),
        new OlympicGroupBracketProcessor(),
        new RoundRobinGroupBracketProcessor(),
        new SubGroupsToOlympicBracketProcessor()
    };

    private static (WTournament t, AgeWeightGroup group, TournamentPart partA, TournamentPart partB) BuildTwoPart()
    {
        var t = new WTournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };
        var partA = new TournamentPart { ID = Guid.NewGuid(), Name = "Часть 1" };
        var partB = new TournamentPart { ID = Guid.NewGuid(), Name = "Часть 2" };
        t.Parts.Add(partA);
        t.Parts.Add(partB);

        var mat = new Mat { ID = Guid.NewGuid(), Name = "M1", ActivePartID = partA.ID };
        t.Mats.Add(mat);

        var group = new AgeWeightGroup
        {
            ID = Guid.NewGuid(),
            BirthYearMin = 2005, BirthYearMax = 2006, WeightMax = 60,
            MaxRoundSecond = 180, MaxTimeoutSecond = 30, MaxActionSecond = 30,
            MatID = mat.ID,
            PartID = partA.ID
        };
        var ws = Enumerable.Range(0, 4).Select(i => new Wrestler
        {
            ID = Guid.NewGuid(), FirstName = $"W{i}", LastName = $"Ф{i}",
            BirthDate = new DateTime(2005, 1, 1),
            Weight = 60, IsEntryFeePaid = true, IsWeightApproved = true,
            GroupID = group.ID, SeedNumber = i + 1
        }).ToList();
        group.Wrestlers = ws;
        t.Groups.Add(group);
        mat.Groups.Add(group);
        foreach (var w in ws) t.Wrestlers.Add(w);

        new OlympicGroupBracketProcessor().Generate(t, group);
        group.Bracket.BracketTypeCode = BracketTypeEnum.Olympic.ToString();

        new MatMatchNumbersGenerator().Generate(t, BuiltInProcessors());

        return (t, group, partA, partB);
    }

    private static MatRedistributionService NewService() =>
        new MatRedistributionService(new MatMatchNumbersGenerator(), BuiltInProcessors());

    [Fact]
    public void MoveGroupToPart_with_pending_matches_succeeds_and_bumps_FieldsVersion()
    {
        var (t, group, _, partB) = BuildTwoPart();
        var versionBefore = group.FieldsVersion;

        var result = NewService().MoveGroupToPart(t, group, partB.ID);

        result.Outcome.Should().Be(MoveOutcome.Moved);
        group.PartID.Should().Be(partB.ID);
        group.FieldsVersion.Should().Be(versionBefore + 1);
        group.MatID.Should().NotBeNull("MatID is preserved across part moves");
    }

    [Fact]
    public void MoveGroupToPart_same_part_is_NoChange()
    {
        var (t, group, partA, _) = BuildTwoPart();
        var versionBefore = group.FieldsVersion;

        var result = NewService().MoveGroupToPart(t, group, partA.ID);

        result.Outcome.Should().Be(MoveOutcome.NoChange);
        group.FieldsVersion.Should().Be(versionBefore);
    }

    [Fact]
    public void MoveGroupToPart_blocked_by_live_match()
    {
        var (t, group, _, partB) = BuildTwoPart();
        var firstPending = group.Bracket.Rounds
            .SelectMany(r => r.RoundMatches)
            .First(m => m.Status == MatchStatusEnum.Pending);
        firstPending.StartDateTime = DateTime.Now;

        var result = NewService().MoveGroupToPart(t, group, partB.ID);

        result.Outcome.Should().Be(MoveOutcome.BlockedByLiveMatch);
        result.LiveMatch.Should().BeSameAs(firstPending);
        group.PartID.Should().NotBe(partB.ID);
    }

    [Fact]
    public void MoveGroupToPart_blocked_by_completed_matches()
    {
        var (t, group, _, partB) = BuildTwoPart();

        // Approve the first qualification match so the group has historical
        // completions in part A. Moving to part B would silently rewrite
        // part A's PersonalResults — block.
        var processor = new OlympicGroupBracketProcessor();
        processor.Load(t, group);
        var firstPending = group.Bracket.Rounds
            .SelectMany(r => r.RoundMatches)
            .First(m => m.Status == MatchStatusEnum.Pending && m.WrestlerInRed != null && m.WrestlerInBlue != null);
        firstPending.WinType = MatchWinTypeEnum.PointsWin;
        processor.CompleteMatch(firstPending, isRedWon: true, winType: MatchWinTypeEnum.PointsWin);

        var result = NewService().MoveGroupToPart(t, group, partB.ID);

        result.Outcome.Should().Be(MoveOutcome.BlockedByCompletedMatches);
        result.CompletedMatchesCount.Should().BeGreaterThan(0);
        group.PartID.Should().NotBe(partB.ID);
    }

    [Fact]
    public void MoveGroupToPart_renumbers_so_target_part_starts_at_1()
    {
        var (t, group, _, partB) = BuildTwoPart();

        NewService().MoveGroupToPart(t, group, partB.ID);

        // The group's matches now live in partB on M1. Per-(Part, Mat)
        // numbering means M1 × partB has its own counter starting at 1.
        var numbers = group.Bracket.Rounds.SelectMany(r => r.RoundMatches).Select(m => m.MatchNumber).ToList();
        numbers.Should().Contain(1);
    }
}
