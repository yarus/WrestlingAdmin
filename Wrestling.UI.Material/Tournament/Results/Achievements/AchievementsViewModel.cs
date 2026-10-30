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
        private TournamentPart _selectedPart;

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
                OnPropertyChanged(nameof(HasAchievements));
            }
        }

        // Drives the empty-state placeholder: false when no nomination has any
        // winner yet (early in the tournament, before any decisive result).
        public bool HasAchievements => _items != null && _items.Count > 0;

        // Mirrors PersonalResultsViewModel: per-part view defaulting to the
        // first non-empty part. Achievements are part-scoped so award
        // ceremonies show only the relevant cohort (per agreed design).
        public IList<TournamentPart> AvailableParts =>
            Tournament?.Parts?.ToList() ?? new List<TournamentPart>();

        public bool HasMultipleParts => (Tournament?.Parts?.Count ?? 0) > 1;

        public TournamentPart SelectedPart
        {
            get => _selectedPart;
            set
            {
                if (_selectedPart == value) return;
                _selectedPart = value;
                OnPropertyChanged(nameof(SelectedPart));
                Refresh();
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

            if (Tournament?.Parts != null && Tournament.Parts.Count > 0)
            {
                _selectedPart = Tournament.Parts.FirstOrDefault(p =>
                    Tournament.Groups.Any(g => g.PartID == p.ID && g.Bracket != null))
                    ?? Tournament.Parts[0];
            }
            else
            {
                _selectedPart = null;
            }
            OnPropertyChanged(nameof(AvailableParts));
            OnPropertyChanged(nameof(HasMultipleParts));
            OnPropertyChanged(nameof(SelectedPart));

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

            // Filter by selected part — achievements belong to a part because
            // each winner is a wrestler whose group has a PartID. Skip rows
            // whose group is in another part. Then group by AchievementType
            // so each nomination shows up once with all its winners (ties
            // produce >1 wrestler per category).
            var groupPartById = Tournament?.Groups?.ToDictionary(g => g.ID, g => g.PartID)
                ?? new Dictionary<System.Guid, System.Guid?>();

            Items = _resultsService.Achievements
                .Where(a => _selectedPart == null
                            || (a.Wrestler?.GroupID is System.Guid gid
                                && groupPartById.TryGetValue(gid, out var pid)
                                && pid == _selectedPart.ID))
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
