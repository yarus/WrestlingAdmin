using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Tournament.Dashboard;
using Wrestling.UI.Utils;
using Xunit;

namespace Wrestling.UI.Material.Tests;

// The old DispatcherTimer-based autosave was replaced with an event-driven
// model. InitData on the dashboard must no longer spin up a background timer
// that periodically saves — saves only happen after explicit events.
//
// The manual "Сохранить турнир" quick button must stay visible regardless of
// the autosave flag: event-driven autosave only covers match/import, so
// mutations like registering teams/wrestlers or generating brackets still
// need an on-demand save action.
public sealed class DashboardAutosaveTests
{
    // The save button is constructed with the (tooltip, icon, command) overload,
    // so the Russian text lives on TooltipText; Label is empty.
    private const string SaveButtonTooltip = "Сохранить турнир";

    private sealed class NullPanelView : IPanelView
    {
        public bool WasShown => false;
        public void ShowScreen(ObservableObject dataContext) { }
        public void CloseScreen() { }
    }

    private static DashboardViewModel BuildDashboard(bool autosaveEnabled, out FakeTournamentsManager mgr)
    {
        var di = TestContainerBuilder.MakeDefault();
        di.Add(new NullPanelView(), "ScoreScreen");
        di.Add<ScoreScreenViewModel>(new ScoreScreenViewModel(di));

        var settings = new GlobalSettings { IsAutosaveEnabled = autosaveEnabled };
        var tournament = new Entities.Tournament(settings) { FileName = "tournament.wrt", Name = "T" };
        di.Resolve<IDataContext>().Tournament = tournament;

        mgr = (FakeTournamentsManager)di.Resolve<ITournamentsManager>();

        var vm = new DashboardViewModel(di);
        vm.InitData();
        return vm;
    }

    [Fact]
    public async Task InitData_with_autosave_enabled_and_FileName_does_not_save_periodically()
    {
        var vm = BuildDashboard(autosaveEnabled: true, out var mgr);

        // Give any residual timer a chance to tick. The presence of any
        // background persistence is exactly what this refactor forbids —
        // saves must happen only after explicit match/import events.
        await Task.Delay(2000);

        mgr.SaveAsyncCount.Should().Be(0);
    }

    [Fact]
    public void QuickButtons_include_save_command_when_autosave_enabled()
    {
        var vm = BuildDashboard(autosaveEnabled: true, out _);

        vm.QuickButtons.Select(b => b.TooltipText).Should().Contain(SaveButtonTooltip,
            "manual save must stay reachable — event-driven autosave only covers match/import, " +
            "so team/wrestler/bracket/schedule edits have no other escape hatch");
    }

    [Fact]
    public void QuickButtons_include_save_command_when_autosave_disabled()
    {
        var vm = BuildDashboard(autosaveEnabled: false, out _);

        vm.QuickButtons.Select(b => b.TooltipText).Should().Contain(SaveButtonTooltip);
    }
}
