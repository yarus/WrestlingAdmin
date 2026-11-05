using System;
using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Bracket
{
    // Merges fixed category queues. Only their interleaving changes; bracket
    // cells and the relative order of every category remain untouched.
    public static class RestAwareMatchOrder
    {
        public static IReadOnlyList<WrestlingMatch> Optimize(
            IReadOnlyList<WrestlingMatch> original, IEnumerable<AgeWeightGroup> groups)
        {
            var profiles = new Dictionary<WrestlingMatch, Profile>();
            foreach (var group in groups) BuildProfiles(group, profiles);
            var priorities = original.Select((match, index) => (match, index))
                .ToDictionary(x => x.match, x => x.index);
            var queues = original.GroupBy(m => m.GroupID)
                .Select(g => new Queue<WrestlingMatch>(g)).ToList();
            var result = new List<WrestlingMatch>();
            var recent = new List<(WrestlingMatch Match, int CompletedAt)>();
            int realBouts = 0;
            while (queues.Any(q => q.Count > 0))
            {
                var candidates = queues.Where(q => q.Count > 0)
                    .OrderBy(q => priorities[q.Peek()]).ToList();
                int Rest(Queue<WrestlingMatch> queue)
                {
                    var match = queue.Peek();
                    if (!profiles[match].CanBeReal) return 3;
                    for (int i = recent.Count - 1; i >= 0; i--)
                    {
                        var previous = recent[i].Match;
                        if (previous.GroupID == match.GroupID && CanShareAthlete(profiles[previous], profiles[match]))
                            return realBouts - recent[i].CompletedAt;
                    }
                    return 3;
                }

                // Keep the existing priority whenever the rest requirement is
                // satisfied. If all queues need rest, take the best available
                // interval rather than inventing idle bouts.
                var selected = candidates.FirstOrDefault(q => Rest(q) >= 3)
                    ?? candidates.OrderByDescending(Rest).First();
                var next = selected.Dequeue();
                result.Add(next);
                if (profiles[next].CountsAsRest) realBouts++;
                if (profiles[next].CanBeReal)
                {
                    recent.Add((next, realBouts));
                }
                recent.RemoveAll(previous => realBouts - previous.CompletedAt >= 3);
            }
            // Local choices must not make the overall schedule worse. Keep
            // the original on ties to avoid unnecessary renumbering.
            return RestPenalty(result, profiles) < RestPenalty(original, profiles) ? result : original;
        }

        private static int RestPenalty(IReadOnlyList<WrestlingMatch> matches, Dictionary<WrestlingMatch, Profile> profiles)
        {
            int penalty = 0, realBouts = 0;
            var recent = new List<(WrestlingMatch Match, int CompletedAt)>();
            foreach (var match in matches)
            {
                var profile = profiles[match];
                if (profile.CanBeReal)
                {
                    for (int i = recent.Count - 1; i >= 0; i--)
                    {
                        var previous = recent[i];
                        if (previous.Match.GroupID != match.GroupID || !CanShareAthlete(profiles[previous.Match], profile)) continue;
                        int deficit = 3 - (realBouts - previous.CompletedAt);
                        penalty += deficit * deficit;
                        break;
                    }
                }
                if (profile.CountsAsRest) realBouts++;
                if (profile.CanBeReal) recent.Add((match, realBouts));
                recent.RemoveAll(previous => realBouts - previous.CompletedAt >= 3);
            }
            return penalty;
        }

        private static bool CanShareAthlete(Profile first, Profile second)
        {
            // A finalist cannot also wrestle for bronze/consolation; potential
            // participant sets alone would incorrectly flag these as overlaps.
            if ((first.IsFinal && second.IsConsolation) || (second.IsFinal && first.IsConsolation)) return false;
            if (first.ExclusiveRound != null && first.ExclusiveRound == second.ExclusiveRound) return false;
            return first.Athletes.Overlaps(second.Athletes);
        }

        private sealed class Profile
        {
            public HashSet<Guid> Athletes = new HashSet<Guid>();
            public bool CanBeReal;
            public bool CountsAsRest;
            public bool IsFinal;
            public bool IsConsolation;
            public GroupRound ExclusiveRound;
            public int MinWins;
            public int MaxWins;
        }

        private static HashSet<Guid> Athletes(WrestlingMatch match) => new HashSet<Guid>(
            new[] { match.WrestlerInRed, match.WrestlerInBlue }
                .Where(w => w != null).Select(w => w.ID));

        private static void BuildProfiles(AgeWeightGroup group, Dictionary<WrestlingMatch, Profile> profiles)
        {
            var rounds = group.Bracket.Rounds;
            var main = rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).OrderBy(r => r.RoundNumber).ToList();
            var additional = rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).OrderBy(r => r.RoundNumber).ToList();
            var code = group.Bracket.BracketTypeCode;
            bool pools = code == BracketTypeEnum.SubGroupsIntoOlympic.ToString();
            bool circle = code == BracketTypeEnum.RoundRobin.ToString();
            bool consolation = code == BracketTypeEnum.OlympicConsilationFinalists.ToString();

            foreach (var round in main)
            {
                foreach (var match in round.RoundMatches)
                {
                    var profile = new Profile { Athletes = Athletes(match) };
                    if (circle || pools)
                    {
                        profile.CanBeReal = match.WrestlerInRed != null && match.WrestlerInBlue != null;
                        profile.CountsAsRest = profile.CanBeReal;
                    }
                    else
                    {
                        var sources = main.SelectMany(r => r.RoundMatches)
                            .Where(m => m != match && m.NextMatchBracketFullNumber == match.BracketFullNumber
                                && profiles.ContainsKey(m)).ToList();
                        if (sources.Count > 0)
                        {
                            profile.Athletes = new HashSet<Guid>(sources.SelectMany(m => profiles[m].Athletes));
                            profile.CanBeReal = sources.Count(m => profiles[m].Athletes.Count > 0) == 2;
                            profile.MinWins = sources.Min(m => profiles[m].MinWins) + (profile.CanBeReal ? 1 : 0);
                            profile.MaxWins = sources.Max(m => profiles[m].MaxWins) + (profile.CanBeReal ? 1 : 0);
                        }
                        else
                        {
                            profile.CanBeReal = match.WrestlerInRed != null && match.WrestlerInBlue != null;
                            profile.MinWins = profile.MaxWins = profile.CanBeReal ? 1 : 0;
                        }
                        profile.CountsAsRest = profile.CanBeReal;
                        profile.IsFinal = round == main.Last();
                        profile.ExclusiveRound = round;
                    }
                    profiles[match] = profile;
                }
            }

            foreach (var round in additional)
            {
                foreach (var match in round.RoundMatches)
                {
                    var profile = new Profile { Athletes = Athletes(match), CanBeReal = true, CountsAsRest = true, IsConsolation = true };
                    if (consolation && main.Count >= 2)
                    {
                        var semifinal = main[main.Count - 2].RoundMatches
                            .FirstOrDefault(m => m.BracketNumber == match.BracketNumber);
                        if (semifinal != null)
                        {
                            profile.Athletes = new HashSet<Guid>(profiles[semifinal].Athletes);
                            // The first consolation cell consumes the first two
                            // losers on a finalist's path. A first-round bye can
                            // make it an automatic pass; never count that cell
                            // as guaranteed rest for the other categories.
                            int index = additional.IndexOf(round);
                            int requiredLosers = additional.Count - index + 1;
                            profile.CanBeReal = profiles[semifinal].MaxWins >= requiredLosers;
                            profile.CountsAsRest = profiles[semifinal].MinWins >= requiredLosers;
                        }
                    }
                    else
                    {
                        profile.Athletes.UnionWith(main.SelectMany(r => r.RoundMatches)
                            .SelectMany(m => profiles[m].Athletes));
                        if (pools)
                        {
                            int index = additional.IndexOf(round);
                            profile.IsFinal = index == 1;
                            profile.IsConsolation = index == 2;
                            if (index == 0) profile.ExclusiveRound = round;
                        }
                    }
                    profiles[match] = profile;
                }
            }
        }
    }
}
