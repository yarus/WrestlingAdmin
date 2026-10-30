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

public class MatRedistributionServiceTests
{
    private static List<IGroupBracketProcessor> BuiltInProcessors() => new()
    {
        new OlympicWithConsolationFromFinalistsGroupBracketProcessor(),
        new OlympicGroupBracketProcessor(),
        new RoundRobinGroupBracketProcessor(),
        new SubGroupsToOlympicBracketProcessor()
    };

    // Builds a tournament with two mats and one already-bound group on Mat A.
    // Optional second group can be added unbound for the "move into empty mat"
    // scenarios. Returned tuple gives the caller everything it needs to assert
    // post-move state.
    private static (WTournament tournament, AgeWeightGroup group, Mat matA, Mat matB) BuildTwoMatsOneGroup(int wrestlers = 4)
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

        var matA = new Mat { ID = Guid.NewGuid(), Name = "Mat A" };
        var matB = new Mat { ID = Guid.NewGuid(), Name = "Mat B" };

        var t = new WTournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };
        t.Groups.Add(group);
        foreach (var w in ws) t.Wrestlers.Add(w);
        t.Mats.Add(matA);
        t.Mats.Add(matB);

        new OlympicGroupBracketProcessor().Generate(t, group);
        group.Bracket.BracketTypeCode = BracketTypeEnum.Olympic.ToString();

        // Bind to Mat A so the "move" cases have a starting state.
        group.MatID = matA.ID;
        group.MatLabel = matA.Name;
        matA.Groups.Add(group);

        // Initial numbering pass so we have a baseline to compare against.
        new MatMatchNumbersGenerator().Generate(t, BuiltInProcessors());

        return (t, group, matA, matB);
    }

    private static MatRedistributionService NewService() =>
        new MatRedistributionService(new MatMatchNumbersGenerator(), BuiltInProcessors());

    [Fact]
    public void Move_to_another_mat_swaps_group_membership_and_bumps_version()
    {
        var (t, group, matA, matB) = BuildTwoMatsOneGroup();
        var versionBefore = group.FieldsVersion;

        var result = NewService().MoveGroupToMat(t, group, matB.ID);

        result.Outcome.Should().Be(MoveOutcome.Moved);
        group.MatID.Should().Be(matB.ID);
        group.MatLabel.Should().Be(matB.Name);
        group.FieldsVersion.Should().Be(versionBefore + 1);
        matA.Groups.Should().NotContain(group);
        matB.Groups.Should().Contain(group);
    }

    [Fact]
    public void Unbind_clears_mat_assignment_and_drops_from_donor()
    {
        var (t, group, matA, _) = BuildTwoMatsOneGroup();
        var versionBefore = group.FieldsVersion;

        var result = NewService().MoveGroupToMat(t, group, null);

        result.Outcome.Should().Be(MoveOutcome.Moved);
        group.MatID.Should().BeNull();
        group.MatLabel.Should().BeEmpty();
        group.FieldsVersion.Should().Be(versionBefore + 1);
        matA.Groups.Should().NotContain(group);
    }

    [Fact]
    public void Bind_from_unbound_places_group_on_target_mat()
    {
        var (t, group, _, matB) = BuildTwoMatsOneGroup();
        // First unbind to set up the "unbound → bound" path.
        NewService().MoveGroupToMat(t, group, null);
        var versionAfterUnbind = group.FieldsVersion;

        var result = NewService().MoveGroupToMat(t, group, matB.ID);

        result.Outcome.Should().Be(MoveOutcome.Moved);
        group.MatID.Should().Be(matB.ID);
        group.FieldsVersion.Should().Be(versionAfterUnbind + 1);
        matB.Groups.Should().Contain(group);
    }

    [Fact]
    public void Move_to_same_mat_is_NoChange_and_does_not_bump_version()
    {
        var (t, group, matA, _) = BuildTwoMatsOneGroup();
        var versionBefore = group.FieldsVersion;

        var result = NewService().MoveGroupToMat(t, group, matA.ID);

        result.Outcome.Should().Be(MoveOutcome.NoChange);
        group.FieldsVersion.Should().Be(versionBefore);
        matA.Groups.Should().Contain(group);
    }

    [Fact]
    public void Live_match_blocks_move_and_returns_offending_match()
    {
        var (t, group, matA, matB) = BuildTwoMatsOneGroup();
        var firstPending = group.Bracket.Rounds
            .SelectMany(r => r.RoundMatches)
            .First(m => m.Status == MatchStatusEnum.Pending);
        firstPending.StartDateTime = DateTime.Now; // simulate timer started

        var versionBefore = group.FieldsVersion;
        var result = NewService().MoveGroupToMat(t, group, matB.ID);

        result.Outcome.Should().Be(MoveOutcome.BlockedByLiveMatch);
        result.LiveMatch.Should().BeSameAs(firstPending);
        group.MatID.Should().Be(matA.ID);
        group.FieldsVersion.Should().Be(versionBefore);
        matA.Groups.Should().Contain(group);
        matB.Groups.Should().NotContain(group);
    }

    [Fact]
    public void Move_renumbers_matches_on_both_mats()
    {
        var (t, group, matA, matB) = BuildTwoMatsOneGroup();

        // Add a second group on Mat B so it had its own numbering before the move.
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
        t.Groups.Add(group2);
        foreach (var w in ws2) t.Wrestlers.Add(w);
        new OlympicGroupBracketProcessor().Generate(t, group2);
        group2.Bracket.BracketTypeCode = BracketTypeEnum.Olympic.ToString();
        group2.MatID = matB.ID;
        group2.MatLabel = matB.Name;
        matB.Groups.Add(group2);
        new MatMatchNumbersGenerator().Generate(t, BuiltInProcessors());

        var matBMaxBefore = group2.Bracket.Rounds.SelectMany(r => r.RoundMatches).Max(m => m.MatchNumber);

        // Move the original group from A to B → numbers on B must grow, on A must drop.
        NewService().MoveGroupToMat(t, group, matB.ID);

        var matBMaxAfter = matB.Groups
            .SelectMany(g => g.Bracket.Rounds.SelectMany(r => r.RoundMatches))
            .Max(m => m.MatchNumber);
        matBMaxAfter.Should().BeGreaterThan(matBMaxBefore,
            "Mat B picked up the moved group's matches in addition to its own");

        // Mat A is now empty — no group should have a non-zero MatchNumber under it.
        matA.Groups.Should().BeEmpty();
    }

    [Fact]
    public void HasLiveMatch_detects_pending_with_StartDateTime()
    {
        var (_, group, _, _) = BuildTwoMatsOneGroup();
        var svc = NewService();

        svc.HasLiveMatch(group).Should().BeFalse("no match has been started yet");

        var firstPending = group.Bracket.Rounds
            .SelectMany(r => r.RoundMatches)
            .First(m => m.Status == MatchStatusEnum.Pending);
        firstPending.StartDateTime = DateTime.Now;

        svc.HasLiveMatch(group).Should().BeTrue();

        firstPending.StartDateTime = null;
        svc.HasLiveMatch(group).Should().BeFalse("Revert cleared StartDateTime");
    }
}
