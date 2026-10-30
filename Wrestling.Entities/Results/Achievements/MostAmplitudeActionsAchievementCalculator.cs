using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities.Localization;

namespace Wrestling.Entities.Results.Achievements
{
    public class MostAmplitudeActionsAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => EntityLocalization.T("Achievement_MostAmplitude_Title", "Космодром");
        public string AchievementType => "MostAmplitudeActions";
        public string AchievementDefinition => EntityLocalization.T("Achievement_MostAmplitude_Definition", "Борец, выполнивший больше всех 4-бальных бросков за турнир");

        public List<WrestlerAchievement> CalculateAchievement(Tournament tournament, List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var result = results.Max(r => r.NumberOfAmplitudeActions);

            // No 4-point throw landed yet — don't crown the whole field on a
            // 0–0 tie. FreeWin byes contain no actions, so never reach this count.
            if (result <= 0) return null;

            var finalResults = results
                .Where(r => r.NumberOfAmplitudeActions == result)
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
                    AchievementValue = item.NumberOfAmplitudeActions.ToString(),
                    AchievementDefinition = AchievementDefinition
                });
            }

            return response;
        }
    }
}
