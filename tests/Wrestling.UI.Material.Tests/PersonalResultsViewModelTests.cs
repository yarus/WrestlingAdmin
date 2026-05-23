using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MvvmDialogs;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Entities.Results;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Tournament.Results.PersonalResults;
using Xunit;

namespace Wrestling.UI.Material.Tests;

// Behavior contract for the per-group accordion on the "Личные итоги" screen:
//   - Groups in Items are sorted ASC by BirthYearMin → BirthYearMax → WeightMax,
//     mirroring how a paper protocol is laid out (older categories first, then
//     light → heavy weights inside each age band).
//   - WrestlersCount / MedalsCount in each group's header reflect the FULL
//     roster, not the post-filter slice — the filter must not change cardinality
//     reported in the header (a real bug we shipped 2026-05-03).
//   - When a filter is active, groups whose roster has no matches are HIDDEN
//     entirely; otherwise every category shows up even if some have zero results.
//   - FilterString re-applies the filter only on ≥3 character input, mirroring
//     ApplicationsView UX so a single key-press doesn't thrash the list.
public sealed class PersonalResultsViewModelTests
{
    [Fact]
    public void InitData_sorts_groups_by_birthYear_then_weight()
    {
        // Mixed input order, identifiable by (minYear, weightMax) tuple.
        var spec = new[]
        {
            (minYear: 2005, maxYear: 2008, weight: 60.0),  // молодёжь
            (minYear: 2014, maxYear: 2015, weight: 35.0),  // дети 35
            (minYear: 2014, maxYear: 2015, weight: 50.0),  // дети 50
            (minYear: 2010, maxYear: 2012, weight: 50.0),  // юноши 50
            (minYear: 2010, maxYear: 2012, weight: 65.0),  // юноши 65
        };
        var (di, _) = BuildContainer(BuildResults(spec));

        var vm = new PersonalResultsViewModel(di);
        vm.InitData();

        vm.Items.Select(i => (i.Category.BirthYearMin, i.Category.WeightMax))
            .Should().Equal(
                ((int?)2005, (double?)60.0),
                ((int?)2010, (double?)50.0),
                ((int?)2010, (double?)65.0),
                ((int?)2014, (double?)35.0),
                ((int?)2014, (double?)50.0));
    }

    [Fact]
    public void Header_counters_track_full_roster_not_filtered_view()
    {
        // 4-wrestler group: 3 medalists (places 1, 2, 3) + 1 unplaced.
        var (di, _) = BuildContainer(MakeResults(2010, 2010, 60, places: new int?[] { 1, 2, 3, null }));

        var vm = new PersonalResultsViewModel(di);
        vm.InitData();

        var header = vm.Items.Single();
        header.WrestlersCount.Should().Be(4);
        header.MedalsCount.Should().Be(3);

        // Toggling "Только призёры" must not change the header counters.
        vm.IsOnlyMedalsVisible = true;

        vm.Items.Single().WrestlersCount.Should().Be(4);
        vm.Items.Single().MedalsCount.Should().Be(3);
        vm.Items.Single().Wrestlers.Should().HaveCount(3); // visible list shrinks
    }

    [Fact]
    public void OnlyMedalsFilter_hides_groups_with_no_medalists_when_filter_is_active()
    {
        var resultsA = MakeResults(2010, 2010, 50, places: new int?[] { 1, 2, 3 });
        var resultsB = MakeResults(2010, 2010, 60, places: new int?[] { null, null });
        var (di, _) = BuildContainer(resultsA.Concat(resultsB).ToList());

        var vm = new PersonalResultsViewModel(di);
        vm.InitData();

        vm.Items.Should().HaveCount(2, "no filter → both categories visible");

        vm.IsOnlyMedalsVisible = true;

        vm.Items.Should().HaveCount(1, "category B has no medalists, hide it");
        vm.Items.Single().Category.WeightMax.Should().Be(50);
    }

