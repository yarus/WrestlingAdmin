using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.UI.Material.Match;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Utils.Converters;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public class WarningHistoryTests
{
    private static MatchControlViewModel Build() => new(new TestDiContainer())
    {
        ScoreScreenVm = new ScoreScreenViewModel(new TestDiContainer())
    };

    private static List<MatchAction> History(MatchControlViewModel vm) =>
        (List<MatchAction>)typeof(MatchControlViewModel).GetField("_matchActions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vm)!;

    [Theory]
    [InlineData("Red")]
    [InlineData("Blue")]
    public void Removing_warning_then_undo_restores_counter_and_records_signed_changes(string side)
    {
        var vm = Build();
        vm.ChangeWarningsCommand.Execute("+1," + side);
        vm.ChangeWarningsCommand.Execute("-1," + side);
        vm.AdjustPointsCommand.Execute("0," + side);

        var count = side == "Red" ? vm.ScoreScreenVm.Wrestler1WarningsNumber : vm.ScoreScreenVm.Wrestler2WarningsNumber;
        count.Should().Be(1);
        History(vm).Select(a => a.WarningDelta).Should().Equal(1, -1, 1);
        History(vm).Should().OnlyContain(a => a.Points == 0 && a.IsForRed == (side == "Red"));
        vm.AdjustPointsCommand.Execute("0," + side);
        count = side == "Red" ? vm.ScoreScreenVm.Wrestler1WarningsNumber : vm.ScoreScreenVm.Wrestler2WarningsNumber;
        count.Should().Be(0);
        History(vm).Sum(a => a.WarningDelta).Should().Be(0);
        vm.AdjustPointsCommand.Execute("0," + side);
        History(vm).Should().HaveCount(4, "already cancelled actions must not be undone again");
    }

    [Fact]
    public void Removing_at_zero_does_not_add_a_fictitious_action()
    {
        var vm = Build();
        vm.ChangeWarningsCommand.Execute("-1,Red");
        vm.ScoreScreenVm.Wrestler1WarningsNumber.Should().Be(0);
        History(vm).Should().BeEmpty();
    }

    [Fact]
    public void Undo_is_scoped_to_side_and_still_accepts_legacy_warning_entries()
    {
        var vm = Build();
        History(vm).Add(new MatchAction { Type = MatchActionType.SetWarning, IsForRed = true });
        vm.ScoreScreenVm.Wrestler1WarningsNumber = 1;
        vm.ChangeWarningsCommand.Execute("+1,Blue");
        vm.AdjustPointsCommand.Execute("0,Red");

        vm.ScoreScreenVm.Wrestler1WarningsNumber.Should().Be(0);
        vm.ScoreScreenVm.Wrestler2WarningsNumber.Should().Be(1);
        History(vm)[0].WarningDelta.Should().BeNull("old ambiguous records must not be rewritten");
        History(vm).Last().WarningDelta.Should().Be(-1);
    }

    [Fact]
    public void Removal_description_is_distinct_in_display_converter()
    {
        var converter = new MatchActionDescriptionConverter();
        converter.Convert(new object[] { MatchActionType.SetWarning, true, 0, -1 }, typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be("Снято предупреждение красному");
        converter.Convert(new object[] { MatchActionType.RevertWarning, false, 0, 1 }, typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be("Отмена снятия предупреждения синему");
    }
}
