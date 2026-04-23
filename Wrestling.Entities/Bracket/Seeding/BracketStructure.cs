using System;

namespace Wrestling.Entities.Bracket.Seeding
{
    // Pure helpers that mirror the math used by OlympicGroupBracketProcessor.
    // Seeding decisions need to know which first-round slots share a match,
    // which slots meet in later rounds, etc.
    public static class BracketStructure
    {
        public static int NextPow2(int n)
        {
            int p = 1;
            while (p < n) p <<= 1;
            return p;
        }

        public static int TotalRounds(int n) => (int)Math.Ceiling(Math.Log(NextPow2(Math.Max(2, n)), 2));

        public static (int fullMatches, int freeMatches) FirstRoundShape(int n)
        {
            int totalCells = NextPow2(n);
            int fullMatches = (2 * n - totalCells) / 2;
            int freeMatches = n - 2 * fullMatches;
            return (fullMatches, freeMatches);
        }

        // Slot (1..N) → index of the first-round match containing this slot.
        // Free-win slots occupy matches 1..freeMatches each on their own; then
        // adjacent slot pairs share a match.
        public static int SlotToFirstRoundMatch(int slot, int freeMatches)
        {
            if (slot <= freeMatches) return slot;
            return freeMatches + ((slot - freeMatches + 1) / 2);
        }

        // First round in which slots a and b can meet. 1 = same pair in round 1,
        // 2 = in round 2 (free-win vs qualifier, or adjacent round-1 winners),
        // ..., TotalRounds = final. Returns 0 if a == b.
        public static int DepthOfEncounter(int a, int b, int n)
        {
            if (a == b) return 0;
            var (_, freeMatches) = FirstRoundShape(n);
            int ma = SlotToFirstRoundMatch(a, freeMatches);
            int mb = SlotToFirstRoundMatch(b, freeMatches);
            int round = 1;
            int safety = TotalRounds(n) + 2;
            while (ma != mb && round < safety)
            {
                ma = (ma + 1) / 2;
                mb = (mb + 1) / 2;
                round++;
            }
            return round;
        }

        // Exponential decay: earlier-round conflicts cost exponentially more
        // than later-round ones. A round-1 meeting is 2× worse than round-2,
        // 4× worse than round-3, etc. Drives the cost function for the local
        // search in ClubCityLevelSeedingStrategy.
        public static double DepthWeight(int depth, int n)
        {
            if (depth <= 0) return 0;
            int rounds = TotalRounds(n);
            return Math.Pow(2, Math.Max(0, rounds - depth));
        }
    }
}
