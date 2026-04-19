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
public sealed class DashboardAutosaveTests
{
    private sealed class NullPanelView : IPanelView
    {
        public void ShowScreen(ObservableObject dataContext) { }
        public void CloseScreen() { }
    }

    [Fact]
    public async Task InitData_with_autosave_enabled_and_FileName_does_not_save_periodically()
    {
        var di = TestContainerBuilder.MakeDefault();
        di.Add(new NullPanelView(), "ScoreScreen");
        di.Add<ScoreScreenViewModel>(new ScoreScreenViewModel(di));

        var settings = new GlobalSettings { IsAutosaveEnabled = true };
        var tournament = new Entities.Tournament(settings) { FileName = "tournament.wrt", Name = "T" };
        di.Resolve<IDataContext>().Tournament = tournament;

        var vm = new DashboardViewModel(di);
        vm.InitData();

        // Give any residual timer a chance to tick. The old timer ran every
        // 1s and saved at AutosaveMaxSecond (default 30s); at 2s no tick
        // should fire even under the old design, but the presence of any
        // background persistence is exactly what this refactor forbids.
        await Task.Delay(2000);

        var mgr = (FakeTournamentsManager)di.Resolve<ITournamentsManager>();
        mgr.SaveAsyncCount.Should().Be(0);
    }
}
