using System;
using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Tournament.Standing.Mats
{
    // Per-(Part, Mat) panel used by the distribution section of MatsView —
    // wraps a Mat together with the slice of its groups belonging to the
    // currently-selected Part. Retained while the schedule page is open.
    public sealed class MatsPartMatPanelVm : ObservableObject
    {
        private IList<AgeWeightGroup> _groups;

        public Mat Mat { get; set; }
        public TournamentPart Part { get; set; }
        public bool IsExpanded { get; set; } = true;
        public IList<AgeWeightGroup> Groups
        {
            get => _groups;
            set
            {
                _groups = value;
                OnPropertyChanged(nameof(Groups));
                OnPropertyChanged(nameof(WrestlersCount));
                OnPropertyChanged(nameof(MatchesCount));
                OnPropertyChanged(nameof(ExpectedDurationSeconds));
                OnPropertyChanged(nameof(ExpectedDurationLabel));
            }
        }

        // Per-(Part, Mat) stats — same formulas as Mat.WrestlersCount /
        // .MatchesCount / .ExpectedDurationSeconds, but scoped to this
        // panel's groups.
        public int WrestlersCount => Groups?.Sum(g => g.Wrestlers.Count) ?? 0;
        public int MatchesCount => Groups?.Sum(g => g.PendingMatchesCount) ?? 0;
        public int ExpectedDurationSeconds =>
            Groups?.Sum(g => g.PendingMatchesCount * (g.MaxRoundSecond * 2 + g.MaxTimeoutSecond)) ?? 0;
        public string ExpectedDurationLabel => FormatDuration(ExpectedDurationSeconds);

        private static string FormatDuration(int seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            if ((int)ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}ч {ts.Minutes:D2}м";
            }
            return $"{ts.Minutes}м";
        }
    }
}
