using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Dashboard;
using Wrestling.UI.Material.Tournament.Standing.Applications;
using Wrestling.UI.Material.Tournament.Standing.Carpets;
using Wrestling.UI.Material.Tournament.Standing.Details;
using Wrestling.UI.Material.Tournament.Standing.Draw;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing
{
    public class StandingViewModel : TournamentViewModelBase
    {
        #region Fields
        
        private IStandingPageViewModel _currentPage;

        private List<IStandingPageViewModel> _pageViewModels;
       
        private ICommand _changePageCommand;
        private ICommand _prevPageCommand;
        private ICommand _nextPageCommand;

        #endregion

        public StandingViewModel(IDiContainer container) : base(container)
        {
        }

        public override bool IsBackButtonAvailable => true;

        public override void InitData()
        {
            base.InitData();

            InitPages();

            SetupCurrentPage();
        }

        public override string PageTitle
        {
            get
            {
                if (CurrentPage != null)
                {
                    return CurrentPage.PageTitle;
                }

                return "Турнир";
            }
        }

        public override IList<CommandButtonItem> QuickButtons => CurrentPage?.QuickButtons;

        public bool IsNotLastPage => _pageViewModels.IndexOf(CurrentPage) < _pageViewModels.Count - 1;
        public bool IsNotFirstPage => _pageViewModels.IndexOf(CurrentPage) > 0;

        #region Properties / Commands

        public IStandingPageViewModel CurrentPage
        {
            get { return _currentPage; }
            set
            {
                _currentPage = value;

                OnPropertyChanged("CurrentPage");
                OnPropertyChanged("PageTitle");
                OnPropertyChanged("IsNotFirstPage");
                OnPropertyChanged("IsNotLastPage");
                OnPropertyChanged("QuickButtons");
            }
        }

        public List<IStandingPageViewModel> PageViewModels
        {
            get { return _pageViewModels; }
            set
            {
                _pageViewModels = value;

                OnPropertyChanged("PageViewModels");
            }
        }
        
        public ICommand ChangePageCommand
        {
            get
            {
                if (_changePageCommand == null)
                {
                    _changePageCommand = new RelayCommand(
                        p => ChangeViewModel((IStandingPageViewModel)p),
                        p => p is IStandingPageViewModel);
                }

                return _changePageCommand;
            }
        }

        public ICommand NextPageCommand
        {
            get
            {
                if (_nextPageCommand == null)
                {
                    _nextPageCommand = new RelayCommand(
                        p => NextViewModel(),
                        p => true);
                }

                return _nextPageCommand;
            }
        }

        public ICommand PrevPageCommand
        {
            get
            {
                if (_prevPageCommand == null)
                {
                    _prevPageCommand = new RelayCommand(
                        p => PreviousViewModel(),
                        p => true);
                }

                return _prevPageCommand;
            }
        }
        
        private void ChangeViewModel(IStandingPageViewModel viewModel)
        {
            if (!PageViewModels.Contains(viewModel))
                PageViewModels.Add(viewModel);
            CurrentPage = PageViewModels.FirstOrDefault(vm => vm == viewModel);
            CurrentPage?.InitData();
        }

        private void NextViewModel()
        {
            var index = PageViewModels.IndexOf(CurrentPage);
            if (index < PageViewModels.Count - 1)
            {
                var nextVm = PageViewModels[index + 1];
                ChangeViewModel(nextVm);
            }
        }
        
        private void PreviousViewModel()
        {
            var index = PageViewModels.IndexOf(CurrentPage);
            if (index > 0)
            {
                var prevVm = PageViewModels[index - 1];
                ChangeViewModel(prevVm);
            }
        }

        #endregion

        private void InitPages()
        {
            if (PageViewModels != null) return;

            PageViewModels = new List<IStandingPageViewModel>
            {
                new DetailsViewModel(DiContainer),
                new ApplicationsViewModel(DiContainer),
                new DrawViewModel(DiContainer),
                new CarpetsViewModel(DiContainer)
            };
        }

        protected override void OnBackCommand()
        {
            base.OnBackCommand();

            ConfirmDetails();

            NavigateToView<DashboardViewModel>();
        }

        private void ConfirmDetails()
        {
            var carpetedGroups = DataContext.Tournament.Carpets.SelectMany(c => c.Groups);

            var hasNoBracketGroup = DataContext.Tournament.Groups.FirstOrDefault(g => g.Bracket == null) != null;

            var hasNoCarpetGroup = DataContext.Tournament.Groups.FirstOrDefault(g => !carpetedGroups.Contains(g)) != null;

            if (!hasNoBracketGroup && !hasNoCarpetGroup && DataContext.Tournament.Status == TournamentStatus.Pending)
            {
                DataContext.Tournament.Status = TournamentStatus.InProgress;
            }

            NavigateToView<DashboardViewModel>();
        }

        private void SetupCurrentPage()
        {
            CurrentPage = PageViewModels[0];
            CurrentPage.InitData();
        }
    }
}