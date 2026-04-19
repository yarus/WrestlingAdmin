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

// Covers the event-driven autosave gate introduced to replace the
// DispatcherTimer-based autosave. Behavior contract:
//   - SaveIfAutosaveEnabledAsync persists only when IsAutosaveEnabled is true
//     AND a tournament is loaded AND a FileName is already set.
//   - No timer is involved: saves happen only after explicit calls from event
//     handlers (match completion, successful import).
public sealed class AutosaveGateTests
{
    private sealed class TestableVm : TournamentViewModelBase
    {
        public TestableVm(IDiContainer container) : base(container) { }
        public override IList<CommandButtonItem> QuickButtons => new List<CommandButtonItem>();
        public override string PageTitle => "test";
    }

    private static (TestableVm vm, FakeTournamentsManager mgr, Entities.Tournament tournament) BuildVm(bool autosave, string fileName)
    {
        var di = TestContainerBuilder.MakeDefault();
        var settings = new GlobalSettings { IsAutosaveEnabled = autosave };
        var tournament = new Entities.Tournament(settings) { FileName = fileName, Name = "T" };
        di.Resolve<IDataContext>().Tournament = tournament;

        var vm = new TestableVm(di);
        vm.InitData();
        var mgr = (FakeTournamentsManager)di.Resolve<ITournamentsManager>();
        return (vm, mgr, tournament);
    }

    [Fact]
    public async Task Saves_once_when_autosave_enabled_and_FileName_set()
    {
        var (vm, mgr, _) = BuildVm(autosave: true, fileName: "tournament.wrt");

        await vm.SaveIfAutosaveEnabledAsync();

        mgr.SaveAsyncCount.Should().Be(1);
    }

    [Fact]
    public async Task Does_not_save_when_autosave_disabled()
    {
        var (vm, mgr, _) = BuildVm(autosave: false, fileName: "tournament.wrt");

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
