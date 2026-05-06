using System.Collections.Generic;
using System.Linq;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Match;
using Wrestling.UI.Material.Settings;
using Wrestling.UI.Material.Slider;
using Wrestling.UI.Material.Tournament.Data;
using Wrestling.UI.Material.Tournament.Phase5;
using Wrestling.UI.Material.Tournament.Phase6;
using Wrestling.UI.Material.Tournament.Progress.Brackets;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Wrestling.UI.Material.Tournament.Results;
using Wrestling.UI.Material.Tournament.Results.Achievements;
using Wrestling.UI.Material.Tournament.Results.PersonalResults;
using Wrestling.UI.Material.Tournament.Results.TeamResults;
using Wrestling.UI.Material.Tournament.Standing.Applications;
using Wrestling.UI.Material.Tournament.Standing.Carpets;
using Wrestling.UI.Material.Tournament.Standing.Details;
using Wrestling.UI.Material.Tournament.Standing.Draw;
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
                new SettingsViewModel(_container),
                new MatchControlViewModel(_container),
                new MatchResultsViewModel(_container),
                // Phase 1-4 sub-pages are now first-class rail destinations.
                new DetailsViewModel(_container),
                new ApplicationsViewModel(_container),
                new DrawViewModel(_container),
                new CarpetsViewModel(_container),
                // Phase 5 hosts these as inner content via CarpetSubViewModel.
                new BracketsViewModel(_container),
                new ScheduleViewModel(_container),
                new SliderControlViewModel(_container),
                // Phase 6 hosts these as next/prev sub-pages.
                new PersonalResultsViewModel(_container),
                new TeamResultsViewModel(_container),
                new AchievementsViewModel(_container),
                new CompletedMatchesViewModel(_container),
                // Phase / utility wrappers.
                new Phase5ViewModel(_container),
                new Phase6ViewModel(_container),
                new DataViewModel(_container)
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

        public void NavigateToView(System.Type viewModelType)
        {
            if (viewModelType == null) return;
            var vm = GetViewModel(viewModelType);
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

        public T GetViewModel<T>() where T : ViewModelBase
        {
            return _viewModels.FirstOrDefault(p => p is T) as T;
        }

        public ViewModelBase GetViewModel(System.Type viewModelType)
        {
            return _viewModels.FirstOrDefault(p => viewModelType.IsInstanceOfType(p));
        }
    }
}