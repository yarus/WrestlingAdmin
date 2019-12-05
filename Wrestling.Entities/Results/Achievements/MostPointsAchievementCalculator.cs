using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Results.Achievements
{
    public class MostPointsCountAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => "Машина Борьбы";
        public string AchievementType => "MostPointsCount";

        public WrestlerAchievement CalculateAchievement(List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var result = results.Max(r => r.AllGainedPoints);

            var finalResult = results
                .Where(r => r.AllGainedPoints == result)
                .OrderBy(r => r.MatchesCount)
                .ThenByDescending(r => r.Wins)
                .ThenByDescending(r => r.Wrestler.BirthDate)
                .First();

            return new WrestlerAchievement
            {
                Title = AchievementTitle,
                Wrestler = finalResult.Wrestler,
                AchievementType = AchievementType,
                AchievementValue = finalResult.AllGainedPoints.ToString()
            };
        }
    }
}
