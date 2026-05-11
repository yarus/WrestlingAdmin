using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities.Localization;

namespace Wrestling.Entities.Results.Achievements
{
    public class FastestWinAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => EntityLocalization.T("Achievement_FastestWin_Title", "Молния");
        public string AchievementType => "FastestWin";
        public string AchievementDefinition => EntityLocalization.T("Achievement_FastestWin_Definition", "Борец, выигравший схватку быстрее всех");

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
                    AchievementValue = string.Format(EntityLocalization.T("Achievement_Value_Seconds", "{0} (сек.)"), item.FastestWinSecond),
                    AchievementDefinition = AchievementDefinition
                });
            }

            return response;
        }
    }
}
