using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities.Localization;

namespace Wrestling.Entities.Results.Achievements
{
    public class WinInLast10SecondsAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => EntityLocalization.T("Achievement_NeverGiveUp_Title", "Никогда не сдаваться");
        public string AchievementType => "NeverGiveUp";
        public string AchievementDefinition => EntityLocalization.T("Achievement_NeverGiveUp_Definition", "Борец, набравший победные баллы за последние 10 секунд схватки");

        public List<WrestlerAchievement> CalculateAchievement(Tournament tournament, List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var response = new List<WrestlerAchievement>();

            var allMatches = tournament.Groups
                .Where(g => g.Bracket != null)
                .SelectMany(g => g.Bracket.Rounds)
                .SelectMany(r => r.RoundMatches)
                .Where(m => m.Status == MatchStatusEnum.Completed && m.WrestlerInRed != null && m.WrestlerInBlue != null)                
                .ToList();            

            foreach (var result in results)
            {
                var wonMatches = allMatches
                    .Where(m => 
                        (m.LastSecondInMatch >= (result.Group.MaxRoundSecond * 2) - 10) && 
                        ((m.IsRedWinner && m.WrestlerInRed.SameAs(result.Wrestler)) || (m.IsBlueWon && m.WrestlerInBlue.SameAs(result.Wrestler)))
                    )
                    .ToList();                

                foreach (var match in wonMatches)
                {
                    int redPoints = 0;
                    int bluePoints = 0;

                    foreach(var action in match.MatchActions)
                    {
                        if (action.RoundNumber == 2 && action.SecondInRound >= (result.Group.MaxRoundSecond * 2) - 10)
                        {
                            break;
                        }

                        // Only real points actions count toward the «before
                        // last 10 seconds» score. Warnings and reverts must
                        // not inflate the running totals — they share the
                        // Points field but have a different Type.
                        if (action.Type != MatchActionType.SetPoints) continue;

                        if (action.IsForRed.HasValue && action.IsForRed.Value)
                        {
                            redPoints += action.Points;
                        }
                        else if (action.IsForRed.HasValue && !action.IsForRed.Value)
                        {
                            bluePoints += action.Points;
                        }
                    }

                    if ((redPoints < bluePoints && match.WrestlerInRed.SameAs(result.Wrestler) && match.IsRedWinner)
                        || (redPoints > bluePoints && match.WrestlerInBlue.SameAs(result.Wrestler) && match.IsBlueWon))
                    {
                        response.Add(new WrestlerAchievement
                        {
                            Title = AchievementTitle,
                            Wrestler = result.Wrestler,
                            AchievementType = AchievementType,
                            AchievementValue = string.Format(EntityLocalization.T("Achievement_Value_MatchNumber", "Схватка #{0}"), match.MatchNumber),
                            AchievementDefinition = AchievementDefinition
                        });
                    }
                }                
            }

            return response;
        }
    }
}
