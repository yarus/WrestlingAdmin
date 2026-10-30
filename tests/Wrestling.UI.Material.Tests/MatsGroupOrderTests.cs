using System;
using System.Collections.ObjectModel;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.UI.Material.Tournament.Standing.Mats;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public sealed class MatsGroupOrderTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Arrows_swap_adjacent_visible_groups_and_refresh_expanded_panel(bool moveUp)
    {
        var part = new TournamentPart { ID = Guid.NewGuid() };
        var mat = new Mat { ID = Guid.NewGuid() };
        var first = new AgeWeightGroup { ID = Guid.NewGuid(), MatID = mat.ID, PartID = part.ID };
        var hidden = new AgeWeightGroup { ID = Guid.NewGuid(), MatID = mat.ID, PartID = Guid.NewGuid() };
        var last = new AgeWeightGroup { ID = Guid.NewGuid(), MatID = mat.ID, PartID = part.ID };
        mat.Groups = new ObservableCollection<AgeWeightGroup> { first, hidden, last };
        var vm = new MatsViewModel(null) { Items = new ObservableCollection<Mat> { mat }, SelectedPart = part };
        var panel = vm.CurrentPartMatPanels[0];
        panel.IsExpanded = true;
        int refreshes = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.CurrentPartMatPanels))
            {
                refreshes++;
                vm.CurrentPartMatPanels[0].Should().BeSameAs(panel);
            }
        };

        (moveUp ? vm.UpGroupCommand : vm.DownGroupCommand).Execute(moveUp ? last : first);

        mat.Groups.Should().Equal(last, hidden, first);
        panel.Groups.Should().Equal(last, first);
        panel.IsExpanded.Should().BeTrue();
        refreshes.Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Arrows_at_visible_part_boundaries_leave_all_groups_in_place(bool moveUp)
    {
        var part = new TournamentPart { ID = Guid.NewGuid() };
        var mat = new Mat { ID = Guid.NewGuid() };
        var hiddenBefore = new AgeWeightGroup { ID = Guid.NewGuid(), MatID = mat.ID, PartID = Guid.NewGuid() };
        var onlyVisible = new AgeWeightGroup { ID = Guid.NewGuid(), MatID = mat.ID, PartID = part.ID };
        var hiddenAfter = new AgeWeightGroup { ID = Guid.NewGuid(), MatID = mat.ID, PartID = hiddenBefore.PartID };
        mat.Groups = new ObservableCollection<AgeWeightGroup> { hiddenBefore, onlyVisible, hiddenAfter };
        var vm = new MatsViewModel(null) { Items = new ObservableCollection<Mat> { mat }, SelectedPart = part };
        var original = mat.Groups.ToArray();

        (moveUp ? vm.UpGroupCommand : vm.DownGroupCommand).Execute(onlyVisible);

        mat.Groups.Should().Equal(original);
    }
}
