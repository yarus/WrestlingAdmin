using System.Windows.Input;
using FluentAssertions;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tests.Fakes;
using Wrestling.UI.Utils;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public sealed class NavigationKeyboardCleanupTests
{
    private sealed class KeyboardPage : ViewModelBase
    {
        private readonly IKeyHandler _keys;
        public int ExitCount { get; private set; }
        public int ReceivedKeys { get; private set; }

        public KeyboardPage(IDiContainer di, IKeyHandler keys) : base(di) => _keys = keys;

        public override void InitData()
        {
            base.InitData();
            _keys.KeyPressed += OnKey;
        }

        private void OnKey(object sender, KeyEventArgs e) => ReceivedKeys++;

        protected override void OnNavigatingOut()
        {
            ExitCount++;
            _keys.KeyPressed -= OnKey;
        }

        public void GoHome() => NavigateToView<HomeViewModel>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Direct_navigation_unsubscribes_the_outgoing_pages_keyboard_handler(bool byType)
    {
        var di = new TestDiContainer();
        var keys = new KeyHandler();
        var page = new KeyboardPage(di, keys);
        var shell = new FakeShellViewModel { CurrentViewModel = page };
        var navigation = new NavigationService(di) { ShellVm = shell };
        navigation.LoadNavigation();
        page.InitData();
        keys.RiseKeyDown(null);
        page.ReceivedKeys.Should().Be(1);

        if (byType) navigation.NavigateToView(typeof(HomeViewModel));
        else navigation.NavigateToView<HomeViewModel>();
        keys.RiseKeyDown(null);

        shell.CurrentViewModel.Should().BeOfType<HomeViewModel>();
        page.ExitCount.Should().Be(1);
        page.ReceivedKeys.Should().Be(1);
    }

    [Fact]
    public void Navigation_through_a_page_cleans_up_exactly_once()
    {
        var di = new TestDiContainer();
        var page = new KeyboardPage(di, new KeyHandler());
        var navigation = new NavigationService(di)
        {
            ShellVm = new FakeShellViewModel { CurrentViewModel = page }
        };
        di.Add<INavigationService>(navigation);
        navigation.LoadNavigation();
        page.InitData();

        page.GoHome();

        page.ExitCount.Should().Be(1);
    }
}
