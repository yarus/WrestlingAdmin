using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities.Localization;

namespace Wrestling.Entities.Results.Achievements
{
    public class FastestActionAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => EntityLocalization.T("Achievement_FastestAction_Title", "Метеор");

        public string AchievementType => "FastestAction";
        public string AchievementDefinition => EntityLocalization.T("Achievement_FastestAction_Definition", "Борец, быстрее всех выполнивший результативное действие");

        public List<WrestlerAchievement> CalculateAchievement(Tournament tournament, List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            // Only wrestlers who actually scored an action qualify. Without one,
            // FastestActionSecond returns the per-group sentinel
            // (MaxRoundSecond * 2); FreeWin byes contain no actions. If nobody
            // has scored yet, there's no laureate — otherwise the whole field
            // would tie on the sentinel and all get crowned.
            var contenders = results
                .Where(r => r.Group != null && r.FastestActionSecond < r.Group.MaxRoundSecond * 2)
                .ToList();
            if (contenders.Count == 0) return null;

            var smallestResult = contenders.Min(r => r.FastestActionSecond);

            var finalResults =
                contenders
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
                    AchievementValue = string.Format(EntityLocalization.T("Achievement_Value_Seconds", "{0} (сек.)"), item.FastestActionSecond),
                    AchievementDefinition = AchievementDefinition
                });
            }

            return response;
        }
    }
}
