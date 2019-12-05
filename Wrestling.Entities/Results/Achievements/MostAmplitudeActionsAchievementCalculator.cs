using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Results.Achievements
{
    public class MostAmplitudeActionsAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => "Космодром";
        public string AchievementType => "MostAmplitudeActions";

        public WrestlerAchievement CalculateAchievement(List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var result = results.Max(r => r.NumberOfAmplitudeActions);

            var finalResult = results
                .Where(r => r.NumberOfAmplitudeActions == result)
                .OrderBy(r => r.MatchesCount)
                .ThenByDescending(r => r.Wins)
                .ThenByDescending(r => r.Wrestler.BirthDate)
                .First();

            if (finalResult == null)
            {
                return null;
            }

            return new WrestlerAchievement
            {
                Title = AchievementTitle,
                Wrestler = finalResult.Wrestler,
                AchievementType = AchievementType,
                AchievementValue = finalResult.NumberOfAmplitudeActions.ToString()
            };
        }
    }
}
