using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Bracket
{
    public class RoundRobinGroupBracketProcessor : GroupBracketProcessorBase
    {
        private static string FakeRound = "Bye";
        public override string Title => "Круговая";
        public override string Code => BracketTypeEnum.RoundRobin.ToString();
        public override int? AthletesMaxCount => 5;
        protected override void GenerateMainRounds()
        {
            GenerateGroupBracket();
            RemoveByeMatches();
        }

        protected override void GenerateAdditionalRounds()
        {
        }

        private Wrestler GetWinnerFromPair(Wrestler first, Wrestler second, GroupBracket bracket)
        {
            var matches = bracket.Rounds.SelectMany(p => p.RoundMatches).Where(x => x.Status == MatchStatusEnum.Completed).ToList();

            if (matches.Count == 0) return null;
            
            var pairMatch = matches.FirstOrDefault(m =>
                (m.WrestlerInBlue.SameAs(first) && m.WrestlerInRed.SameAs(second))
                || (m.WrestlerInRed.SameAs(first) && m.WrestlerInBlue.SameAs(second)));

            if (pairMatch != null)
            {
                return pairMatch.IsRedWon.HasValue && pairMatch.IsRedWon.Value
                    ? (pairMatch.WrestlerInRed.SameAs(first) ? first : second)
                    : (pairMatch.WrestlerInBlue.SameAs(first) ? first : second);
            }

            return null;
        }

        protected override void CalculateResults()
        {
            if (Group.Bracket == null || !Group.IsBracketCompleted) return;

            // UWW round-robin tie-breakers, in priority order:
            //   1. Classification points (already proxied by Wins as primary, then CP)
            //   2. Wins by Tushe (fall)
            //   3. Wins by Domination / DominationWithPoints (technical superiority)
            //   4. Most technical points scored
            //   5. Fewest technical points conceded
            //   6. Head-to-head (only when all of the above are equal)
            //   7. Seed number (last resort, app-specific)
            //
            // Bug fixed 2026-04-29: previously head-to-head was checked
            // whenever exactly two wrestlers shared the same Wins count,
            // overriding higher-priority tiebreakers. In a 3-way tie on
            // Wins, after the leader took 1st, the remaining two were
            // forced through head-to-head — flipping the order even when
            // their classification points clearly differed (real case:
            // 2012-2013 55kg group, Surkhaev 5 CP > Goryachev 3 CP, but
            // Goryachev advanced because he beat Surkhaev head-to-head).
            var orderedStats = GetStats()
                .OrderByDescending(x => x.Wins)
                .ThenByDescending(x => x.OverallTournamentClassificationPoints)
                .ThenByDescending(x => x.WinsByTushe)
                .ThenByDescending(x => x.WinsByDomination)
                .ThenByDescending(x => x.WinsByDominationWithPoints)
                .ThenByDescending(x => x.AllGainedPoints)
                .ThenBy(x => x.AllLostPoints)
                .ThenBy(x => x.Wrestler.SeedNumber)
                .ToList();

            var finalPlace = 1;
            foreach (var stat in orderedStats)
            {
                if (stat.Wrestler.FinalPlace.HasValue || stat.Wrestler.IsPlaceless)
                {
                    // Disqualified wrestlers (mutual DSQ for brutality) keep
                    // FinalPlace=null per UWW «без места».
                    continue;
                }

                var fullyTied = orderedStats.Where(x =>
                    x.Wrestler.ID != stat.Wrestler.ID
                    && !x.Wrestler.FinalPlace.HasValue
                    && !x.Wrestler.IsPlaceless
                    && x.Wins == stat.Wins
                    && x.OverallTournamentClassificationPoints == stat.OverallTournamentClassificationPoints
                    && x.WinsByTushe == stat.WinsByTushe
                    && x.WinsByDomination == stat.WinsByDomination
                    && x.WinsByDominationWithPoints == stat.WinsByDominationWithPoints
                    && x.AllGainedPoints == stat.AllGainedPoints
                    && x.AllLostPoints == stat.AllLostPoints).ToList();

                if (fullyTied.Count == 0)
                {
                    stat.Wrestler.FinalPlace = finalPlace;
                    finalPlace++;
                    continue;
                }

                // Two wrestlers tied on every measurable criterion — use head-to-head.
                if (fullyTied.Count == 1)
                {
                    var winner = GetWinnerFromPair(stat.Wrestler, fullyTied[0].Wrestler, Group.Bracket);
                    if (winner == null)
                    {
                        // No pair match (shouldn't happen in a complete round-robin) —
                        // fall back to SeedNumber order already established by the OrderBy chain.
                        stat.Wrestler.FinalPlace = finalPlace;
                        finalPlace++;
                        continue;
                    }

                    stat.Wrestler.FinalPlace = winner.ID == stat.Wrestler.ID ? finalPlace : finalPlace + 1;
                    fullyTied[0].Wrestler.FinalPlace = winner.ID == fullyTied[0].Wrestler.ID ? finalPlace : finalPlace + 1;

                    finalPlace += 2;
                    continue;
                }

                // 3+ wrestlers tied on everything — head-to-head between specific
                // pairs may be circular; rank by number of head-to-head wins among
                // the tied group, fall back to SeedNumber for residual ties.
                var ranked = fullyTied
                    .Concat(new[] { stat })
                    .Select(r => new
                    {
                        Stat = r,
                        HthWins = fullyTied
                            .Concat(new[] { stat })
                            .Count(other => other.Wrestler.ID != r.Wrestler.ID
                                            && GetWinnerFromPair(r.Wrestler, other.Wrestler, Group.Bracket)?.ID == r.Wrestler.ID)
                    })
                    .OrderByDescending(x => x.HthWins)
                    .ThenBy(x => x.Stat.Wrestler.SeedNumber)
                    .ToList();

                foreach (var item in ranked)
                {
                    item.Stat.Wrestler.FinalPlace = finalPlace;
                    finalPlace++;
                }
            }
        }

        private void GenerateGroupBracket()
        {
            var shuffledList = new List<Wrestler>(GetGroupWrestlers());

            ShuffleWrestlers(shuffledList);

            if (shuffledList.Count % 2 != 0)
            {
                shuffledList.Add(new Wrestler
                {
                    LastName = FakeRound
                });
            }

            int numDays = shuffledList.Count - 1;
            int halfsize = shuffledList.Count / 2;

            List<Wrestler> teams = new List<Wrestler>();

            teams.AddRange(shuffledList);
            teams.RemoveAt(0);

            int teamSize = teams.Count;

            for (int day = 0; day < numDays; day++)
            {
                var round = new GroupRound
                {
                    RoundNumber = day + 1,
                    RoundType = GroupRoundTypeEnum.Main,
                    RoundName = "Круг " + (day + 1)
                };

                int teamIdx = day % teamSize;

                var baseMatch = GenerateGroupMatch(round.RoundNumber, round.RoundName, teams[teamIdx], shuffledList[0], 1, false);

                round.RoundMatches.Add(baseMatch);

                for (int idx = 0; idx < halfsize; idx++)
                {
                    int firstTeam = (day + idx) % teamSize;
                    int secondTeam = (day + teamSize - idx) % teamSize;

                    if (firstTeam != secondTeam)
                    {
                        var match = GenerateGroupMatch(round.RoundNumber, round.RoundName, teams[firstTeam], teams[secondTeam], idx + 1, false);
                        round.RoundMatches.Add(match);
                    }
                }

                Group.Bracket.Rounds.Add(round);
            }
        }

        private void RemoveByeMatches()
        {
            foreach (var round in Group.Bracket.Rounds)
            {
                var byeMatch = round.RoundMatches.FirstOrDefault(p =>
                    (p.WrestlerInRed != null && p.WrestlerInRed.LastName == FakeRound)
                    || (p.WrestlerInBlue != null && p.WrestlerInBlue.LastName == FakeRound));

                if (byeMatch != null) round.RoundMatches.Remove(byeMatch);
            }

            foreach (var round in Group.Bracket.Rounds)
            {
                for (int i = 0; i < round.RoundMatches.Count; i++)
                {
                    round.RoundMatches[i].BracketNumber = i + 1;
                }
            }
        }
    }
}
