using System;
using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Bracket
{
    public static class GroupResultState
    {
        private static IEnumerable<WrestlingMatch> WithHistory(WrestlingMatch match)
        {
            yield return match;
            foreach (var previous in match.PreviousResults.SelectMany(WithHistory)) yield return previous;
        }

        public static bool RebuildExclusions(AgeWeightGroup group)
        {
            if (group.Bracket == null) return false;
            var dsq = new HashSet<Guid>();
            var absent = new HashSet<Guid>();
            foreach (var match in group.Bracket.Rounds.SelectMany(r => r.RoundMatches).SelectMany(WithHistory).Where(m => m.IsMatchCompleted))
            {
                var loser = match.IsRedWon == true ? match.WrestlerInBlue : match.IsRedWon == false ? match.WrestlerInRed : null;
                if (match.WinType is MatchWinTypeEnum.DisqualifyWin or MatchWinTypeEnum.WarningsLimit)
                { if (loser != null) dsq.Add(loser.ID); }
                else if (match.WinType == MatchWinTypeEnum.NoShow)
                { if (loser != null) absent.Add(loser.ID); }
                else if (match.WinType is MatchWinTypeEnum.MutualDisqualify or MatchWinTypeEnum.MutualNoShow)
                {
                    var set = match.WinType == MatchWinTypeEnum.MutualDisqualify ? dsq : absent;
                    if (match.WrestlerInRed != null) set.Add(match.WrestlerInRed.ID);
                    if (match.WrestlerInBlue != null) set.Add(match.WrestlerInBlue.ID);
                }
            }
            bool changed = false;
            foreach (var w in group.Wrestlers)
            {
                bool disqualified = dsq.Contains(w.ID), noShow = !disqualified && absent.Contains(w.ID);
                changed |= w.IsDisqualified != disqualified || w.IsNoShow != noShow;
                w.IsDisqualified = disqualified;
                w.IsNoShow = noShow;
            }
            return changed;
        }

    }
}
