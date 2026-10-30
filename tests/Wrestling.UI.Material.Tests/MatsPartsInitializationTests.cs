using System.Collections.Generic;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Tournament.Standing.Mats;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public sealed class MatsPartsInitializationTests
{
    [Fact]
    public void New_tournament_has_a_stable_default_part_before_save_and_reload()
    {
        var tournament = HomeViewModel.CreateNewTournament(new GlobalSettings());
        var part = tournament.Parts.Should().ContainSingle().Subject;
        part.ID.Should().NotBeEmpty();
        part.Name.Should().NotBeNullOrWhiteSpace();

        var adapter = new EntityToInfoAdapter();
        var reloaded = adapter.GetEntityFromInfo(adapter.GetInfoFromEntity(tournament));

        reloaded.Parts.Should().ContainSingle().Which.ID.Should().Be(part.ID);
    }

    [Fact]
    public void First_schedule_entry_exposes_default_part_and_can_add_second_without_reopening()
    {
        var dc = new DataContext { Tournament = HomeViewModel.CreateNewTournament(new GlobalSettings()) };
        var di = new TestDiContainer();
        di.Add<IDataContext>(dc);
        var vm = new MatsViewModel(di);
        vm.Parts.Should().BeNull("the view may bind before InitData");
        var notifications = new List<string>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        vm.InitData();

        vm.Parts.Should().BeSameAs(dc.Tournament.Parts);
        vm.SelectedPart.Should().BeSameAs(dc.Tournament.Parts[0]);
        notifications.Should().Contain(new[] { "Parts", "HasMultipleParts", "CurrentPartMatPanels" });
        vm.AddPartCommand.CanExecute(null).Should().BeTrue();
        vm.AddPartCommand.Execute(null);
        vm.Parts.Should().HaveCount(2);
        vm.HasMultipleParts.Should().BeTrue();
    }

    [Fact]
    public void Reused_schedule_view_refreshes_parts_when_tournament_is_replaced()
    {
        var dc = new DataContext { Tournament = HomeViewModel.CreateNewTournament(new GlobalSettings()) };
        var di = new TestDiContainer();
        di.Add<IDataContext>(dc);
        var vm = new MatsViewModel(di);
        vm.InitData();
        vm.AddPartCommand.Execute(null);
        vm.HasMultipleParts.Should().BeTrue();
        var nextTournament = HomeViewModel.CreateNewTournament(new GlobalSettings());
        dc.Tournament = nextTournament;
        var notifications = new List<string>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        vm.InitData();

        vm.Parts.Should().BeSameAs(nextTournament.Parts);
        vm.SelectedPart.Should().BeSameAs(nextTournament.Parts[0]);
        vm.HasMultipleParts.Should().BeFalse();
        notifications.Should().Contain(new[] { "Parts", "HasMultipleParts", "CurrentPartMatPanels" });
    }

    [Fact]
    public void Returning_to_same_tournament_refreshes_panels_even_if_selected_part_did_not_change()
    {
        var dc = new DataContext { Tournament = HomeViewModel.CreateNewTournament(new GlobalSettings()) };
        dc.Tournament.Mats.Add(new Mat { ID = System.Guid.NewGuid() });
        var di = new TestDiContainer();
        di.Add<IDataContext>(dc);
        var vm = new MatsViewModel(di);
        vm.InitData();
        var oldPanel = vm.CurrentPartMatPanels[0];
        var notifications = new List<string>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        vm.InitData();

        notifications.Should().Contain("CurrentPartMatPanels");
        vm.CurrentPartMatPanels[0].Should().NotBeSameAs(oldPanel);
    }
}