    [Fact]
    public void Bracketless_groups_in_tournament_dont_appear_in_results()
    {
        // A category that exists in Tournament.Groups but produced no entries
        // in the cache (no bracket / no completed matches) is silently skipped.
        var withResults = MakeResults(2010, 2010, 60, places: new int?[] { 1, 2 });
        var (di, t) = BuildContainer(withResults);
        t.Groups.Add(MakeGroup(2010, 2010, 80));

        var vm = new PersonalResultsViewModel(di);
        vm.InitData();

        vm.Items.Should().HaveCount(1);
        vm.Items.Single().Category.WeightMax.Should().Be(60);
    }

    [Fact]
    public void FilterString_with_fewer_than_3_chars_does_not_re_filter()
    {
        var (di, _) = BuildContainer(MakeResults(2010, 2010, 60,
            lastNames: new[] { "Иванов", "Петров", "Сидоров" }));

        var vm = new PersonalResultsViewModel(di);
        vm.InitData();
        var initialCount = vm.Items.Single().Wrestlers.Count;

        vm.FilterString = "Ив"; // 2 chars — below threshold

        vm.Items.Single().Wrestlers.Should().HaveCount(initialCount,
            "filter string under 3 chars must not narrow the list");
    }

    [Fact]
    public void FilterString_with_3plus_chars_filters_by_lastname_prefix()
    {
        var (di, _) = BuildContainer(MakeResults(2010, 2010, 60,
            lastNames: new[] { "Иванов", "Иваненко", "Петров", "Сидоров" }));

        var vm = new PersonalResultsViewModel(di);
        vm.InitData();

        vm.FilterString = "Ива";

        vm.Items.Single().Wrestlers.Select(w => w.Wrestler.LastName)
            .Should().BeEquivalentTo(new[] { "Иванов", "Иваненко" });
    }

    [Fact]
    public void Clearing_filter_after_3plus_chars_restores_full_list()
    {
        var (di, _) = BuildContainer(MakeResults(2010, 2010, 60,
            lastNames: new[] { "Иванов", "Петров", "Сидоров" }));

        var vm = new PersonalResultsViewModel(di);
        vm.InitData();

        vm.FilterString = "Ива";
        vm.Items.Single().Wrestlers.Should().HaveCount(1);

        vm.FilterString = string.Empty;
        vm.Items.Single().Wrestlers.Should().HaveCount(3);
    }

    [Fact]
    public void ResultsChanged_event_rebuilds_items_with_fresh_cache()
    {
        // After the first build a match completes → service raises
        // ResultsChanged → VM rebuilds without any user interaction.
        var initial = MakeResults(2010, 2010, 60, places: new int?[] { 1, 2 });
        var di = new TestDiContainer();
        SeedDefaults(di);
        var tournament = BuildTournamentFromResults(initial);
        di.Resolve<IDataContext>().Tournament = tournament;
        var rs = new ConfigurableResultsService(initial);
        di.Add<IResultsService>(rs);

        var vm = new PersonalResultsViewModel(di);
        vm.InitData();
        vm.Items.Single().Wrestlers.Should().HaveCount(2);

        // Simulate match approve → cache now has 4 entries on the SAME group
        // (re-use the existing group instance so reference equality holds).
        var existingGroup = tournament.Groups.Single();
        var bigger = MakeResultsForGroup(existingGroup, places: new int?[] { 1, 2, 3, 4 });
        rs.Replace(bigger);

        vm.Items.Single().Wrestlers.Should().HaveCount(4);
    }

    // --- Stub & helpers ----------------------------------------------------

    private sealed class ConfigurableResultsService : IResultsService
    {
        public IReadOnlyList<TournamentResult> AllResults { get; private set; }
        public IReadOnlyList<TournamentTeamResult> TeamResults { get; private set; } = new List<TournamentTeamResult>();
        public IReadOnlyList<WrestlerAchievement> Achievements { get; private set; } = new List<WrestlerAchievement>();
        public event Action ResultsChanged;

        public ConfigurableResultsService(IList<TournamentResult> initial)
        {
            AllResults = (initial ?? new List<TournamentResult>()).ToList();
        }

        public void Recalculate(Entities.Tournament tournament)
        {
            // Not driven from these tests — Replace() is the explicit hook.
        }

