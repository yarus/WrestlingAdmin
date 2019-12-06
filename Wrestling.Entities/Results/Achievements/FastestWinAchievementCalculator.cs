using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Results.Achievements
{
    public class FastestWinAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => "Молния";
        public string AchievementType => "FastestWin";
        public string AchievementDefinition => "Борец, выигравший схватку быстрее всех";

        public List<WrestlerAchievement> CalculateAchievement(Tournament tournament, List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var smallestResult = results.Min(r => r.FastestWinSecond);
            
            var finalResults = 
                results
                .Where(r => r.FastestWinSecond == smallestResult)
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
                    AchievementValue = $"{item.FastestWinSecond} (сек.)",
                    AchievementDefinition = AchievementDefinition
                });
            }

            return response;
        }
    }
}
