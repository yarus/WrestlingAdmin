using System.Collections.Generic;
using System.Linq;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Results.Achievements;
using Wrestling.UI.Material.Tournament.Results.PersonalResults;
using Wrestling.UI.Material.Tournament.Results.TeamResults;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Phase6
{
    // Phase 6 «Результаты» wrapper. Hosts three results sub-tabs:
    // Личные / Командные / Достижения.
    public class Phase6ViewModel : TournamentViewModelBase
    {
        private IStandingPageViewModel _currentPage;
        private List<IStandingPageViewModel> _pageViewModels;

        // One-shot deep-link target — set by callers (e.g. Phase5AdminViewModel)
        // before NavigateToView<Phase6ViewModel>(). Consumed and cleared in
        // InitData so the next plain navigation falls back to the first page.
        private IStandingPageViewModel _pendingInitialPage;

        public Phase6ViewModel(IDiContainer container) : base(container)
        {
        }

        public override bool IsBackButtonAvailable => false;

        public override string PageTitle => CurrentPage?.PageTitle ?? "Результаты";

        public override IList<CommandButtonItem> QuickButtons => CurrentPage?.QuickButtons;

        public IStandingPageViewModel CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage == value) return;
                _currentPage = value;
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(QuickButtons));
                _currentPage?.InitData();
            }
        }

        public List<IStandingPageViewModel> PageViewModels
        {
            get => _pageViewModels;
            set
            {
                _pageViewModels = value;
                OnPropertyChanged(nameof(PageViewModels));
            }
        }

        public override void InitData()
        {
            base.InitData();

            InitPages();
            SetupCurrentPage();
        }

        public void SetInitialPage<TPage>() where TPage : class, IStandingPageViewModel
        {
            if (_pageViewModels == null) InitPages();
            _pendingInitialPage = _pageViewModels.OfType<TPage>().FirstOrDefault();
        }

        private void InitPages()
        {
            if (PageViewModels != null) return;

            var nav = Resolve<INavigationService>();

            PageViewModels = new List<IStandingPageViewModel>
            {
                nav?.GetViewModel<PersonalResultsViewModel>(),
                nav?.GetViewModel<TeamResultsViewModel>(),
                nav?.GetViewModel<AchievementsViewModel>()
            }.Where(vm => vm != null).ToList();
        }

        private void SetupCurrentPage()
        {
            // Preserve CurrentPage across re-entries — when returning from a
            // full-screen MatchResults overlay we want to land on the same
            // sub-tab they left from rather than snap back to Личные.
            var target = _pendingInitialPage ?? CurrentPage ?? PageViewModels.FirstOrDefault();
            _pendingInitialPage = null;
            CurrentPage = target;
        }
    }
}
