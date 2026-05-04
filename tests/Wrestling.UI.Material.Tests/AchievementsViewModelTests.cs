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
using Wrestling.UI.Material.Tournament.Results.Achievements;
using Xunit;

namespace Wrestling.UI.Material.Tests;

// Behavior contract for the "Достижения спортсменов" screen:
//   - Items group all WrestlerAchievement entries by AchievementType so each
//     nomination shows up once with all its winners (ties produce >1 winner
//     under the same group).
//   - Each group's Title and Definition come from the first achievement of
//     that type — definition is the user-visible "за что" tooltip in the
//     accordion header.
//   - ResultsChanged event triggers a rebuild without manual refresh.
public sealed class AchievementsViewModelTests
{
    [Fact]
    public void Items_group_by_AchievementType()
    {
        var ach = new[]
        {
            MakeAchievement(type: "FastestWin", title: "Молния", def: "Самая быстрая победа", value: "30 сек"),
            MakeAchievement(type: "FastestWin", title: "Молния", def: "Самая быстрая победа", value: "30 сек"), // tie
            MakeAchievement(type: "MostPoints", title: "Машина", def: "Больше всех баллов", value: "42"),
        };
        var di = BuildContainer(ach);

        var vm = new AchievementsViewModel(di);
        vm.InitData();

        vm.Items.Should().HaveCount(2);
        vm.Items.Single(g => g.AchievementType == "FastestWin").WinnersCount.Should().Be(2);
        vm.Items.Single(g => g.AchievementType == "MostPoints").WinnersCount.Should().Be(1);
    }

    [Fact]
    public void Group_header_carries_title_and_definition_from_first_achievement()
    {
        var ach = new[]
        {
            MakeAchievement(type: "FastestWin", title: "Молния", def: "Самая быстрая победа", value: "30 сек"),
        };
        var di = BuildContainer(ach);

        var vm = new AchievementsViewModel(di);
        vm.InitData();

        var group = vm.Items.Single();
        group.Title.Should().Be("Молния");
        group.Definition.Should().Be("Самая быстрая победа");
    }

    [Fact]
    public void Empty_cache_yields_empty_Items()
    {
        var di = BuildContainer(Array.Empty<WrestlerAchievement>());
        var vm = new AchievementsViewModel(di);
        vm.InitData();

        vm.Items.Should().BeEmpty();
    }

    [Fact]
    public void ResultsChanged_event_rebuilds_Items_with_new_data()
    {
        var initial = new[] { MakeAchievement("FastestWin", "Молния", "def", "30 сек") };
        var di = BuildContainer(initial);

        var vm = new AchievementsViewModel(di);
        vm.InitData();
        vm.Items.Should().HaveCount(1);

        var rs = (ConfigurableResultsService)di.Resolve<IResultsService>();
        rs.Replace(new[]
        {
            MakeAchievement("FastestWin", "Молния", "def", "30 сек"),
            MakeAchievement("MostPoints", "Машина", "def2", "42"),
            MakeAchievement("MostTushe", "Асфальтоукладчик", "def3", "5"),
        });

        vm.Items.Should().HaveCount(3);
    }

    // --- Stub & helpers ----------------------------------------------------

    private sealed class ConfigurableResultsService : IResultsService
    {
        public IReadOnlyList<TournamentResult> AllResults { get; private set; } = new List<TournamentResult>();
        public IReadOnlyList<TournamentTeamResult> TeamResults { get; private set; } = new List<TournamentTeamResult>();
        public IReadOnlyList<WrestlerAchievement> Achievements { get; private set; }
        public event Action ResultsChanged;

        public ConfigurableResultsService(IEnumerable<WrestlerAchievement> achievements)
        {
            Achievements = (achievements ?? Array.Empty<WrestlerAchievement>()).ToList();
        }

        public void Recalculate(Entities.Tournament tournament) { }

        public void Replace(IEnumerable<WrestlerAchievement> next)
        {
            Achievements = next.ToList();
            ResultsChanged?.Invoke();
        }
    }

    private static TestDiContainer BuildContainer(IEnumerable<WrestlerAchievement> achievements)
    {
        var di = new TestDiContainer();
        di.Add<IDataContext>(new DataContext { Tournament = new Entities.Tournament(new GlobalSettings()) { Name = "T" } });
        di.Add<IDialogService>(new FakeDialogService());
        di.Add<INavigationService>(new FakeNavigationService());
        di.Add<GlobalSettings>(new GlobalSettings());
        di.Add<ITournamentsManager>(new FakeTournamentsManager());
        di.Add<ICacheManager>(new FakeCacheManager());
        di.Add<IResultsService>(new ConfigurableResultsService(achievements));
        return di;
    }

    private static WrestlerAchievement MakeAchievement(string type, string title, string def, string value)
    {
        return new WrestlerAchievement
        {
            Title = title,
            AchievementType = type,
            AchievementDefinition = def,
            AchievementValue = value,
            Wrestler = new Wrestler { ID = Guid.NewGuid(), LastName = "Тест", FirstName = "Спортсмен" }
        };
    }
}
