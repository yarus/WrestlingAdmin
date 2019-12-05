using System.Collections.Generic;

namespace Wrestling.Entities.Results.Achievements
{
    public interface IAchievementCalculator
    {
        string AchievementTitle { get; }
        string AchievementType { get; }

        WrestlerAchievement CalculateAchievement(List<TournamentResult> results);
    }
}