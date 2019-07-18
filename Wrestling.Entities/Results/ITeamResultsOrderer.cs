using System.Collections.Generic;

namespace Wrestling.Entities.Results
{
    public interface ITeamResultsOrderer
    {
        List<TournamentTeamResult> GetOrderedResults(IEnumerable<TournamentTeamResult> results);
    }
}