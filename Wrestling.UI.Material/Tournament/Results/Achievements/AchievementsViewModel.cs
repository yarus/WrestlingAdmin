using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Results.Achievements
{
    public class AchievementsViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        public string PageName => "Достижения";


        private IResultsService _resultsService;
        private bool _resultsSubscribed;

        private IList<AchievementCategoryViewModel> _items;

        public AchievementsViewModel(IDiContainer container) : base(container)
        {
        }

        public override bool IsBackButtonAvailable => true;

        public override string PageTitle => "Достижения спортсменов";

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

        private void Refresh()
        {
            if (_resultsService == null)
            {
                Items = new List<AchievementCategoryViewModel>();
                return;
            }

            // Group by AchievementType so each nomination shows up once with
            // all its winners (ties produce >1 wrestler per category).
            Items = _resultsService.Achievements
                .GroupBy(a => a.AchievementType)
                .Select(g => new AchievementCategoryViewModel(
                    title: g.First().Title,
                    definition: g.First().AchievementDefinition,
                    achievementType: g.Key,
                    winners: g.ToList()))
                .ToList();
        }
    }

    public class AchievementCategoryViewModel
    {
        public AchievementCategoryViewModel(string title, string definition, string achievementType, IList<WrestlerAchievement> winners)
        {
            Title = title;
            Definition = definition;
            AchievementType = achievementType;
            Winners = winners ?? new List<WrestlerAchievement>();
        }

        public string Title { get; }
        public string Definition { get; }
        public string AchievementType { get; }
        public IList<WrestlerAchievement> Winners { get; }

        public int WinnersCount => Winners.Count;
    }
}
