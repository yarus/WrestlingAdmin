namespace Wrestling.Entities
{
    public class WrestlerAchievement
    {
        public Wrestler Wrestler { get; set; }
        public string Title { get; set; }
        public string AchievementType { get; set; }
        public string AchievementValue { get; set; }
    }
}