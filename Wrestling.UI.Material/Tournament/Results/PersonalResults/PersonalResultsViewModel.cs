using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Wrestling.Entities;
using Wrestling.Entities.Results;
using Wrestling.Providers;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Tournament.Results.PersonalResults
{
    public class PersonalResultsViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        // T inherited from TournamentViewModelBase.
        public string PageName => T("Personal_PageName", "Личные");


        private IResultsService _resultsService;
        private bool _resultsSubscribed;

        private IList<WeightCategoryResultsViewModel> _items;
        private string _filterString;
        private bool _isOnlyMedalsVisible;
        private TournamentPart _selectedPart;

        public PersonalResultsViewModel(IDiContainer container) : base(container)
        {
        }

        public override bool IsBackButtonAvailable => true;

        public override string PageTitle => T("Personal_PageTitle", "Личные итоги");

        public IList<WeightCategoryResultsViewModel> Items
        {
            get => _items;
            private set
            {
                _items = value;
                OnPropertyChanged(nameof(Items));
                OnPropertyChanged(nameof(ShouldAutoExpand));
            }
        }

        public string FilterString
        {
            get => _filterString;
            set
            {
                if (_filterString == value) return;
                var prev = _filterString;
                _filterString = value;
                OnPropertyChanged(nameof(FilterString));
                OnPropertyChanged(nameof(IsFilterEnabled));

                var len = value?.Length ?? 0;
                var prevLen = prev?.Length ?? 0;
                // Mirror existing UX from ApplicationsView/old ResultsViewModel:
                // only re-filter on ≥3 char input or a clear after a meaningful query.
                if (prevLen > 2 && len == 0 || len > 2)
                {
                    Rebuild();
                }
            }
        }

        public bool IsOnlyMedalsVisible
        {
            get => _isOnlyMedalsVisible;
            set
            {
                if (_isOnlyMedalsVisible == value) return;
                _isOnlyMedalsVisible = value;
                OnPropertyChanged(nameof(IsOnlyMedalsVisible));
                OnPropertyChanged(nameof(IsFilterEnabled));
                Rebuild();
            }
        }

        public bool IsFilterEnabled =>
            (!string.IsNullOrEmpty(FilterString) && FilterString.Length > 2) || IsOnlyMedalsVisible;

        // Local part filter — independent of any mat's active part. Defaults
        // in InitData to the first part with pending matches (the part the
        // operator is most likely planning awards for); the user can switch
        // freely without affecting any other screen.
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
                Rebuild();
            }
        }

        // Auto-expand when filter is on and the result set is small enough to
        // browse at a glance — same logic as ApplicationsView.
        public bool ShouldAutoExpand
        {
            get
            {
                if (!IsFilterEnabled || _items == null || _items.Count == 0) return false;
                if (_items.Count == 1) return true;
                return _items.Sum(i => i.Wrestlers.Count) <= 5;
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

            // Default to the first non-empty part so the operator sees a
            // useful screen on entry instead of an empty list when picking
            // an arbitrary part. Falls back to Parts[0] if every part is
            // empty (unusual mid-setup state).
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

            Rebuild();
        }

        protected override void OnBackCommand()
        {
            // Phase 6 wrapper handles navigation; sub-page back is no-op.
        }

        private void OnResultsChanged()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            if (_resultsService == null || Tournament == null)
            {
                Items = new List<WeightCategoryResultsViewModel>();
                return;
            }

            var groups = Tournament.Groups?
                .Where(g => _selectedPart == null || g.PartID == _selectedPart.ID)
                .OrderBy(g => g.BirthYearMin ?? int.MaxValue)
                .ThenBy(g => g.BirthYearMax ?? int.MaxValue)
                .ThenBy(g => g.WeightMax ?? double.MaxValue)
                .ToList() ?? new List<AgeWeightGroup>();
            var resultsByGroup = _resultsService.AllResults
                .Where(r => r.Group != null)
                .GroupBy(r => r.Group)
                .ToDictionary(g => g.Key, g => g.ToList());

            var rows = new List<WeightCategoryResultsViewModel>();
            foreach (var group in groups)
            {
                if (!resultsByGroup.TryGetValue(group, out var groupResults)) continue;

                var visible = groupResults
                    .Where(r => !IsOnlyMedalsVisible
                                || (r.Wrestler.FinalPlace.HasValue && r.Wrestler.FinalPlace.Value <= 3))
                    .Where(r => string.IsNullOrEmpty(FilterString)
                                || FilterString.Length <= 2
                                || (!string.IsNullOrEmpty(r.Wrestler.LastName)
                                    && r.Wrestler.LastName.StartsWith(FilterString, true, CultureInfo.InvariantCulture)))
                    .OrderBy(r => r.Wrestler.FinalPlace ?? int.MaxValue)
                    .ThenBy(r => r.Wrestler.LastFirstName)
                    .ToList();

                // Hide empty groups when a filter is active, otherwise keep
                // the full slot list so the user sees every category at a glance.
                if (visible.Count == 0 && IsFilterEnabled) continue;

                rows.Add(new WeightCategoryResultsViewModel(group, groupResults, visible));
            }

            Items = rows;
        }
    }

    public class WeightCategoryResultsViewModel
    {
        public WeightCategoryResultsViewModel(AgeWeightGroup category, IList<TournamentResult> allResults, IList<TournamentResult> visibleWrestlers)
        {
            Category = category;
            Wrestlers = visibleWrestlers ?? new List<TournamentResult>();

            // Header counters stay tied to the full category roster — they
            // describe the category, not the current filter view.
            var roster = allResults ?? new List<TournamentResult>();
            WrestlersCount = roster.Count;
            MedalsCount = roster.Count(w =>
                w.Wrestler.FinalPlace.HasValue && w.Wrestler.FinalPlace.Value <= 3);
        }

        public AgeWeightGroup Category { get; }

        public IList<TournamentResult> Wrestlers { get; }

        public string Name => Category?.Name ?? string.Empty;

        public string BracketLabel => Category?.Bracket?.BracketTypeDisplay ?? string.Empty;

        public int WrestlersCount { get; }

        public int MedalsCount { get; }
    }
}
