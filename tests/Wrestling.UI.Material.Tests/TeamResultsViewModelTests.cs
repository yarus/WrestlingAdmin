using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MvvmDialogs;
using Wrestling.Entities;
using Wrestling.Entities.Results;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Tournament.Results.TeamResults;
using Xunit;

namespace Wrestling.UI.Material.Tests;

// Behavior contract for the "Командные итоги" screen:
//   - Switching SelectedSystem must reorder Items via the orderer keyed by
//     SelectedSystem.OrdererKey + "Orderer" (Olympic/Medals/Points).
//   - Default selection is Olympic on first init.
//   - Items mirror the cached IResultsService.TeamResults (no recompute,
//     orderer-only).
//   - ChangeSystemCommand sets SelectedSystem from a TeamResultsSystemItem
//     parameter (mirrors carpet-picker pattern).
public sealed class TeamResultsViewModelTests
{
    [Fact]
    public void Default_selected_system_is_Olympic()
    {
        var di = BuildContainer(rawTeamResults: SampleTeams());
        var vm = new TeamResultsViewModel(di);

        vm.InitData();

        vm.SelectedSystem.Should().NotBeNull();
        vm.SelectedSystem.OrdererKey.Should().Be("OlympicOrderer");
    }

    [Fact]
    public void Items_use_the_orderer_keyed_by_SelectedSystem()
    {
        // Two distinct orderers reverse one another; we can detect which is
        // active by which order Items comes out in.
        var di = BuildContainer(
            rawTeamResults: SampleTeams(),
            orderers: new (string key, IList<TournamentTeamResult> output)[]
            {
                ("OlympicOrderer", new List<TournamentTeamResult>()), // populated below
                ("MedalsOrderer",  new List<TournamentTeamResult>()),
                ("PointsOrderer",  new List<TournamentTeamResult>()),
            });

        var teams = di.Resolve<IResultsService>().TeamResults.ToList();
        ReplaceOrderer(di, "OlympicOrderer", new[] { teams[0], teams[1], teams[2] });
        ReplaceOrderer(di, "MedalsOrderer",  new[] { teams[2], teams[1], teams[0] });
        ReplaceOrderer(di, "PointsOrderer",  new[] { teams[1], teams[0], teams[2] });

        var vm = new TeamResultsViewModel(di);
        vm.InitData();
        vm.Items.Select(t => t.TeamName).Should().Equal("A", "B", "C");

        vm.SelectedSystem = vm.Systems.Single(s => s.OrdererKey == "MedalsOrderer");
        vm.Items.Select(t => t.TeamName).Should().Equal("C", "B", "A");

        vm.SelectedSystem = vm.Systems.Single(s => s.OrdererKey == "PointsOrderer");
        vm.Items.Select(t => t.TeamName).Should().Equal("B", "A", "C");
    }

    [Fact]
    public void ChangeSystemCommand_sets_SelectedSystem_from_parameter()
    {
        var di = BuildContainer(rawTeamResults: SampleTeams());
        var vm = new TeamResultsViewModel(di);
        vm.InitData();

        var medals = vm.Systems.Single(s => s.OrdererKey == "MedalsOrderer");
        vm.ChangeSystemCommand.Execute(medals);

        vm.SelectedSystem.Should().BeSameAs(medals);
    }

    [Fact]
    public void ChangeSystemCommand_ignores_non_system_parameter()
    {
        // Defensive — a stray binding should not crash the command.
        var di = BuildContainer(rawTeamResults: SampleTeams());
        var vm = new TeamResultsViewModel(di);
        vm.InitData();
        var initial = vm.SelectedSystem;

        Action act = () => vm.ChangeSystemCommand.Execute("not a system item");

        act.Should().NotThrow();
        vm.SelectedSystem.Should().BeSameAs(initial);
    }

