using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Results
{
    public class PointsTeamResultsOrderer : ITeamResultsOrderer
    {
        public List<TournamentTeamResult> GetOrderedResults(IEnumerable<TournamentTeamResult> results)
        {
            return results?.OrderByDescending(r => r.TotalPoints)
                .ThenByDescending(r => r.GoldMedals)
                .ThenByDescending(r => r.SilverMedals)
                .ThenByDescending(r => r.BronzeMedals)
                .ThenBy(r => r.GroupCount)
                .ThenBy(r => r.Wrestlers.Count)
                .ToList();
        }
    }
}