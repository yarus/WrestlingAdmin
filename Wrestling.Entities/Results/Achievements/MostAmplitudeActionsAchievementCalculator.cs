using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Results.Achievements
{
    public class MostAmplitudeActionsAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => "Космодром";
        public string AchievementType => "MostAmplitudeActions";
        public string AchievementDefinition => "Борец, выполнивший больше всех 4-бальных бросков за турнир";

        public List<WrestlerAchievement> CalculateAchievement(Tournament tournament, List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var result = results.Max(r => r.NumberOfAmplitudeActions);

            var finalResults = results
                .Where(r => r.NumberOfAmplitudeActions == result)
                .OrderBy(r => r.MatchesCount)
                .ThenByDescending(r => r.Wins)
                .ThenByDescending(r => r.Wrestler.BirthDate)
                .ToList();

            var response = new List<WrestlerAchievement>();

            foreach (var item in finalResults)
            {
                response.Add(new WrestlerAchievement
                {
                    Title = AchievementTitle,
                    Wrestler = item.Wrestler,
                    AchievementType = AchievementType,
                    AchievementValue = item.NumberOfAmplitudeActions.ToString(),
                    AchievementDefinition = AchievementDefinition
                });
            }

            return response;
        }
    }
}