    [Fact]
    public void ResultsChanged_event_refreshes_Items_with_new_team_data()
    {
        var di = BuildContainer(rawTeamResults: SampleTeams());
        ReplaceOrderer(di, "OlympicOrderer", di.Resolve<IResultsService>().TeamResults.ToList());

        var vm = new TeamResultsViewModel(di);
        vm.InitData();
        vm.Items.Should().HaveCount(3);

        // Service caches new team data and raises event.
        var rs = (ConfigurableResultsService)di.Resolve<IResultsService>();
        var bigger = MakeTeams("A", "B", "C", "D");
        ReplaceOrderer(di, "OlympicOrderer", bigger);
        rs.ReplaceTeams(bigger.ToList());

        vm.Items.Should().HaveCount(4);
    }

    [Fact]
    public void Empty_team_results_yields_empty_Items()
    {
        var di = BuildContainer(rawTeamResults: new List<TournamentTeamResult>());
        var vm = new TeamResultsViewModel(di);
        vm.InitData();

        vm.Items.Should().BeEmpty();
    }

    // --- Stubs & helpers ---------------------------------------------------

    private sealed class ConfigurableResultsService : IResultsService
    {
        public IReadOnlyList<TournamentResult> AllResults { get; private set; } = new List<TournamentResult>();
        public IReadOnlyList<TournamentTeamResult> TeamResults { get; private set; }
        public IReadOnlyList<WrestlerAchievement> Achievements { get; private set; } = new List<WrestlerAchievement>();
        public event Action ResultsChanged;

        public ConfigurableResultsService(IList<TournamentTeamResult> teams)
        {
            TeamResults = (teams ?? new List<TournamentTeamResult>()).ToList();
        }

        public void Recalculate(Entities.Tournament tournament) { }

        public void ReplaceTeams(IList<TournamentTeamResult> next)
        {
            TeamResults = next.ToList();
            ResultsChanged?.Invoke();
        }
    }

    private sealed class StaticOrderer : ITeamResultsOrderer
    {
        public IList<TournamentTeamResult> Output { get; set; }
        public List<TournamentTeamResult> GetOrderedResults(IEnumerable<TournamentTeamResult> teamResults)
            => Output?.ToList() ?? new List<TournamentTeamResult>();
    }

    private static TestDiContainer BuildContainer(
        IList<TournamentTeamResult> rawTeamResults,
        (string key, IList<TournamentTeamResult> output)[] orderers = null)
    {
        var di = new TestDiContainer();
        di.Add<IDataContext>(new DataContext { Tournament = new Entities.Tournament(new GlobalSettings()) { Name = "T" } });
        di.Add<IDialogService>(new FakeDialogService());
        di.Add<INavigationService>(new FakeNavigationService());
        di.Add<GlobalSettings>(new GlobalSettings());
        di.Add<ITournamentsManager>(new FakeTournamentsManager());
        di.Add<ICacheManager>(new FakeCacheManager());

        di.Add<IResultsService>(new ConfigurableResultsService(rawTeamResults));

        // Default orderers each return the input as-is. Tests override per key
        // via ReplaceOrderer when they need to assert specific ordering.
        foreach (var key in new[] { "OlympicOrderer", "MedalsOrderer", "PointsOrderer" })
        {
            di.Add(new StaticOrderer { Output = rawTeamResults.ToList() }, key);
        }

        if (orderers != null)
        {
            foreach (var (key, output) in orderers)
            {
                di.Add(new StaticOrderer { Output = output ?? rawTeamResults.ToList() }, key);
            }
        }

        return di;
    }

    private static void ReplaceOrderer(TestDiContainer di, string key, IList<TournamentTeamResult> output)
    {
        di.Remove(key);
        di.Add(new StaticOrderer { Output = output.ToList() }, key);
    }

    private static List<TournamentTeamResult> SampleTeams() => MakeTeams("A", "B", "C");

    private static List<TournamentTeamResult> MakeTeams(params string[] names)
    {
        return names.Select(n => new TournamentTeamResult(
            teamID: Guid.NewGuid(),
            teamName: n,
            teamCity: n + "-city",
            wrestlers: new List<TournamentResult>())).ToList();
    }
}
