using FluentAssertions;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Tests.Fakes;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public class HomeViewModelTests
{
    [Fact]
    public void PageTitle_contains_Russian_tournament_administrator_label()
    {
        var di = TestContainerBuilder.MakeDefault();
        var vm = new HomeViewModel(di);

        vm.PageTitle.Should().Contain("Вольная борьба");
        vm.PageTitle.Should().Contain("Администратор");
    }

    [Fact]
    public void DrawerItems_include_settings_and_exit_buttons()
    {
        var di = TestContainerBuilder.MakeDefault();
        var vm = new HomeViewModel(di);

        vm.DrawerItems.Should().HaveCount(2);
        vm.DrawerItems[0].Label.Should().Be("Настройки");
        vm.DrawerItems[1].Label.Should().Be("Выйти");
    }

    [Fact]
    public void InitData_resolves_services_from_container_without_throwing()
    {
        var di = TestContainerBuilder.MakeDefault();
        var vm = new HomeViewModel(di);

        var act = () => vm.InitData();
        act.Should().NotThrow();
    }

    [Fact]
    public void WindowTitle_falls_back_to_default_label_when_no_tournament_loaded()
    {
        var di = TestContainerBuilder.MakeDefault();
        var vm = new HomeViewModel(di);
        vm.InitData();

        vm.WindowTitle.Should().Be("Вольная борьба - Администратор турниров");
    }
}
