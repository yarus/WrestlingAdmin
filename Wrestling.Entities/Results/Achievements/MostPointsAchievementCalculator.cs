using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Results.Achievements
{
    public class MostPointsCountAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => "Машина Борьбы";
        public string AchievementType => "MostPointsCount";
        public string AchievementDefinition => "Борец, набравший больше всех баллов за турнир";

        public List<WrestlerAchievement> CalculateAchievement(Tournament tournament, List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var result = results.Max(r => r.AllGainedPoints);

            var finalResults = results
                .Where(r => r.AllGainedPoints == result)
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
                    AchievementValue = item.AllGainedPoints.ToString(),
                    AchievementDefinition = AchievementDefinition
                });
            }

            return response;
        }
    }
}
