using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.UI.Material.Match;
using Wrestling.UI.Material.Tests.Fakes;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public class SetWinTypeViewModelTests
{
    [Fact]
    public void Constructor_filters_available_win_types()
    {
        var di = TestContainerBuilder.MakeDefault();
        var available = new List<MatchWinTypeEnum> { MatchWinTypeEnum.Tushe, MatchWinTypeEnum.PointsWin };

        var vm = new SetWinTypeViewModel(di, value: null, availableWinTypes: available);

        vm.WinTypes.Should().BeEquivalentTo(available);
        vm.SelectedItem.Should().BeNull();
    }

    [Fact]
    public void SelectedItem_setter_raises_property_changed()
    {
        var di = TestContainerBuilder.MakeDefault();
        var vm = new SetWinTypeViewModel(di, value: MatchWinTypeEnum.Tushe,
            availableWinTypes: new List<MatchWinTypeEnum> { MatchWinTypeEnum.Tushe, MatchWinTypeEnum.PointsWin });

        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SelectedItem = MatchWinTypeEnum.PointsWin;

        vm.SelectedItem.Should().Be(MatchWinTypeEnum.PointsWin);
        raised.Should().Contain("SelectedItem");
    }

    [Fact]
    public void Unknown_win_types_are_not_exposed()
    {
        var di = TestContainerBuilder.MakeDefault();
        var vm = new SetWinTypeViewModel(di, value: null,
            availableWinTypes: new List<MatchWinTypeEnum> { MatchWinTypeEnum.Tushe });

        vm.WinTypes.Should().ContainSingle().Which.Should().Be(MatchWinTypeEnum.Tushe);
        vm.WinTypes.Should().NotContain(MatchWinTypeEnum.FreeWin);
    }
}
