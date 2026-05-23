using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities.Localization;

namespace Wrestling.Entities.Results.Achievements
{
    public class MostDominationWinsAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => EntityLocalization.T("Achievement_MostDomination_Title", "Доминатор");

        public string AchievementType => "MostDominationWins";
        public string AchievementDefinition => EntityLocalization.T("Achievement_MostDomination_Definition", "Борец, выигравший больше всего схваток по техническому превосходству");

        public List<WrestlerAchievement> CalculateAchievement(Tournament tournament, List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var result = results.Max(r => r.WinsByDomination);

            var finalResults = results
                .Where(r => r.WinsByDomination == result)
                .OrderBy(r => r.MatchesCount)
                .ThenByDescending(r => r.Wins)
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
                    AchievementValue = item.WinsByDomination.ToString(),
                    AchievementDefinition = AchievementDefinition
                });
            }

            return response;
        }
    }
}
