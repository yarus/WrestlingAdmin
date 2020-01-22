using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Results.Achievements
{
    public class FastestActionAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => "Метеор";

        public string AchievementType => "FastestAction";
        public string AchievementDefinition => "Борец, быстрее всех выполнивший результативное действие";

        public List<WrestlerAchievement> CalculateAchievement(Tournament tournament, List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var smallestResult = results.Min(r => r.FastestActionSecond);

            var finalResults =
                results
                .Where(r => r.FastestActionSecond == smallestResult)
                .OrderByDescending(r => r.Wins)
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
                    AchievementValue = $"{item.FastestActionSecond} (сек.)",
                    AchievementDefinition = AchievementDefinition
                });
            }

            return response;
        }
    }
}
