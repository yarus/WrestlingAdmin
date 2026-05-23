namespace Wrestling.Entities.Bracket.Seeding
{
    // Turns the Level enum into a numeric weight. Used as the weakest
    // tie-break signal by ClubCityLevelSeedingStrategy — a pair of high-level
    // wrestlers meeting in round 1 costs a bit more than a pair of unrated ones.
    //
    // Adult ranks ALWAYS outrank junior ranks (even Adult3 > Junior1).
    internal static class LevelNormalizer
    {
        public static double Normalize(WrestlerLevelEnum level)
        {
            switch (level)
            {
                case WrestlerLevelEnum.MSMK:    return 9;
                case WrestlerLevelEnum.MS:      return 8;
                case WrestlerLevelEnum.KMS:     return 7;
                case WrestlerLevelEnum.Adult1:  return 6;
                case WrestlerLevelEnum.Adult2:  return 5;
                case WrestlerLevelEnum.Adult3:  return 4;
                case WrestlerLevelEnum.Junior1: return 3;
                case WrestlerLevelEnum.Junior2: return 2;
                case WrestlerLevelEnum.Junior3: return 1;
                case WrestlerLevelEnum.None:
                default:                        return 0;
            }
        }
    }
}
