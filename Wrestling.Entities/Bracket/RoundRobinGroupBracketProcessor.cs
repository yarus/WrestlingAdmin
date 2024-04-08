using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Bracket
{
    public class RoundRobinGroupBracketProcessor : GroupBracketProcessorBase
    {
        public override string Title => "Круговая";
        public override string Code => BracketTypeEnum.RoundRobin.ToString();
        public override int? AthletsMaxCount => 5;
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
                (m.WrestlerInBlue == first && m.WrestlerInRed == second)
                || (m.WrestlerInRed == first && m.WrestlerInBlue == second));

            if (pairMatch != null)
            {
                return pairMatch.IsRedWon.HasValue && pairMatch.IsRedWon.Value
                    ? (pairMatch.WrestlerInRed == first ? first : second)
                    : (pairMatch.WrestlerInBlue == first ? first : second);
            }

            return null;
        }

        protected override void CalculateResults()
        {
            if (Group.Bracket == null || !Group.IsBracketCompleted) return;

            // 1. First order by wins count
            // 2. Check pair result if wins count equals
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
                var sameWins = orderedStats.Where(x => x.Wins == stat.Wins && x.Wrestler.ID != stat.Wrestler.ID && !x.Wrestler.FinalPlace.HasValue).ToList();
                if (sameWins.Count == 0)
                {
                    stat.Wrestler.FinalPlace = finalPlace;
                    finalPlace++;
                    continue;
                }
                
                // If only 1 wrestler with same wins count - check pair result
                if (sameWins.Count == 1)
                {
                    var winner = GetWinnerFromPair(stat.Wrestler, sameWins[0].Wrestler, Group.Bracket);
                    if (winner == null) continue;
                    
                    stat.Wrestler.FinalPlace = winner.ID == stat.Wrestler.ID ? finalPlace : finalPlace + 1;
                    sameWins[0].Wrestler.FinalPlace = winner.ID == sameWins[0].Wrestler.ID ? finalPlace : finalPlace + 1;

                    finalPlace++;
                    continue;
                }
                
                // If more than 1 wrestlers with same result - use the current order
                stat.Wrestler.FinalPlace = finalPlace;
                finalPlace++;
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
                    LastName = "Bye"
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
                    RoundName = "Раунд " + (day + 1)
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
                    (p.WrestlerInRed != null && p.WrestlerInRed.LastName == "Bye")
                    || (p.WrestlerInBlue != null && p.WrestlerInBlue.LastName == "Bye"));

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
