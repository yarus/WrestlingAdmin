using System;
using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities.Results;

namespace Wrestling.Entities
{
    public class TeamResultsCalculator : ITeamResultsCalculator
    {
        public List<TournamentTeamResult> GetTeamResults(List<TournamentResult> personalResults, ITeamResultsOrderer orderer)
        {
            var teamResults = new List<TournamentTeamResult>();

            var teamNames = personalResults.Select(r => r.Wrestler.TeamName).Distinct().ToList();

            var teamIds = personalResults.Select(r => r.Wrestler.TeamID).Distinct().ToList();            

            if (teamNames.Count != teamIds.Count) throw new ApplicationException("Team Ids count not equal to Team names count in TeamResultsCalculator");

            for (int i = 0; i < teamIds.Count; i++)
            {
                if (!teamIds[i].HasValue) continue;

                var result = new TournamentTeamResult(teamIds[i].Value, teamNames[i], personalResults);

                teamResults.Add(result);
            }
            /*
            foreach (var result in personalResults)
            {
                var team = teamResults.FirstOrDefault(r => r.TeamID == result.Wrestler.TeamID);

                if (team == null)
                {
                    team = new TournamentTeamResult { TeamID = result.Wrestler.TeamID, TeamName = result.Wrestler.TeamName };
                    teamResults.Add(team);
                }

                if (result.Wrestler.FinalPlace == 1)
                {
                    team.GoldMedals++;
                }
                else if (result.Wrestler.FinalPlace == 2)
                {
                    team.SilverMedals++;
                }
                else if (result.Wrestler.FinalPlace == 3)
                {
                    team.BronzeMedals++;
                }

                team.Wrestlers++;
            }

            foreach (var result in teamResults)
            {
                result.GroupCount = personalResults.Where(r => r.Wrestler.TeamID == result.TeamID).Select(g => g.GroupName).ToList().Distinct().Count();
            }
            */

            return orderer != null ? orderer.GetOrderedResults(teamResults) : teamResults;
        }
    }
}