using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Results.Achievements
{
    public class MostDominationWinsAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => "Доминатор";

        public string AchievementType => "MostDominationWins";

        public WrestlerAchievement CalculateAchievement(List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var result = results.Max(r => r.WinsByDomination);

            var finalResult = results
                .Where(r => r.WinsByDomination == result)
                .OrderBy(r => r.MatchesCount)
                .ThenByDescending(r => r.Wins)
                .ThenByDescending(r => r.Wrestler.BirthDate)
                .First();

            return new WrestlerAchievement
            {
                Title = AchievementTitle,
                Wrestler = finalResult.Wrestler,
                AchievementType = AchievementType,
                AchievementValue = finalResult.WinsByDomination.ToString()
            };
        }
    }
}