        public IReadOnlyList<TournamentTeamResult> GetOrderedTeamResults(ITeamResultsOrderer orderer) => TeamResults;

        public void Replace(IList<TournamentResult> next)
        {
            AllResults = next.ToList();
            ResultsChanged?.Invoke();
        }
    }

    private static (TestDiContainer di, Entities.Tournament t) BuildContainer(IList<TournamentResult> results)
    {
        var di = new TestDiContainer();
        SeedDefaults(di);
        var tournament = BuildTournamentFromResults(results);
        di.Resolve<IDataContext>().Tournament = tournament;
        di.Add<IResultsService>(new ConfigurableResultsService(results));
        return (di, tournament);
    }

    private static void SeedDefaults(TestDiContainer di)
    {
        var shell = new FakeShellViewModel();
        var nav = new FakeNavigationService { ShellVm = shell };
        di.Add<IDataContext>(new DataContext());
        di.Add<IDialogService>(new FakeDialogService());
        di.Add<INavigationService>(nav);
        di.Add<GlobalSettings>(new GlobalSettings());
        di.Add<ITournamentsManager>(new FakeTournamentsManager());
        di.Add<ICacheManager>(new FakeCacheManager());
    }

    private static Entities.Tournament BuildTournamentFromResults(IList<TournamentResult> results)
    {
        var t = new Entities.Tournament(new GlobalSettings()) { ID = Guid.NewGuid(), Name = "T" };
        foreach (var g in results.Select(r => r.Group).Distinct())
        {
            t.Groups.Add(g);
        }
        return t;
    }

    private static IList<TournamentResult> BuildResults(
        IEnumerable<(int minYear, int maxYear, double weight)> spec)
    {
        var all = new List<TournamentResult>();
        foreach (var (minYear, maxYear, weight) in spec)
        {
            all.AddRange(MakeResults(minYear, maxYear, weight, places: new int?[] { 1 }));
        }
        return all;
    }

    private static IList<TournamentResult> MakeResults(
        int? minYear,
        int? maxYear,
        double? weightMax,
        int?[] places = null,
        string[] lastNames = null)
    {
        var n = places?.Length ?? lastNames?.Length ?? 0;
        if (places == null) places = Enumerable.Range(1, n).Cast<int?>().ToArray();
        if (lastNames == null) lastNames = Enumerable.Range(1, n).Select(i => $"W{i:D2}").ToArray();

        var group = MakeGroup(minYear, maxYear, weightMax);
        return MakeResultsForGroup(group, places, lastNames);
    }

    private static IList<TournamentResult> MakeResultsForGroup(
        AgeWeightGroup group,
        int?[] places,
        string[] lastNames = null)
    {
        if (lastNames == null) lastNames = Enumerable.Range(1, places.Length).Select(i => $"W{i:D2}").ToArray();

        var results = new List<TournamentResult>();
        for (int i = 0; i < places.Length; i++)
        {
            var w = MakeWrestler(lastNames[i], placement: places[i]);
            w.GroupID = group.ID;
            group.Wrestlers.Add(w);
            results.Add(new TournamentResult(group, w));
        }
        return results;
    }

    private static AgeWeightGroup MakeGroup(int? minYear, int? maxYear, double? weightMax)
    {
        return new AgeWeightGroup
        {
            ID = Guid.NewGuid(),
            BirthYearMin = minYear,
            BirthYearMax = maxYear,
            WeightMax = weightMax,
            MaxRoundSecond = 180,
            MaxTimeoutSecond = 30,
            MaxActionSecond = 30,
            Wrestlers = new List<Wrestler>(),
            Bracket = new GroupBracket { BracketTypeLabel = "Olympic", BracketTypeCode = "Olympic" }
        };
    }

    private static Wrestler MakeWrestler(string last, int? placement = null)
    {
        return new Wrestler
        {
            ID = Guid.NewGuid(),
            LastName = last,
            FirstName = "X",
            BirthDate = new DateTime(2010, 1, 1),
            Weight = 60,
            FinalPlace = placement,
            IsEntryFeePaid = true,
            IsWeightApproved = true
        };
    }
}
