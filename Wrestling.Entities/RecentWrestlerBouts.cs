using System;
using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities
{
    public static class RecentWrestlerBouts
    {
        // Read history once for the small set of athletes currently of interest.
        // PreviousResults contain actual bouts whose cells were later reused.
        public static IReadOnlyDictionary<Guid, DateTime> FindLastCompletions(
            IEnumerable<WrestlingMatch> matches, IEnumerable<Guid> athleteIds)
        {
            var wanted = new HashSet<Guid>(athleteIds);
            var result = new Dictionary<Guid, DateTime>();
            if (wanted.Count == 0) return result;
            var visited = new HashSet<WrestlingMatch>();
            void Visit(WrestlingMatch match)
            {
                if (!visited.Add(match)) return;
                foreach (var previous in match.PreviousResults) Visit(previous);
                if (!match.IsMatchCompleted || match.WinType == MatchWinTypeEnum.FreeWin
                    || match.WinType == MatchWinTypeEnum.NoShow || match.WinType == MatchWinTypeEnum.MutualNoShow)
                    return;
                var completed = match.MatchActions.Where(a => a.Type == MatchActionType.MatchCompleted)
                    .Select(a => (DateTime?)a.DateTime).Max();
                if (!completed.HasValue) return;
                foreach (var athlete in new[] { match.WrestlerInRed, match.WrestlerInBlue })
                {
                    if (athlete != null && wanted.Contains(athlete.ID)
                        && (!result.TryGetValue(athlete.ID, out var last) || completed.Value > last))
                        result[athlete.ID] = completed.Value;
                }
            }
            foreach (var match in matches) Visit(match);
            return result;
        }

        public static int WarningLevel(DateTime completed, DateTime now)
        {
            var elapsed = now - completed;
            return elapsed < TimeSpan.FromMinutes(5) ? 2 : elapsed < TimeSpan.FromMinutes(10) ? 1 : 0;
        }
    }
}
