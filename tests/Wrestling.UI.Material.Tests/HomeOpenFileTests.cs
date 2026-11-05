using System;
using System.IO;
using FluentAssertions;
using Wrestling.DataAccess;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Material.Tournament.Conducting;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public sealed class HomeOpenFileTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Opening_a_file_with_spaces_and_cyrillic_routes_to_its_phase_and_keeps_an_absolute_save_path(bool relative)
    {
        var directory = Path.Combine(Path.GetTempPath(), "wrestling-startup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "Тестовый турнир.wrt");
            var manager = new TournamentsManager(new TournamentDataAccess(new JsonStorageDataAccess()), new EntityToInfoAdapter());
            var tournament = new Wrestling.Entities.Tournament(new GlobalSettings()) { ID = Guid.NewGuid() };
            tournament.Mats.Add(new Mat { ID = Guid.NewGuid(), Name = "A" });
            manager.SaveToFile(tournament, path).Should().BeTrue();
            var di = TestContainerBuilder.MakeDefault();
            di.Add<ITournamentsManager>(manager);
            di.Add<IResultsService>(new ResultsService(null, null, null));
            var vm = new HomeViewModel(di);
            vm.InitData();

            vm.OpenFromFile(relative ? Path.GetRelativePath(Environment.CurrentDirectory, path) : path).Should().BeTrue();

            var opened = di.Resolve<IDataContext>().Tournament;
            opened.ID.Should().Be(tournament.ID);
            opened.FileName.Should().Be(path);
            ((FakeNavigationService)di.Resolve<INavigationService>()).NavigatedTo.Should().ContainSingle()
                .Which.Should().Be(typeof(ConductingViewModel));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("missing-startup-tournament.wrt")]
    [InlineData("\0invalid.wrt")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unreadable_argument_reports_a_load_error_and_keeps_the_home_page(string path)
    {
        var di = TestContainerBuilder.MakeDefault();
        di.Add<ITournamentsManager>(new TournamentsManager(new TournamentDataAccess(new JsonStorageDataAccess()), new EntityToInfoAdapter()));
        var vm = new HomeViewModel(di);
        vm.InitData();

        vm.OpenFromFile(path).Should().BeFalse();

        di.Resolve<IDataContext>().Tournament.Should().BeNull();
        var navigation = (FakeNavigationService)di.Resolve<INavigationService>();
        navigation.NavigatedTo.Should().BeEmpty();
        ((FakeShellViewModel)navigation.ShellVm).Snackbar.Should().ContainSingle();
    }
}
