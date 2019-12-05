using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Results.Achievements
{
    public class MostTusheWinsAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => "Асфальтоукладчик";

        public string AchievementType => "MostTusheWinsCount";

        public WrestlerAchievement CalculateAchievement(List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var result = results.Max(r => r.WinsByTushe);

            var finalResult = results
                .Where(r => r.WinsByTushe == result)
                .OrderBy(r => r.MatchesCount)
                .ThenByDescending(r => r.Wins)
                .ThenByDescending(r => r.Wrestler.BirthDate)
                .First();

            return new WrestlerAchievement
            {
                Title = AchievementTitle,
                Wrestler = finalResult.Wrestler,
                AchievementType = AchievementType,
                AchievementValue = finalResult.WinsByTushe.ToString()
            };
        }
    }
}
