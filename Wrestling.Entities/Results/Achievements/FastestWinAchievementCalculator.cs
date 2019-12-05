using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Results.Achievements
{
    public class FastestWinAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => "Молния";
        public string AchievementType => "FastestWin";

        public WrestlerAchievement CalculateAchievement(List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var smallestResult = results.Min(r => r.FastestWinSecond);
            
            var finalResult = 
                results
                .Where(r => r.FastestWinSecond == smallestResult)
                .OrderByDescending(r => r.Wins)
                .ThenByDescending(r => r.Wrestler.BirthDate)
                .First();

            return new WrestlerAchievement
            {
                Title = AchievementTitle,
                Wrestler = finalResult.Wrestler,
                AchievementType = AchievementType,
                AchievementValue = finalResult.FastestWinSecond.ToString()
            };
        }
    }
}
