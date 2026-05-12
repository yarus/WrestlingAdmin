using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Wrestling.Entities;
using Wrestling.Entities.Results.Achievements;
using Wrestling.Providers;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Tournament.Results.Achievements
{
    public class AchievementsViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        // T inherited from TournamentViewModelBase.
        public string PageName => T("Achievements_PageName", "Достижения");


        private IResultsService _resultsService;
        private bool _resultsSubscribed;
        private bool _localizationSubscribed;

        private IList<AchievementCategoryViewModel> _items;

        public AchievementsViewModel(IDiContainer container) : base(container)
        {
        }

        public override bool IsBackButtonAvailable => true;

        public override string PageTitle => T("Achievements_PageTitle", "Достижения спортсменов");

        public IList<AchievementCategoryViewModel> Items
        {
            get => _items;
            private set
            {
                _items = value;
                OnPropertyChanged(nameof(Items));
            }
        }

        public override void InitData()
        {
            base.InitData();

            if (_resultsService == null)
            {
                _resultsService = Resolve<IResultsService>();
            }

            if (_resultsService != null && !_resultsSubscribed)
            {
                _resultsService.ResultsChanged += OnResultsChanged;
                _resultsSubscribed = true;
            }

            // Subscribe to language changes so achievement titles/definitions
            // re-render without re-opening the tournament. The Items list is
            // rebuilt with fresh AchievementCategoryViewModel instances whose
            // Title/Definition pull through AchievementLabels at access time.
            if (!_localizationSubscribed)
            {
                LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
                _localizationSubscribed = true;
            }

            Refresh();
        }

        protected override void OnBackCommand()
        {
            // Phase 6 wrapper handles navigation; sub-page back is no-op.
        }

        private void OnResultsChanged()
        {
            Refresh();
        }

        private void OnLocalizationChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "Item[]" && e.PropertyName != nameof(LocalizationService.CurrentLanguage)) return;
            Refresh();
        }

        private void Refresh()
        {
            if (_resultsService == null)
            {
                Items = new List<AchievementCategoryViewModel>();
                return;
            }

            // Group by AchievementType so each nomination shows up once with
            // all its winners (ties produce >1 wrestler per category). Title
            // and Definition are computed from AchievementType via
            // AchievementLabels so language switches reflect immediately on
            // the next Refresh().
            Items = _resultsService.Achievements
                .GroupBy(a => a.AchievementType)
                .Select(g => new AchievementCategoryViewModel(
                    achievementType: g.Key,
                    winners: g.ToList()))
                .ToList();
        }
    }

    public class AchievementCategoryViewModel
    {
        public AchievementCategoryViewModel(string achievementType, IList<WrestlerAchievement> winners)
        {
            AchievementType = achievementType;
            Winners = winners ?? new List<WrestlerAchievement>();
        }

        // Computed via AchievementLabels each access so a language switch
        // followed by Items refresh produces a fresh display value.
        public string Title => AchievementLabels.GetTitle(AchievementType);
        public string Definition => AchievementLabels.GetDefinition(AchievementType);
        public string AchievementType { get; }
        public IList<WrestlerAchievement> Winners { get; }

        public int WinnersCount => Winners.Count;
    }
}
