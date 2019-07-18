using System;
using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities.Results;

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

        private Wrestler GetWinnerFromPair(Wrestler first, Wrestler second, List<WrestlingMatch> matches)
        {
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
            if (Group.Bracket == null) return;

            // 1. First order by wins count
            // 2. Check pair result if wins count equals

            var matches = Group.Bracket.Rounds.SelectMany(p => p.RoundMatches).Where(x => x.Status == MatchStatusEnum.Completed).ToList();
            if (matches.Count == 0)
            {
                return;
            }

            var stats = GetStats();

            var orderedStats = stats
                .OrderByDescending(x => x.Wins)
                .ToList();

            int finalPlace = 1;
            for (int i = 0;i < orderedStats.Count;i++)
            {
                var sameWins = orderedStats.Where(x => x.Wins == orderedStats[i].Wins && x.Wrestler != orderedStats[i].Wrestler && !x.Wrestler.FinalPlace.HasValue).ToList();
                if (sameWins.Count == 0)
                {
                    orderedStats[i].Wrestler.FinalPlace = finalPlace;
                    finalPlace++;
                }
                // If only 1 wrestler with same wins count - check pair result
                else if (sameWins.Count == 1)
                {
                    var winner = GetWinnerFromPair(orderedStats[i].Wrestler, sameWins[0].Wrestler, matches);

                    orderedStats[i].Wrestler.FinalPlace = winner == orderedStats[i].Wrestler ? finalPlace : finalPlace + 1;
                    sameWins[0].Wrestler.FinalPlace = winner == sameWins[0].Wrestler ? finalPlace : finalPlace + 1;

                    finalPlace += 2;
                }
                // If more than 1 wrestlers with same result - order by stats and pick final places
                else
                {
                    var allSameStats = new List<TournamentResult>();
                    allSameStats.Add(orderedStats[i]);
                    allSameStats.AddRange(sameWins);

                    var finalOrder = allSameStats
                        .OrderByDescending(x => x.OverallTournamentRating)
                        .ToList();

                    foreach (var t in finalOrder)
                    {
                        t.Wrestler.FinalPlace = finalPlace;
                        finalPlace++;
                    }
                }
            }

            /*

            var groupWrestlers = GetGroupWrestlers().ToList();

            foreach (var wrestler in groupWrestlers)
            {
                var wins = matches.Where(p =>
                        (p.Status == MatchStatusEnum.Completed && p.WrestlerInRed == wrestler && p.IsRedWon.Value)
                        || (p.Status == MatchStatusEnum.Completed && p.WrestlerInBlue == wrestler && p.IsBlueWon))
                    .ToList()
                    .Count;
                wrestler.FinalPlace = wins * 10;
            }

            foreach (var wr in groupWrestlers)
            {
                var samePoints = groupWrestlers.FirstOrDefault(p => p.FinalPlace == wr.FinalPlace && p != wr);
                while (samePoints != null && samePoints.FinalPlace != 0)
                {
                    var match = matches.FirstOrDefault(p =>
                        (p.WrestlerInRed == wr && p.WrestlerInBlue == samePoints)
                        || (p.WrestlerInBlue == wr && p.WrestlerInRed == samePoints));

                    if (match != null)
                    {
                        match.WrestlerInRed.FinalPlace = match.IsRedWon.Value
                            ? match.WrestlerInRed.FinalPlace + 1
                            : match.WrestlerInRed.FinalPlace - 1;
                        match.WrestlerInBlue.FinalPlace = match.IsBlueWon
                            ? match.WrestlerInBlue.FinalPlace + 1
                            : match.WrestlerInBlue.FinalPlace - 1;
                    }
                    else
                    {
                        break;
                    }

                    samePoints = groupWrestlers.FirstOrDefault(p => p.FinalPlace == wr.FinalPlace);
                }
            }

            var orderedWrestlers = groupWrestlers.OrderByDescending(p => p.FinalPlace).ToList();
            for (int i = 0; i < orderedWrestlers.Count; i++)
            {
                orderedWrestlers[i].FinalPlace = i + 1;
            }
            */
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
