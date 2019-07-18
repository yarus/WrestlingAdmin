using System.Collections.Generic;

namespace Wrestling.Entities.Results
{
    public interface ITeamResultsCalculator
    {
        List<TournamentTeamResult> GetTeamResults(List<TournamentResult> personalResults, ITeamResultsOrderer orderer);
    }
}