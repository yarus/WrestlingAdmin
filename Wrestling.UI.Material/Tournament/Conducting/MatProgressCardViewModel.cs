using System;
using System.Linq;
using System.Windows.Input;
using Wrestling.Entities;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Conducting
{
    // Lightweight per-mat view-model for Conducting Z2 cards. Wraps a Mat
    // with computed labels that depend on tournament-wide state (Parts)
    // which the Mat entity doesn't carry directly. Rebuilt on every refresh
    // tick — cheap, and avoids leaking PropertyChanged plumbing into Mat.
    public sealed class MatProgressCardViewModel
    {
        private readonly Action<Mat, TournamentPart> _advance;
        private ICommand _advanceCommand;

        public MatProgressCardViewModel(Mat mat, Entities.Tournament tournament, Action<Mat, TournamentPart> advance)
        {
            Mat = mat;
            _advance = advance;
            ActivePartName = ResolveActivePartName(mat, tournament);
            HasMultipleParts = (tournament?.Parts?.Count ?? 0) > 1;
            NextPart = ResolveNextPart(mat, tournament);
            CanAdvance = HasMultipleParts && NextPart != null && AllGroupsCompletedInActivePart(mat);
        }

        public Mat Mat { get; }
        public string Name => Mat?.Name;
        public string ProgressLabel => Mat?.ProgressLabel;
        public string ExpectedDurationLabel => Mat?.ExpectedDurationLabel;
        public string ActivePartName { get; }
        public bool HasMultipleParts { get; }
        public TournamentPart NextPart { get; }
        public string NextPartName => NextPart?.Name;
        public bool CanAdvance { get; }

        public ICommand AdvanceCommand =>
            _advanceCommand ?? (_advanceCommand = new RelayCommand(
                _ => _advance?.Invoke(Mat, NextPart),
                _ => CanAdvance));

        private static string ResolveActivePartName(Mat mat, Entities.Tournament t)
        {
            if (mat == null || t?.Parts == null) return null;
            if (!mat.ActivePartID.HasValue) return null;
            var part = t.Parts.FirstOrDefault(p => p.ID == mat.ActivePartID.Value);
            return part?.Name;
        }

        // "Next" is the next part by collection order (Parts has no
        // explicit Order field — insertion position is the source of truth).
        // Returns null when active is the last, when active is unknown, or
        // when no parts exist.
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

        // Banner condition: every group of the active part on this mat has
        // zero pending matches. Empty mat (no groups in active part) also
        // qualifies — the operator is ready to flip to the next part.
        private static bool AllGroupsCompletedInActivePart(Mat mat)
        {
            if (mat?.Groups == null || !mat.ActivePartID.HasValue) return false;
            return mat.Groups
                .Where(g => g.PartID == mat.ActivePartID.Value)
                .All(g => g.PendingMatchesCount == 0);
        }
    }
}
