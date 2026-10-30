using System;
using System.Collections.ObjectModel;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.UI.Material.Tournament.Standing.Mats;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public sealed class MatsPanelStateTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Removing_group_preserves_each_mat_panel_state_and_updates_groups(bool expanded)
    {
        var part = new TournamentPart { ID = Guid.NewGuid() };
        var firstMat = new Mat { ID = Guid.NewGuid() };
        var secondMat = new Mat { ID = Guid.NewGuid() };
        var removed = new AgeWeightGroup { PartID = part.ID };
        var remaining = new AgeWeightGroup { PartID = part.ID };
        firstMat.Groups.Add(removed);
        firstMat.Groups.Add(remaining);
        var vm = new MatsViewModel(null)
        {
            Items = new ObservableCollection<Mat> { firstMat, secondMat }, SelectedPart = part
        };
        var panels = vm.CurrentPartMatPanels;
        panels[0].IsExpanded = expanded;
        panels[1].IsExpanded = !expanded;
        var notifications = new System.Collections.Generic.List<string>();
        panels[0].PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        firstMat.Groups.Remove(removed);
        var refreshed = vm.CurrentPartMatPanels;

        refreshed[0].Should().BeSameAs(panels[0]);
        refreshed[1].Should().BeSameAs(panels[1]);
        refreshed[0].IsExpanded.Should().Be(expanded);
        refreshed[1].IsExpanded.Should().Be(!expanded);
        refreshed[0].Groups.Should().ContainSingle().Which.Should().BeSameAs(remaining);
        notifications.Should().Contain(new[] { "Groups", "WrestlersCount", "MatchesCount", "ExpectedDurationLabel" });
    }

    [Fact]
    public void Parts_keep_independent_expansion_states_for_the_same_mat()
    {
        var firstPart = new TournamentPart { ID = Guid.NewGuid() };
        var secondPart = new TournamentPart { ID = Guid.NewGuid() };
        var vm = new MatsViewModel(null)
        {
            Items = new ObservableCollection<Mat> { new Mat { ID = Guid.NewGuid() } }, SelectedPart = firstPart
        };
        vm.CurrentPartMatPanels[0].IsExpanded = false;

        vm.SelectedPart = secondPart;
        vm.CurrentPartMatPanels[0].IsExpanded.Should().BeTrue();
        vm.SelectedPart = firstPart;
        vm.CurrentPartMatPanels[0].IsExpanded.Should().BeFalse();
    }
}
