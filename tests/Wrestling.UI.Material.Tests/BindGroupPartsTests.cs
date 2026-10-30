using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Tournament.Standing.Mats;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public sealed class BindGroupPartsTests
{
    private static (Wrestling.Entities.Tournament Tournament, AgeWeightGroup Group, Mat Mat,
        TournamentPart TargetPart, TestDiContainer Di) Create()
    {
        var tournament = HomeViewModel.CreateNewTournament(new GlobalSettings());
        var target = new TournamentPart { ID = Guid.NewGuid(), Name = "Часть 2" };
        tournament.Parts.Add(target);
        var mat = new Mat { ID = Guid.NewGuid(), Name = "Ковёр 1", ActivePartID = target.ID };
        tournament.Mats.Add(mat);
        var group = new AgeWeightGroup
        {
            ID = Guid.NewGuid(), PartID = tournament.Parts[0].ID,
            Wrestlers = new List<Wrestler> { new Wrestler { ID = Guid.NewGuid(), SeedNumber = 1 },
                new Wrestler { ID = Guid.NewGuid(), SeedNumber = 2 } }
        };
        tournament.Groups.Add(group);
        foreach (var wrestler in group.Wrestlers) tournament.Wrestlers.Add(wrestler);
        var processor = new RoundRobinGroupBracketProcessor();
        processor.Generate(tournament, group);
        var di = new TestDiContainer();
        di.Add<IDataContext>(new DataContext { Tournament = tournament });
        di.Add<IMatRedistributionService>(new MatRedistributionService(new MatMatchNumbersGenerator(),
            new List<IGroupBracketProcessor> { processor }));
        return (tournament, group, mat, target, di);
    }

    [Fact]
    public void Second_part_picker_offers_unassigned_group_from_first_part()
    {
        var (_, group, _, target, di) = Create();
        var picker = new BindGroupViewModel(di) { PartFilter = target.ID };
        picker.InitData();
        picker.Groups.Should().ContainSingle().Which.Should().BeSameAs(group);
    }

    [Fact]
    public void Binding_to_second_part_moves_part_and_mat_and_numbers_match()
    {
        var (tournament, group, mat, target, di) = Create();
        var vm = new MatsViewModel(di);
        vm.InitData();
        vm.SelectedPart = target;
        int version = group.FieldsVersion;

        var result = vm.BindSelectedGroup(mat, group, target.ID);

        result.Outcome.Should().Be(MoveOutcome.Moved);
        group.PartID.Should().Be(target.ID);
        group.MatID.Should().Be(mat.ID);
        group.FieldsVersion.Should().BeGreaterThan(version);
        mat.Groups.Should().ContainSingle().Which.Should().BeSameAs(group);
        vm.CurrentPartMatPanels[0].Groups.Should().ContainSingle().Which.Should().BeSameAs(group);
        group.Bracket.Rounds.SelectMany(r => r.RoundMatches).Should().ContainSingle().Which.MatchNumber.Should().Be(1);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    public void Completed_results_stay_in_their_part_but_free_byes_can_move(bool samePart, bool freeWin, bool offered)
    {
        var (_, group, _, target, di) = Create();
        var match = group.Bracket.Rounds.SelectMany(r => r.RoundMatches).Single();
        match.Status = MatchStatusEnum.Completed;
        match.WinType = freeWin ? MatchWinTypeEnum.FreeWin : MatchWinTypeEnum.Tushe;
        var picker = new BindGroupViewModel(di) { PartFilter = samePart ? group.PartID : target.ID };
        picker.InitData();
        picker.Groups.Contains(group).Should().Be(offered);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Already_assigned_or_live_groups_are_not_offered(bool assigned)
    {
        var (_, group, mat, target, di) = Create();
        if (assigned) group.MatID = mat.ID;
        else group.Bracket.Rounds.SelectMany(r => r.RoundMatches).Single().StartDateTime = DateTime.Now;
        var picker = new BindGroupViewModel(di) { PartFilter = target.ID };
        picker.InitData();
        picker.Groups.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Stale_selection_rechecks_match_guards_before_changing_part_or_mat(bool live)
    {
        var (_, group, mat, target, di) = Create();
        var vm = new MatsViewModel(di);
        vm.InitData();
        var originalPart = group.PartID;
        int originalVersion = group.FieldsVersion;
        var match = group.Bracket.Rounds.SelectMany(r => r.RoundMatches).Single();
        if (live) match.StartDateTime = DateTime.Now;
        else { match.Status = MatchStatusEnum.Completed; match.WinType = MatchWinTypeEnum.Tushe; }

        var result = vm.BindSelectedGroup(mat, group, target.ID);

        result.Outcome.Should().Be(live ? MoveOutcome.BlockedByLiveMatch : MoveOutcome.BlockedByCompletedMatches);
        group.PartID.Should().Be(originalPart);
        group.MatID.Should().BeNull();
        group.FieldsVersion.Should().Be(originalVersion);
        mat.Groups.Should().BeEmpty();
    }
}
