using System.Collections.Generic;

namespace Wrestling.Entities.Bracket.Seeding
{
    // Turns the Level column into a numeric weight. Used as the weakest
    // tie-break signal by ClubCityLevelSeedingStrategy — a pair of high-level
    // wrestlers meeting in round 1 costs a bit more than a pair of unrated ones.
    //
    // The only valid values (highest-first):
    //   МСМК → МС → КМС → I → II → III → I юн → II юн → III юн
    // Empty / "б/р" → 0. Anything else is treated as no rank.
    // Adult ranks ALWAYS outrank junior ranks (even III > I юн).
    internal static class LevelNormalizer
    {
        private static readonly Dictionary<string, double> Weights = new Dictionary<string, double>
        {
            ["мсмк"] = 9,
            ["мс"] = 8,
            ["кмс"] = 7,
            ["i"] = 6,
            ["ii"] = 5,
            ["iii"] = 4,
            ["i юн"] = 3,
            ["ii юн"] = 2,
            ["iii юн"] = 1,
            ["б/р"] = 0, [""] = 0,
        };

        public static double Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;
            var t = raw.Trim().ToLowerInvariant();
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            return Weights.TryGetValue(t, out var w) ? w : 0;
        }
    }
}
