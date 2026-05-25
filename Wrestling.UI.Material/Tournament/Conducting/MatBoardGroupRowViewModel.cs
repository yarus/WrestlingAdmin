using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Tournament.Conducting
{
    // One row in a Mat Board column. Carries enough state to render the group
    // line ("2014 38кг — 4/14 — [Move ▸]") plus the move dropdown bindings.
    // The owning MatBoardViewModel handles rebuilds after a successful move.
    public sealed class MatBoardGroupRowViewModel : ObservableObject
    {
        private readonly MatBoardViewModel _owner;
        private readonly AgeWeightGroup _group;

        public MatBoardGroupRowViewModel(MatBoardViewModel owner, AgeWeightGroup group)
        {
            _owner = owner;
            _group = group;
        }

        public AgeWeightGroup Group => _group;

        public string Name => _group.Name;

        public int PendingMatchesCount => _group.PendingMatchesCount;

        public int TotalMatchesCount =>
            _group.Bracket?.Rounds?.Sum(r => r.RoundMatches.Count) ?? 0;

        // Compact "done/total" label, e.g. "4/14". `MatchesCount` on the
        // group is the *active* count (not yet completed); subtracting from
        // total yields completed.
        public string ProgressLabel
        {
            get
            {
                var total = TotalMatchesCount;
                var done = total - PendingMatchesCount;
                return $"{done}/{total}";
            }
        }

        public bool HasLiveMatch
        {
            get
            {
                var live = FindLiveMatch();
                return live != null;
            }
        }

        // Used by the PopupBox listing destination mats. Rebuilt on demand so a
        // mat added mid-session is visible immediately.
        public IEnumerable<MatMoveTargetViewModel> OtherMats
        {
            get
            {
                var t = _owner.Tournament;
                if (t == null) yield break;
                foreach (var mat in t.Mats)
                {
                    if (mat.ID == _group.MatID) continue;
                    yield return new MatMoveTargetViewModel(mat, _group, _owner);
                }
                // "Открепить" option at the bottom — useful when the operator
                // wants to take a group off the rotation entirely instead of
                // moving it to a specific mat.
                if (_group.MatID.HasValue)
                {
                    yield return new MatMoveTargetViewModel(null, _group, _owner);
                }
            }
        }

        // Locates the pending match that has StartDateTime set — the one being
        // scored right now. Returned to surface in the block dialog.
        public WrestlingMatch FindLiveMatch()
        {
            if (_group?.Bracket?.Rounds == null) return null;
            foreach (var round in _group.Bracket.Rounds)
            {
                foreach (var match in round.RoundMatches)
                {
                    if (match.Status == MatchStatusEnum.Pending && match.StartDateTime.HasValue)
                    {
                        return match;
                    }
                }
            }
            return null;
        }
    }

    // Renders a single "Перенести на: Ковёр N" menu item. Holds the action so
    // the popup item itself is the command source.
    public sealed class MatMoveTargetViewModel
    {
        private readonly Mat _targetMat; // null = unbind
        private readonly AgeWeightGroup _group;
        private readonly MatBoardViewModel _owner;
        private ICommand _moveCommand;

        public MatMoveTargetViewModel(Mat targetMat, AgeWeightGroup group, MatBoardViewModel owner)
        {
            _targetMat = targetMat;
            _group = group;
            _owner = owner;
        }

        public string Label
        {
            get
            {
                if (_targetMat == null)
                {
                    return TournamentViewModelBase.T("MatBoard_MoveTo_Unbind", "Открепить");
                }
                var pending = _targetMat.Groups?.Sum(g => g.PendingMatchesCount) ?? 0;
                var hint = TournamentViewModelBase.T("MatBoard_MatFree_Hint", "свободен");
                return pending == 0 ? $"{_targetMat.Name}  ·  {hint}" : _targetMat.Name;
            }
        }

        public ICommand MoveCommand =>
            _moveCommand ?? (_moveCommand = new RelayCommand(_ => _owner.ExecuteMove(_group, _targetMat?.ID)));
    }
}
