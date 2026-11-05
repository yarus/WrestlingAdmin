using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Tournament.Progress.Schedule
{
    public sealed class ScheduleMatchRow
    {
        public WrestlingMatch Match { get; }
        public int MatchNumber => Match.MatchNumber;
        public int RestWarningLevel { get; }
        public bool HasRestWarning => RestWarningLevel > 0;
        public Brush RestWarningBrush => RestWarningLevel == 2 ? Brushes.Firebrick : Brushes.DarkGoldenrod;
        public string RestWarningText { get; }

        internal ScheduleMatchRow(WrestlingMatch match, IReadOnlyDictionary<Guid, DateTime> lastCompletions,
            DateTime now, bool showWarning)
        {
            Match = match;
            var lines = new List<string>();
            if (showWarning)
            {
                foreach (var athlete in new[] { match.WrestlerInRed, match.WrestlerInBlue }.Where(w => w != null))
                {
                    if (!lastCompletions.TryGetValue(athlete.ID, out var completed)) continue;
                    int level = RecentWrestlerBouts.WarningLevel(completed, now);
                    if (level == 0) continue;
                    RestWarningLevel = Math.Max(RestWarningLevel, level);
                    var minutes = Math.Max(0, (int)(now - completed).TotalMinutes);
                    lines.Add(string.Format(TournamentViewModelBase.T("Schedule_RecentBout_Tooltip",
                        "{0}: после завершения схватки прошло {1} мин."), athlete.FullName, minutes));
                }
            }
            RestWarningText = string.Join(Environment.NewLine, lines);
        }
    }
}
