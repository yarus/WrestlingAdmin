using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Results.Achievements
{
    public class WinInLast10SecondsAchievementCalculator : IAchievementCalculator
    {
        public string AchievementTitle => "Никогда не сдаваться";
        public string AchievementType => "NeverGiveUp";
        public string AchievementDefinition => "Борец, набравший победные баллы за последние 10 секунд схватки";

        public List<WrestlerAchievement> CalculateAchievement(Tournament tournament, List<TournamentResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return null;
            }

            var response = new List<WrestlerAchievement>();

            var allMatches = tournament.Groups.SelectMany(g => g.Bracket.Rounds).SelectMany(r => r.RoundMatches)
                .Where(m => m.Status == MatchStatusEnum.Completed && m.WrestlerInRed != null && m.WrestlerInBlue != null)                
                .ToList();            

            foreach (var result in results)
            {
                var wonMatches = allMatches
                    .Where(m => 
                        (m.LastSecondInMatch >= (result.Group.MaxRoundSecond * 2) - 10) && 
                        ((m.IsRedWon.HasValue && m.IsRedWon.Value && m.WrestlerInRed == result.Wrestler) || (m.IsRedWon.HasValue && !m.IsRedWon.Value && m.WrestlerInBlue == result.Wrestler))                        
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

                        if (action.Points > 0 || action.Points < 0)
                        {
                            if (action.IsForRed.HasValue && action.IsForRed.Value)
                            {
                                redPoints += action.Points;
                            }
                            else if (action.IsForRed.HasValue && !action.IsForRed.Value)
                            {
                                bluePoints += action.Points;
                            }
                        }
                    }

                    if ((redPoints < bluePoints && match.WrestlerInRed == result.Wrestler && match.IsRedWon.HasValue && match.IsRedWon.Value)
                        || (redPoints > bluePoints && match.WrestlerInBlue == result.Wrestler && match.IsRedWon.HasValue && !match.IsRedWon.Value))
                    {
                        response.Add(new WrestlerAchievement
                        {
                            Title = AchievementTitle,
                            Wrestler = result.Wrestler,
                            AchievementType = AchievementType,
                            AchievementValue = "Схватка #" + match.MatchNumber,
                            AchievementDefinition = AchievementDefinition
                        });
                    }
                }                
            }

            return response;
        }
    }
}
