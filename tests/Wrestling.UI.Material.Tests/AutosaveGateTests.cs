using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Utils;
using Xunit;

namespace Wrestling.UI.Material.Tests;

// Covers the event-driven autosave hook. Behavior contract after the
// IsAutosaveEnabled flag was removed (autosave is unconditional now):
//   - SaveIfAutosaveEnabledAsync persists when a tournament is loaded AND a
//     FileName is already set.
//   - When FileName is empty the hook is a silent no-op — it must NOT pop a
//     SaveAs dialog from a background sync tick or post-match handler. The
//     operator picks a path via the dashboard prompt instead.
public sealed class AutosaveGateTests
{
    private sealed class TestableVm : TournamentViewModelBase
    {
        public TestableVm(IDiContainer container) : base(container) { }
        public override IList<CommandButtonItem> QuickButtons => new List<CommandButtonItem>();
        public override string PageTitle => "test";
    }

    private static (TestableVm vm, FakeTournamentsManager mgr, Entities.Tournament tournament) BuildVm(string fileName)
    {
        var di = TestContainerBuilder.MakeDefault();
        var tournament = new Entities.Tournament(new GlobalSettings()) { FileName = fileName, Name = "T" };
        di.Resolve<IDataContext>().Tournament = tournament;

        var vm = new TestableVm(di);
        vm.InitData();
        var mgr = (FakeTournamentsManager)di.Resolve<ITournamentsManager>();
        return (vm, mgr, tournament);
    }

    [Fact]
    public async Task Saves_once_when_FileName_set()
    {
        var (vm, mgr, _) = BuildVm(fileName: "tournament.wrt");

        await vm.SaveIfAutosaveEnabledAsync();

        mgr.SaveAsyncCount.Should().Be(1);
    }

    [Fact]
    public async Task Does_not_save_when_FileName_empty()
    {
        var (vm, mgr, _) = BuildVm(fileName: string.Empty);

        await vm.SaveIfAutosaveEnabledAsync();

        mgr.SaveAsyncCount.Should().Be(0);
    }

    [Fact]
    public async Task Does_not_save_when_no_tournament_loaded()
    {
        var di = TestContainerBuilder.MakeDefault();
        var vm = new TestableVm(di);
        vm.InitData();

        await vm.SaveIfAutosaveEnabledAsync();

        ((FakeTournamentsManager)di.Resolve<ITournamentsManager>()).SaveAsyncCount.Should().Be(0);
    }
}
