using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Conducting
{
    // "Доска ковров" — full-screen overlay reached from the Conducting
    // dashboard. Wide table of mat columns with Live / Active / Completed
    // sections inside each column; per-group "Перенести на" popup runs the
    // move through IMatRedistributionService so the legacy «Расписание»
    // screen and this one share one mutation path.
    public sealed class MatBoardViewModel : TournamentViewModelBase
    {
        private IMatRedistributionService _redistribution;
        private IResultsService _resultsService;
        private bool _resultsSubscribed;
        private TournamentPart _selectedPart;

        public MatBoardViewModel(IDiContainer container) : base(container)
        {
            Columns = new ObservableCollection<MatBoardColumnViewModel>();
            AvailableParts = new List<TournamentPart>();
        }

        public override string PageTitle => T("MatBoard_PageTitle", "Доска ковров");
        public override bool IsBackButtonAvailable => true;

        protected override void OnBackCommand()
        {
            NavigateToView<ConductingViewModel>();
        }

        public ObservableCollection<MatBoardColumnViewModel> Columns { get; }

        public bool HasMats => Columns.Count > 0;

        // Part scope for redistribution — when the tournament has 2+ parts
        // the operator picks one, and every column is filtered to that
        // part's groups only. This prevents cross-part moves (which would
        // silently rewrite the source part's standings) and matches the
        // mental model "I'm rebalancing Part 1's load across mats".
        public IList<TournamentPart> AvailableParts { get; private set; }
        public bool HasMultipleParts => AvailableParts != null && AvailableParts.Count > 1;

        public TournamentPart SelectedPart
        {
            get => _selectedPart;
            set
            {
                if (_selectedPart == value) return;
                _selectedPart = value;
                OnPropertyChanged(nameof(SelectedPart));
                RebuildColumns();
            }
        }

        public override void InitData()
        {
            base.InitData();

            if (_redistribution == null)
            {
                _redistribution = Resolve<IMatRedistributionService>();
            }

            if (_resultsService == null)
            {
                _resultsService = Resolve<IResultsService>();
            }

            if (_resultsService != null && !_resultsSubscribed)
            {
                _resultsService.ResultsChanged += OnResultsChanged;
                _resultsSubscribed = true;
            }

            // Snapshot parts + pick a sensible default. Prefer the first
            // part that actually has groups so the screen isn't empty for
            // a freshly-added (still-empty) trailing part.
            var t = DataContext?.Tournament;
            AvailableParts = t?.Parts?.ToList() ?? new List<TournamentPart>();
            OnPropertyChanged(nameof(AvailableParts));
            OnPropertyChanged(nameof(HasMultipleParts));

            if (AvailableParts.Count > 0 && t != null)
            {
                // Default to the part most mats are currently running (their
                // ActivePartID) — that's what the operator is most likely
                // here to rebalance. Fall back to the first part with groups,
                // then the first part.
                _selectedPart = MostCommonActivePart(t)
                                ?? AvailableParts.FirstOrDefault(p => t.Groups.Any(g => g.PartID == p.ID))
                                ?? AvailableParts[0];
            }
            else
            {
                _selectedPart = null;
            }
            OnPropertyChanged(nameof(SelectedPart));

            RebuildColumns();
        }

        public override void OnNavigationCompleted()
        {
            base.OnNavigationCompleted();
            RebuildColumns();
        }

        // The TournamentPart that the most mats currently have as their
        // ActivePartID. Null when no mat has a (resolvable) active part.
        private TournamentPart MostCommonActivePart(Entities.Tournament t)
        {
            if (t?.Mats == null || t.Parts == null) return null;
            return t.Mats
                .Where(m => m.ActivePartID.HasValue)
                .GroupBy(m => m.ActivePartID.Value)
                .OrderByDescending(g => g.Count())
                .Select(g => t.Parts.FirstOrDefault(p => p.ID == g.Key))
                .FirstOrDefault(p => p != null);
        }

        private void OnResultsChanged() => RebuildColumns();

        // Refresh every mat's per-group counters before rebuilding the
        // columns so the on-screen "X/Y" labels reflect the latest state
        // (matches that completed via Approve on another screen).
        private void RebuildColumns()
        {
            Columns.Clear();
            var t = DataContext?.Tournament;
            if (t == null)
            {
                OnPropertyChanged(nameof(HasMats));
                return;
            }

            foreach (var mat in t.Mats)
            {
                mat.RefreshStats();
                // Filter by the operator-selected viewing part — every
                // column shows only the slice of groups in that part, so
                // group moves between mats stay within one part. When no
                // part is selected (single-part tournament or pre-Parts
                // legacy data) show everything.
                var rows = mat.Groups
                    .Where(g => g.Bracket != null
                                && (_selectedPart == null || g.PartID == _selectedPart.ID))
                    .Select(g => new MatBoardGroupRowViewModel(this, g))
                    .ToList();
                Columns.Add(new MatBoardColumnViewModel(mat, rows, t, AdvanceMatToNextPart));
            }

            OnPropertyChanged(nameof(HasMats));
        }

        // Same callback shape used by Conducting Z2 cards — flips a single
        // mat to the next part by Order. Bumps Mat.FieldsVersion so peers
        // pick the change up through ApplyMatFieldChanges. Rebuilds columns
        // afterwards so the new active-part filter takes effect immediately.
        private void AdvanceMatToNextPart(Mat mat, TournamentPart nextPart)
        {
            if (mat == null || nextPart == null) return;
            mat.ActivePartID = nextPart.ID;
            mat.FieldsVersion++;
            ShowSnackMessage(string.Format(
                T("Conducting_AdvancePart_Snack", "Ковёр «{0}» переключён на часть «{1}»"),
                mat.Name, nextPart.Name));
            RebuildColumns();
        }

        // Invoked by the per-row "Перенести на" menu. Handles the three move
        // outcomes: success (snackbar + rebuild), blocked-by-live (modal
        // explaining which match), no-op (silent).
        internal void ExecuteMove(AgeWeightGroup group, Guid? targetMatId)
        {
            var t = DataContext?.Tournament;
            if (t == null || _redistribution == null || group == null) return;

            var result = _redistribution.MoveGroupToMat(t, group, targetMatId);
            switch (result.Outcome)
            {
                case MoveOutcome.Moved:
                    RebuildColumns();
                    var targetName = targetMatId.HasValue
                        ? t.Mats.FirstOrDefault(m => m.ID == targetMatId.Value)?.Name
                        : T("MatBoard_MoveTo_Unbind", "Открепить");
                    ShowSnackMessage(string.Format(
                        T("MatBoard_Snack_Moved", "Группа «{0}» перенесена: {1}"),
                        group.Name, targetName));
                    break;

                case MoveOutcome.BlockedByLiveMatch:
                    var matchNo = result.LiveMatch?.MatchNumber ?? 0;
                    var red = result.LiveMatch?.WrestlerInRed?.LastFirstNameShort ?? "—";
                    var blue = result.LiveMatch?.WrestlerInBlue?.LastFirstNameShort ?? "—";
                    var body = string.Format(
                        T("MatBoard_LiveMatchBlock_Body",
                            "Сейчас идёт схватка №{0} ({1} vs {2}). Дождитесь Approve или нажмите Revert, прежде чем переносить группу."),
                        matchNo, red, blue);
                    Dialog.ShowMessageBox(this, body,
                        T("MatBoard_LiveMatchBlock_Title", "Группа в работе"),
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    break;

                case MoveOutcome.NoChange:
                default:
                    break;
            }
        }
    }
}
