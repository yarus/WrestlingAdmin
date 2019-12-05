using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Results.Achievements
{
    public class FastestActionAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => "Метеор";

        public string AchievementType => "FastestAction";

        public WrestlerAchievement CalculateAchievement(List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var smallestResult = results.Min(r => r.FastestActionSecond);

            var finalResult =
                results
                .Where(r => r.FastestActionSecond == smallestResult)
                .OrderByDescending(r => r.Wins)
                .ThenByDescending(r => r.Wrestler.BirthDate)
                .First();

            return new WrestlerAchievement
            {
                Title = AchievementTitle,
                Wrestler = finalResult.Wrestler,
                AchievementType = AchievementType,
                AchievementValue = finalResult.FastestActionSecond.ToString()
            };
        }
    }
}
