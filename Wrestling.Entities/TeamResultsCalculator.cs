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
            var teamDict = new Dictionary<Guid, TeamDto>();

            foreach (var result in personalResults.Where(result => result.Wrestler.TeamID.HasValue))
            {
                if (!teamDict.ContainsKey(result.Wrestler.TeamID.Value))
                {
                    teamDict.Add(result.Wrestler.TeamID.Value, new TeamDto
                    {
                        City = result.Wrestler.TeamCity,
                        Name = result.Wrestler.TeamName,
                        TeamId = result.Wrestler.TeamID.Value
                    });
                }
            }

            var teamResults = teamDict.Select(teamDto => new TournamentTeamResult(teamDto.Key, teamDto.Value.Name, teamDto.Value.City, personalResults)).ToList();

            return orderer != null ? orderer.GetOrderedResults(teamResults) : teamResults;
        }

        private class TeamDto
        {
            public Guid TeamId { get; set; }
            public string Name { get; set; }
            public string City { get; set; }
        }
    }
}