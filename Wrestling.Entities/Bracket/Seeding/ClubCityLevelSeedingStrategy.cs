using System;
using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Bracket.Seeding
{
    // Smart draw: spread wrestlers of the same club / city / high Level across
    // the bracket so they meet as late as possible. Ported from the
    // .claude/skills/draw-seeding JS implementation after it was validated on
    // a real tournament file. Does NOT consider external rating — for that,
    // keep using the skill with a --rating CSV.
    //
    // Priority order (strongest first):
    //   1. Club  (Wrestler.TeamID)  — same-club pairs cost the most
    //   2. City  (Wrestler.TeamCity)
    //   3. Level (Wrestler.Level, normalized to a weight)
    //
    // For each strategy call:
    //   - Wrestlers entering with IsSeedFixed=true AND a valid SeedNumber are
    //     treated as locks (their slot is not changed).
    //   - All other wrestlers get re-placed. IsSeedFixed is left as-is — it's
    //     the caller's job to lock after an explicit draw (DrawViewModel does
    //     this in RegenerateBrackets/GenerateBracket). This lets InitData run
    //     Seed on tab entry for display purposes without silently locking
    //     everyone.
    public class ClubCityLevelSeedingStrategy : ISeedingStrategy
    {
        private const double WeightClub = 10000;
        private const double WeightCity = 500;
        private const double WeightLevel = 5;

        // Randomness source. Default: unseeded — production callers get a fresh
        // shuffle on each Seed invocation, so clicking "Пересоздать все сетки"
        // after unfixing everyone produces a genuinely new distribution whenever
        // the group has multiple wrestlers at the same Level (very common).
        // Tests pass a fixed seed via the overload for reproducibility.
        private readonly Random _rng;

        public ClubCityLevelSeedingStrategy() : this(null) { }

        public ClubCityLevelSeedingStrategy(int? seed)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public void Seed(AgeWeightGroup group)
        {
            if (group?.Wrestlers == null) return;
            // Pre-shuffle the input list so the downstream OrderByDescending(Level)
            // (a stable sort in LINQ-to-Objects) preserves a random order inside
            // each equal-Level bucket. Locked wrestlers are still pinned by
            // BuildLocks below, so fairness of locks is unaffected.
            var wrestlers = ShuffleCopy(group.Wrestlers);
            int n = wrestlers.Count;
            if (n < 2)
            {
                // Edge cases: 0 or 1 wrestler — just normalize SeedNumber.
                for (int i = 0; i < wrestlers.Count; i++)
                {
                    wrestlers[i].SeedNumber = i + 1;
                }
                group.Wrestlers = wrestlers;
                return;
            }

            var bracketCode = group.Bracket?.BracketTypeCode ?? DefaultBracketCode(n);
            var locks = BuildLocks(wrestlers, n);

            Wrestler[] slots;
            if (bracketCode == BracketTypeEnum.RoundRobin.ToString())
            {
                slots = SeedRoundRobin(wrestlers, n, locks);
            }
            else if (bracketCode == BracketTypeEnum.SubGroupsIntoOlympic.ToString())
            {
                slots = SeedSubGroups(wrestlers, n, locks);
            }
            else
            {
                slots = SeedOlympic(wrestlers, n, locks);
            }

            var reordered = new List<Wrestler>(n);
            for (int i = 0; i < n; i++)
            {
                var wr = slots[i + 1];
                wr.SeedNumber = i + 1;
                reordered.Add(wr);
            }
            group.Wrestlers = reordered;
        }

        private static string DefaultBracketCode(int n)
        {
            if (n <= 5) return BracketTypeEnum.RoundRobin.ToString();
            if (n <= 7) return BracketTypeEnum.SubGroupsIntoOlympic.ToString();
            return BracketTypeEnum.OlympicConsilationFinalists.ToString();
        }

        private static Dictionary<int, Wrestler> BuildLocks(List<Wrestler> wrestlers, int n)
        {
            var result = new Dictionary<int, Wrestler>();
            foreach (var w in wrestlers)
            {
                if (!w.IsSeedFixed || !w.SeedNumber.HasValue) continue;
                int s = w.SeedNumber.Value;
                if (s < 1 || s > n) continue;
                if (result.ContainsKey(s)) continue; // duplicate lock — skip the second one
                result[s] = w;
            }
            return result;
        }

        // ---------- RoundRobin ----------
        // Pairing inside round-robin is re-shuffled by the processor itself, so
        // SeedNumber only affects tie-break order. Strongest (highest Level)
        // gets seed 1; everyone else ordered by Level desc, then name.
        private static Wrestler[] SeedRoundRobin(List<Wrestler> wrestlers, int n, Dictionary<int, Wrestler> locks)
        {
            var slots = new Wrestler[n + 1];
            var lockedIds = new HashSet<Guid>();
            foreach (var kv in locks) { slots[kv.Key] = kv.Value; lockedIds.Add(kv.Value.ID); }

            var rest = wrestlers
                .Where(w => !lockedIds.Contains(w.ID))
                .OrderByDescending(w => LevelNormalizer.Normalize(w.Level))
                .ToList();

            int k = 0;
            for (int s = 1; s <= n; s++)
            {
                if (slots[s] != null) continue;
                slots[s] = rest[k++];
            }
            return slots;
        }

        // ---------- SubGroupsIntoOlympic ----------
        // Subgroup A = seeds 1..kA, Subgroup B = seeds (N-kB+1)..N, middle =
        // everything in between (only non-empty for N=8). Inside each subgroup
        // pairings are round-robin, so we minimise intra-subgroup conflicts by
        // enumerating every feasible partition and picking the one with lowest
        // within-subgroup cost. For N ≤ 8 this is at most C(8,3)*C(5,3) = 560
        // combinations.
        private static Wrestler[] SeedSubGroups(List<Wrestler> wrestlers, int n, Dictionary<int, Wrestler> locks)
        {
            int kA = (n == 7) ? 4 : 3;
            int kB = 3;
            var slotsA = Enumerable.Range(1, kA).ToList();
            var slotsB = Enumerable.Range(n - kB + 1, kB).ToList();
            var slotsM = Enumerable.Range(kA + 1, Math.Max(0, n - kA - kB)).ToList();

            var slots = new Wrestler[n + 1];
            var lockedInA = new List<Wrestler>();
            var lockedInB = new List<Wrestler>();
            var lockedInM = new List<Wrestler>();
            var freeSlotsA = new List<int>(slotsA);
            var freeSlotsB = new List<int>(slotsB);
            var freeSlotsM = new List<int>(slotsM);
            var lockedIds = new HashSet<Guid>();

            foreach (var kv in locks)
            {
                int s = kv.Key;
                var w = kv.Value;
                slots[s] = w;
                lockedIds.Add(w.ID);
                if (slotsA.Contains(s)) { lockedInA.Add(w); freeSlotsA.Remove(s); }
                else if (slotsB.Contains(s)) { lockedInB.Add(w); freeSlotsB.Remove(s); }
                else { lockedInM.Add(w); freeSlotsM.Remove(s); }
            }
            var free = wrestlers.Where(w => !lockedIds.Contains(w.ID)).ToList();
            if (freeSlotsA.Count + freeSlotsB.Count + freeSlotsM.Count != free.Count)
            {
                // Defensive: locks inconsistent with slot counts. Fall back to
                // RoundRobin seeding so we still produce a valid 1..N mapping.
                return SeedRoundRobin(wrestlers, n, locks);
            }

            var indices = Enumerable.Range(0, free.Count).ToList();
            (List<int> aPick, List<int> bPick, List<int> mPick, double cost)? best = null;

            foreach (var aPick in Combinations(indices, freeSlotsA.Count))
            {
                var afterA = indices.Except(aPick).ToList();
                foreach (var bPick in Combinations(afterA, freeSlotsB.Count))
                {
                    var mPick = afterA.Except(bPick).ToList();
                    var aMembers = lockedInA.Concat(aPick.Select(i => free[i])).ToList();
                    var bMembers = lockedInB.Concat(bPick.Select(i => free[i])).ToList();
                    double cost = SubgroupCost(aMembers) + SubgroupCost(bMembers);
                    if (best == null || cost < best.Value.cost)
                    {
                        best = (aPick, bPick, mPick, cost);
                        if (cost == 0) break;
                    }
                }
                if (best.HasValue && best.Value.cost == 0) break;
            }

            if (best == null)
            {
                return SeedRoundRobin(wrestlers, n, locks);
            }

            // Order within each subgroup: strongest (by Level) first, so a
            // locked seed keeps its place and free seats fill up in a
            // predictable order.
            var aFree = best.Value.aPick.Select(i => free[i])
                .OrderByDescending(w => LevelNormalizer.Normalize(w.Level)).ToList();
            var bFree = best.Value.bPick.Select(i => free[i])
                .OrderByDescending(w => LevelNormalizer.Normalize(w.Level)).ToList();
            var mFree = best.Value.mPick.Select(i => free[i])
                .OrderByDescending(w => LevelNormalizer.Normalize(w.Level)).ToList();

            foreach (var s in freeSlotsA) { slots[s] = aFree[0]; aFree.RemoveAt(0); }
            foreach (var s in freeSlotsB) { slots[s] = bFree[0]; bFree.RemoveAt(0); }
            foreach (var s in freeSlotsM) { slots[s] = mFree[0]; mFree.RemoveAt(0); }
            return slots;
        }

        private static double SubgroupCost(List<Wrestler> members)
        {
            double c = 0;
            for (int i = 0; i < members.Count; i++)
                for (int j = i + 1; j < members.Count; j++)
                    c += PairConflict(members[i], members[j]);
            return c;
        }

        // ---------- Olympic / OlympicConsolationFinalists ----------
        private static Wrestler[] SeedOlympic(List<Wrestler> wrestlers, int n, Dictionary<int, Wrestler> locks)
        {
            var slots = new Wrestler[n + 1];
            var lockedSlots = new HashSet<int>();
            var lockedIds = new HashSet<Guid>();
            foreach (var kv in locks) { slots[kv.Key] = kv.Value; lockedSlots.Add(kv.Key); lockedIds.Add(kv.Value.ID); }

            var remaining = wrestlers
                .Where(w => !lockedIds.Contains(w.ID))
                .OrderByDescending(w => LevelNormalizer.Normalize(w.Level))
                .ToList();

            // Initial placement: strongest wrestlers into the "seeded" slots
            // (slots whose round-1 opponent is either a free-winner or not
            // themselves seeded), in an order that maximises mutual distance
            // across the tree.
            var favOrder = BitReverseSeededOrder(n);
            int idx = 0;
            foreach (var slot in favOrder)
            {
                if (lockedSlots.Contains(slot)) continue;
                if (idx >= remaining.Count) break;
                if (slots[slot] != null) continue;
                slots[slot] = remaining[idx++];
            }

            // Fill the rest, picking for each open slot the wrestler that
            // minimises conflict with already-placed neighbours.
            var leftover = remaining.Skip(idx).ToList();
            var openSlots = new List<int>();
            for (int s = 1; s <= n; s++) if (slots[s] == null) openSlots.Add(s);

            foreach (var s in openSlots)
            {
                if (leftover.Count == 0) break;
                int bestI = 0; double bestCost = double.PositiveInfinity;
                for (int i = 0; i < leftover.Count; i++)
                {
                    slots[s] = leftover[i];
                    double local = 0;
                    for (int t = 1; t <= n; t++)
                    {
                        if (t == s || slots[t] == null) continue;
                        int d = BracketStructure.DepthOfEncounter(s, t, n);
                        if (d == 0) continue;
                        double pair = PairConflict(slots[s], slots[t]);
                        if (pair > 0) local += pair * BracketStructure.DepthWeight(d, n);
                    }
                    if (local < bestCost) { bestCost = local; bestI = i; }
                    slots[s] = null;
                }
                slots[s] = leftover[bestI];
                leftover.RemoveAt(bestI);
            }

            // Local search — 2-opt + 3-opt. 3-opt escapes plateaus where every
            // pair-swap looks neutral but a rotation of three slots reduces
            // cost (three same-city wrestlers are a classic example).
            double currentCost = PlacementCost(slots, n);
            for (int iter = 0; iter < 500; iter++)
            {
                bool improved = false;
                for (int a = 1; a <= n; a++)
                {
                    if (lockedSlots.Contains(a)) continue;
                    for (int b = a + 1; b <= n; b++)
                    {
                        if (lockedSlots.Contains(b)) continue;
                        (slots[a], slots[b]) = (slots[b], slots[a]);
                        double newCost = PlacementCost(slots, n);
                        if (newCost < currentCost - 1e-9)
                        {
                            currentCost = newCost;
                            improved = true;
                        }
                        else
                        {
                            (slots[a], slots[b]) = (slots[b], slots[a]);
                        }
                    }
                }
                if (improved) continue;
                // 3-cycle
                for (int a = 1; a <= n && !improved; a++)
                {
                    if (lockedSlots.Contains(a)) continue;
                    for (int b = a + 1; b <= n && !improved; b++)
                    {
                        if (lockedSlots.Contains(b)) continue;
                        for (int c = b + 1; c <= n && !improved; c++)
                        {
                            if (lockedSlots.Contains(c)) continue;
                            var wa = slots[a]; var wb = slots[b]; var wc = slots[c];
                            slots[a] = wb; slots[b] = wc; slots[c] = wa;
                            double newCost = PlacementCost(slots, n);
                            if (newCost < currentCost - 1e-9) { currentCost = newCost; improved = true; continue; }
                            slots[a] = wc; slots[b] = wa; slots[c] = wb;
                            newCost = PlacementCost(slots, n);
                            if (newCost < currentCost - 1e-9) { currentCost = newCost; improved = true; continue; }
                            slots[a] = wa; slots[b] = wb; slots[c] = wc;
                        }
                    }
                }
                if (!improved) break;
            }
            return slots;
        }

        // Greedy "seeded slots" traversal: pick slot 1, then the seeded slot
        // farthest from everything already chosen (by min DepthOfEncounter),
        // and so on. Ensures top wrestlers end up in slots that meet only in
        // late rounds.
        private static List<int> BitReverseSeededOrder(int n)
        {
            var seeded = SeededSlots(n);
            if (seeded.Count == 0) return new List<int>();
            var ordered = new List<int> { seeded[0] };
            var remaining = new HashSet<int>(seeded.Skip(1));
            while (remaining.Count > 0)
            {
                int best = 0, bestMin = -1;
                foreach (var s in remaining)
                {
                    int minDepth = int.MaxValue;
                    foreach (var o in ordered)
                    {
                        int d = BracketStructure.DepthOfEncounter(s, o, n);
                        if (d < minDepth) minDepth = d;
                    }
                    if (minDepth > bestMin || (minDepth == bestMin && (best == 0 || s < best)))
                    {
                        bestMin = minDepth;
                        best = s;
                    }
                }
                ordered.Add(best);
                remaining.Remove(best);
            }
            return ordered;
        }

        // Seeded slots = slots whose round-1 opponent is either a free-winner
        // (no real match) or a non-seeded slot. Favourites go here so they
        // don't fight each other in round 1.
        private static List<int> SeededSlots(int n)
        {
            var (_, freeMatches) = BracketStructure.FirstRoundShape(n);
            var result = new List<int>();
            for (int s = 1; s <= n; s++)
            {
                if (s <= freeMatches) result.Add(s);
                else if ((s - freeMatches) % 2 == 1) result.Add(s);
            }
            return result;
        }

        private static double PlacementCost(Wrestler[] slots, int n)
        {
            double cost = 0;
            for (int a = 1; a <= n; a++)
            {
                if (slots[a] == null) continue;
                for (int b = a + 1; b <= n; b++)
                {
                    if (slots[b] == null) continue;
                    int d = BracketStructure.DepthOfEncounter(a, b, n);
                    if (d == 0) continue;
                    double p = PairConflict(slots[a], slots[b]);
                    if (p > 0) cost += p * BracketStructure.DepthWeight(d, n);
                }
            }
            return cost;
        }

        private static double PairConflict(Wrestler a, Wrestler b)
        {
            double w = 0;
            bool sameClub = a.TeamID.HasValue && a.TeamID == b.TeamID;
            if (sameClub) w += WeightClub;

            var ac = (a.TeamCity ?? "").Trim().ToLowerInvariant();
            var bc = (b.TeamCity ?? "").Trim().ToLowerInvariant();
            if (!sameClub && ac.Length > 0 && ac == bc) w += WeightCity;

            double la = LevelNormalizer.Normalize(a.Level);
            double lb = LevelNormalizer.Normalize(b.Level);
            double lvl = Math.Min(la, lb);
            if (lvl >= 2) w += WeightLevel * lvl;
            return w;
        }

        // Fisher-Yates shuffle into a fresh List<Wrestler>. Leaves the caller's
        // ObservableCollection untouched.
        private List<Wrestler> ShuffleCopy(IEnumerable<Wrestler> source)
        {
            var list = source.ToList();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                if (j != i)
                {
                    (list[i], list[j]) = (list[j], list[i]);
                }
            }
            return list;
        }

        // Standard n-choose-k enumerator; yields index lists sized k drawn from
        // `pool`.
        private static IEnumerable<List<int>> Combinations(List<int> pool, int k)
        {
            if (k == 0) { yield return new List<int>(); yield break; }
            if (k > pool.Count) yield break;
            var idx = new int[k];
            for (int i = 0; i < k; i++) idx[i] = i;
            while (true)
            {
                yield return idx.Select(i => pool[i]).ToList();
                int p = k - 1;
                while (p >= 0 && idx[p] == pool.Count - k + p) p--;
                if (p < 0) break;
                idx[p]++;
                for (int j = p + 1; j < k; j++) idx[j] = idx[j - 1] + 1;
            }
        }
    }
}
