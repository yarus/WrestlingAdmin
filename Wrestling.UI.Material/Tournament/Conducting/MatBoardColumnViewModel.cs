using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Wrestling.Entities;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Tournament.Conducting
{
    // One column on the Mat Board — represents a single mat with its groups
    // segmented into Live / Active / Completed sections. Rebuilt on every
    // refresh tick; cheap because per-mat group counts are small.
    public sealed class MatBoardColumnViewModel : ObservableObject
    {
        private readonly Mat _mat;
        private readonly List<MatBoardGroupRowViewModel> _live;
        private readonly List<MatBoardGroupRowViewModel> _active;
        private readonly List<MatBoardGroupRowViewModel> _completed;
        private readonly Action<Mat, TournamentPart> _advance;
        private ICommand _advanceCommand;

        public MatBoardColumnViewModel(Mat mat, IEnumerable<MatBoardGroupRowViewModel> rows)
            : this(mat, rows, tournament: null, advance: null) { }

        public MatBoardColumnViewModel(
            Mat mat,
            IEnumerable<MatBoardGroupRowViewModel> rows,
            Entities.Tournament tournament,
            Action<Mat, TournamentPart> advance)
        {
            _mat = mat;
            _advance = advance;
            var rowList = rows?.ToList() ?? new List<MatBoardGroupRowViewModel>();
            _live = rowList.Where(r => r.HasLiveMatch).ToList();
            _active = rowList.Where(r => !r.HasLiveMatch && r.PendingMatchesCount > 0).ToList();
            _completed = rowList.Where(r => r.PendingMatchesCount == 0 && r.TotalMatchesCount > 0).ToList();

            ActivePartName = ResolveActivePartName(mat, tournament);
            HasMultipleParts = (tournament?.Parts?.Count ?? 0) > 1;
            NextPart = ResolveNextPart(mat, tournament);
            // Banner condition matches Conducting Z2: the column's filtered
            // rows are already part-scoped (RebuildColumns filters by
            // mat.ActivePartID), so an empty Live+Active set means "this
            // mat is done with its current part" and the operator can flip.
            CanAdvance = HasMultipleParts && NextPart != null && _live.Count == 0 && _active.Count == 0;
        }

        public Mat Mat => _mat;
        public string Name => _mat.Name;

        public int PendingMatchesCount => _mat.Groups?.Sum(g => g.PendingMatchesCount) ?? 0;
        public int TotalMatchesCount =>
            _mat.Groups?.Sum(g => g.Bracket?.Rounds?.Sum(r => r.RoundMatches.Count) ?? 0) ?? 0;

        public string ProgressLabel
        {
            get
            {
                var total = TotalMatchesCount;
                var done = total - PendingMatchesCount;
                return $"{done}/{total}";
            }
        }

        // Free mat is the prime drop target — operator looks for these when
        // looking to redistribute load. Empty list (no groups) also qualifies.
        public bool IsFree => PendingMatchesCount == 0;

        public string FreeHint => TournamentViewModelBase.T("MatBoard_Column_FreeHint", "свободен — примет любую группу");

        public IReadOnlyList<MatBoardGroupRowViewModel> LiveRows => _live;
        public IReadOnlyList<MatBoardGroupRowViewModel> ActiveRows => _active;
        public IReadOnlyList<MatBoardGroupRowViewModel> CompletedRows => _completed;

        public bool HasLive => _live.Count > 0;
        public bool HasActive => _active.Count > 0;
        public bool HasCompleted => _completed.Count > 0;

        // Per-column part awareness — same shape as MatProgressCardViewModel
        // for symmetry. CanAdvance gates the column-header banner.
        public string ActivePartName { get; }
        public bool HasMultipleParts { get; }
        public TournamentPart NextPart { get; }
        public string NextPartName => NextPart?.Name;
        public bool CanAdvance { get; }

        public ICommand AdvanceCommand =>
            _advanceCommand ?? (_advanceCommand = new RelayCommand(
                _ => _advance?.Invoke(_mat, NextPart),
                _ => CanAdvance));

        private static string ResolveActivePartName(Mat mat, Entities.Tournament t)
        {
            if (mat == null || t?.Parts == null) return null;
            if (!mat.ActivePartID.HasValue) return null;
            var part = t.Parts.FirstOrDefault(p => p.ID == mat.ActivePartID.Value);
            return part?.Name;
        }

        private static TournamentPart ResolveNextPart(Mat mat, Entities.Tournament t)
        {
            if (mat == null || t?.Parts == null || t.Parts.Count == 0) return null;
            if (!mat.ActivePartID.HasValue) return null;
            for (int i = 0; i < t.Parts.Count - 1; i++)
            {
                if (t.Parts[i].ID == mat.ActivePartID.Value) return t.Parts[i + 1];
            }
            return null;
        }
    }
}
