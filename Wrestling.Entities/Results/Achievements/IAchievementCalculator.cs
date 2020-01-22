using System.Collections.Generic;

namespace Wrestling.Entities.Results.Achievements
{
    public interface IAchievementCalculator
    {
        string AchievementTitle { get; }
        string AchievementType { get; }
        string AchievementDefinition { get; }

        List<WrestlerAchievement> CalculateAchievement(Tournament tournament, List<TournamentResult> results);
    }
}