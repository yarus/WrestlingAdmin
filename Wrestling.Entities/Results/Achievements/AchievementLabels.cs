using Wrestling.Entities.Localization;

namespace Wrestling.Entities.Results.Achievements
{
    // Centralized lookup from AchievementType → localized display title +
    // definition. Calculators still expose their own Title/Definition
    // properties for direct use, but consumers that aggregate results (the
    // achievements page, the print view) read through this helper so the
    // labels stay fresh on language switch — the previous design baked the
    // labels into WrestlerAchievement instances at calculation time, so they
    // didn't update until a tournament was re-opened.
    public static class AchievementLabels
    {
        public static string GetTitle(string achievementType)
        {
            switch (achievementType)
            {
                case "FastestAction":       return EntityLocalization.T("Achievement_FastestAction_Title", "Метеор");
                case "FastestWin":          return EntityLocalization.T("Achievement_FastestWin_Title", "Молния");
                case "MostAmplitudeActions":return EntityLocalization.T("Achievement_MostAmplitude_Title", "Космодром");
                case "MostDominationWins":  return EntityLocalization.T("Achievement_MostDomination_Title", "Доминатор");
                case "MostPointsCount":     return EntityLocalization.T("Achievement_MostPoints_Title", "Машина Борьбы");
                case "MostTusheWinsCount":  return EntityLocalization.T("Achievement_MostTushe_Title", "Асфальтоукладчик");
                case "NeverGiveUp":         return EntityLocalization.T("Achievement_NeverGiveUp_Title", "Никогда не сдаваться");
                default: return achievementType ?? string.Empty;
            }
        }

        public static string GetDefinition(string achievementType)
        {
            switch (achievementType)
            {
                case "FastestAction":       return EntityLocalization.T("Achievement_FastestAction_Definition", "Борец, быстрее всех выполнивший результативное действие");
                case "FastestWin":          return EntityLocalization.T("Achievement_FastestWin_Definition", "Борец, выигравший схватку быстрее всех");
                case "MostAmplitudeActions":return EntityLocalization.T("Achievement_MostAmplitude_Definition", "Борец, выполнивший больше всех 4-бальных бросков за турнир");
                case "MostDominationWins":  return EntityLocalization.T("Achievement_MostDomination_Definition", "Борец, выигравший больше всего схваток по техническому превосходству");
                case "MostPointsCount":     return EntityLocalization.T("Achievement_MostPoints_Definition", "Борец, набравший больше всех баллов за турнир");
                case "MostTusheWinsCount":  return EntityLocalization.T("Achievement_MostTushe_Definition", "Борец, выигравший больше всего схваток по туше");
                case "NeverGiveUp":         return EntityLocalization.T("Achievement_NeverGiveUp_Definition", "Борец, набравший победные баллы за последние 10 секунд схватки");
                default: return string.Empty;
            }
        }
    }
}
