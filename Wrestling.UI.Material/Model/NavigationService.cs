using System.Collections.Generic;
using System.Linq;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Match;
using Wrestling.UI.Material.Settings;
using Wrestling.UI.Material.Slider;
using Wrestling.UI.Material.Tournament.Dashboard;
using Wrestling.UI.Material.Tournament.Progress.Brackets;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Wrestling.UI.Material.Tournament.Results;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Model
{
    public class NavigationService : INavigationService
    {
        private readonly IDiContainer _container;
        private List<ViewModelBase> _viewModels;

        public NavigationService(IDiContainer container)
        {
            _container = container;
        }

        public void LoadNavigation()
        {
            _viewModels = new List<ViewModelBase>
            {
                new HomeViewModel(_container),
                new StandingViewModel(_container),
                new SettingsViewModel(_container),
                new MatchControlViewModel(_container),
                new MatchResultsViewModel(_container),
                new BracketsViewModel(_container),
                new ResultsViewModel(_container),
                new DashboardViewModel(_container),
                new ScheduleViewModel(_container),
                new CompletedMatchesViewModel(_container),
                new SliderControlViewModel(_container)
            };
        }

        public void ShowPrintPreview(ViewModelBase vm)
        {
            var printHost = _container.Resolve("PrintHost") as IPanelView;
            if (printHost != null && vm != null)
            {
                vm.InitData();
                printHost.ShowScreen(vm);
            }
        }

        public void NavigateToView<T>() where T : ViewModelBase
        {
            var vm = GetViewModel<T>();
            if (vm != null)
            {
                vm.InitData();
                ShellVm.CurrentViewModel = vm;
                vm.OnNavigationCompleted();
            }
        }
        
        public void CloseApp()
        {
            ShellVm.RequestClose();
        }

        public IShellViewModel ShellVm { get; set; }
        
        private T GetViewModel<T>() where T : ViewModelBase
        {
            return _viewModels.FirstOrDefault(p => p is T) as T;
        }
    }
}