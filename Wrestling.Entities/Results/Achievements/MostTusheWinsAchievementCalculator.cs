using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities.Localization;

namespace Wrestling.Entities.Results.Achievements
{
    public class MostTusheWinsAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => EntityLocalization.T("Achievement_MostTushe_Title", "Асфальтоукладчик");

        public string AchievementType => "MostTusheWinsCount";
        public string AchievementDefinition => EntityLocalization.T("Achievement_MostTushe_Definition", "Борец, выигравший больше всего схваток по туше");

        public List<WrestlerAchievement> CalculateAchievement(Tournament tournament, List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var result = results.Max(r => r.WinsByTushe);

            var finalResults = results
                .Where(r => r.WinsByTushe == result)
                .OrderBy(r => r.MatchesCount)
                .ThenByDescending(r => r.Wins)
                .ThenByDescending(r => r.Wrestler.BirthDate)
                .ToList();

            var response = new List<WrestlerAchievement>();

            foreach(var item in finalResults)
            {
                response.Add(new WrestlerAchievement
                {
                    Title = AchievementTitle,
                    Wrestler = item.Wrestler,
                    AchievementType = AchievementType,
                    AchievementValue = item.WinsByTushe.ToString(),
                    AchievementDefinition = AchievementDefinition
                });
            }

            return response;
        }
    }
}
