using System;
using System.Linq;

namespace Wrestling.Entities.Bracket
{
    public class OlympicGroupBracketProcessor : GroupBracketProcessorBase
    {
        public override string Title => "Олимпийская с матчем за 3-е место";
        public override string Code => BracketTypeEnum.Olympic.ToString();
        public override int? AthletsMinCount => 4;
        public override int? AthletsMaxCount => 64;

        protected override void GenerateMainRounds()
        {
            GenerateFirstRound();
            FeelBracketWithEmptyMatches();
            CollectFreeWinsForFirstRound();
        }

        protected override void GenerateAdditionalRounds()
        {
            if (Group.Wrestlers.Count < 4) return;

            var thirdPlaceRound = new GroupRound
            {
                RoundName = "3-е место",
                RoundNumber = Group.Bracket.Rounds.Count + 1,
                RoundType = GroupRoundTypeEnum.Additional
            };

            var thirdPlaceMatch = GenerateGroupMatch(thirdPlaceRound.RoundNumber, thirdPlaceRound.RoundName, null, null, 1, false);
            thirdPlaceMatch.NextMatchBracketFullNumber = string.Empty;
            thirdPlaceRound.RoundMatches.Add(thirdPlaceMatch);

            Group.Bracket.Rounds.Add(thirdPlaceRound);
        }

        protected override void RevertAdditionalBracket(WrestlingMatch wrestlingMatch)
        {
            if (wrestlingMatch.RoundNumber == Group.Bracket.Rounds.Count(p => p.RoundType == GroupRoundTypeEnum.Main) - 1)
            {
                var thirdPlaceRound = Group.Bracket.Rounds.FirstOrDefault(p => p.RoundType == GroupRoundTypeEnum.Additional);
                if (thirdPlaceRound == null) return;

                var thirdPlaceMatch = thirdPlaceRound.RoundMatches[0];

                if (wrestlingMatch.IsRedWon.HasValue && wrestlingMatch.IsRedWon.Value)
                {
                    if (thirdPlaceMatch.WrestlerInRed == wrestlingMatch.WrestlerInBlue) thirdPlaceMatch.WrestlerInRed = null;
                    else if (thirdPlaceMatch.WrestlerInBlue == wrestlingMatch.WrestlerInBlue) thirdPlaceMatch.WrestlerInBlue = null;
                }
                else if (wrestlingMatch.IsRedWon.HasValue && !wrestlingMatch.IsRedWon.Value)
                {
                    if (thirdPlaceMatch.WrestlerInRed == wrestlingMatch.WrestlerInRed) thirdPlaceMatch.WrestlerInRed = null;
                    else if (thirdPlaceMatch.WrestlerInBlue == wrestlingMatch.WrestlerInRed) thirdPlaceMatch.WrestlerInBlue = null;
                }

                thirdPlaceMatch.Status = MatchStatusEnum.Pending;
                thirdPlaceMatch.WinType = null;
                thirdPlaceMatch.PointsRed = 0;
                thirdPlaceMatch.WarningsNumberBlue = 0;
                thirdPlaceMatch.WarningsNumberRed = 0;
                thirdPlaceMatch.PointsBlue = 0;
                thirdPlaceMatch.StartDateTime = null;
            }
        }

        public override bool CanMatchBeReverted(WrestlingMatch wrestlingMatch)
        {
            var baseCheck = base.CanMatchBeReverted(wrestlingMatch);

            if (!baseCheck) return false;

            // If it is semi-final we need to check additional round too
            if (wrestlingMatch.RoundNumber == Group.Bracket.Rounds.Count(p => p.RoundType == GroupRoundTypeEnum.Main) - 1)
            {
                var thirdPlaceRound = Group.Bracket.Rounds.FirstOrDefault(p => p.RoundType == GroupRoundTypeEnum.Additional);
                if (thirdPlaceRound == null) return true;

                return thirdPlaceRound.RoundMatches[0].Status == MatchStatusEnum.Pending;
            }

            return true;
        }

        protected override void ProceedToAdditionalBracket(WrestlingMatch wrestlingMatch)
        {
            var mainRounds = Group.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Main).ToList();
            if (wrestlingMatch.RoundNumber != mainRounds.Count - 1) return;

            var additionalRound = Group.Bracket.Rounds.FirstOrDefault(p => p.RoundType == GroupRoundTypeEnum.Additional);
            if (additionalRound == null) return;

            var addMatch = additionalRound.RoundMatches[0];

            var looser = wrestlingMatch.IsRedWon.Value ? wrestlingMatch.WrestlerInBlue : wrestlingMatch.WrestlerInRed;
            if (looser != null)
            {
                if (wrestlingMatch.MatchNumber % 2 == 0)
                {
                    addMatch.WrestlerInBlue = looser;
                }
                else
                {
                    addMatch.WrestlerInRed = looser;
                }
            }

            // Check if both semi-finals completed but addtional bracket final is empty -> means free wins
            if (mainRounds.Count > 1)
            {
                var semifinals = mainRounds[mainRounds.Count - 2];
                if (semifinals.RoundMatches.FirstOrDefault(p => p.Status == MatchStatusEnum.Pending) == null)
                {
                    if (addMatch.WrestlerInRed == null && addMatch.WrestlerInBlue != null)
                    {
                        CompleteMatch(addMatch, false, MatchWinTypeEnum.FreeWin);
                    } else if (addMatch.WrestlerInBlue == null && addMatch.WrestlerInRed != null)
                    {
                        CompleteMatch(addMatch, true, MatchWinTypeEnum.FreeWin);
                    }
                }
            }
        }

        protected override void CalculateResults()
        {
            if (Group.Bracket == null) return;

            var mainRounds = Group.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Main).ToList();
            var final = mainRounds[mainRounds.Count - 1].RoundMatches[0];

            int currentPlace = 1;

            var winner = final.IsRedWon.Value ? final.WrestlerInRed : final.WrestlerInBlue;
            if (winner != null)
            {
                winner.FinalPlace = currentPlace;
                currentPlace++;
            }

            var looser = final.IsRedWon.Value ? final.WrestlerInBlue : final.WrestlerInRed;
            if (looser != null)
            {
                looser.FinalPlace = currentPlace;
                currentPlace++;
            }

            var addRounds = Group.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Additional).ToList();
            if (addRounds.Count > 0)
            {
                var addFinal = addRounds[0].RoundMatches[0];
                var addWinner = addFinal.IsRedWon.Value ? addFinal.WrestlerInRed : addFinal.WrestlerInBlue;
                if (addWinner != null)
                {
                    addWinner.FinalPlace = currentPlace;
                    currentPlace++;
                }

                var addLooser = addFinal.IsRedWon.Value ? addFinal.WrestlerInBlue : addFinal.WrestlerInRed;
                if (addLooser != null)
                {
                    addLooser.FinalPlace = currentPlace;
                    currentPlace++;
                }
            }

            foreach (var match in mainRounds.OrderByDescending(p => p.RoundNumber).SelectMany(x => x.RoundMatches))
            {
                var matchWinner = match.IsRedWon.Value ? match.WrestlerInRed : match.WrestlerInBlue;
                if (matchWinner != null && !matchWinner.FinalPlace.HasValue)
                {
                    matchWinner.FinalPlace = currentPlace;
                    currentPlace++;
                }

                var matchLooser = match.IsRedWon.Value ? match.WrestlerInBlue : match.WrestlerInRed;
                if (matchLooser != null && !matchLooser.FinalPlace.HasValue)
                {
                    matchLooser.FinalPlace = currentPlace;
                    currentPlace++;
                }
            }
        }

        private void GenerateFirstRound()
        {
            int wrestlersCount = Group.Wrestlers.Count;
            int totalCells = GetTotalCellsForFirstRound(wrestlersCount);
            int fullMatches = (2 * wrestlersCount - totalCells) / 2;
            int freeMatches = wrestlersCount - fullMatches * 2;
            int roundsCount = GetRoundsCount(wrestlersCount);

            var round = new GroupRound
            {
                RoundNumber = 1,
                RoundName = GetRoundNameForRound(1, roundsCount == 0 ? 1 : roundsCount),
                RoundType = GroupRoundTypeEnum.Main
            };

            for (int i = 0; i < freeMatches; i++)
            {
                var wrestler1 = Group.Wrestlers[i];
                round.RoundMatches.Add(GenerateGroupMatch(round.RoundNumber, round.RoundName, wrestler1, null, (i + 1), true));
            }

            for (int i = 0; i < fullMatches; i++)
            {
                var wrestler1 = Group.Wrestlers[freeMatches + i * 2];
                var wrestler2 = Group.Wrestlers[freeMatches + i * 2 + 1];

                round.RoundMatches.Add(GenerateGroupMatch(round.RoundNumber, round.RoundName, wrestler1, wrestler2, freeMatches + i + 1, true));
            }

            /*

            for (int i = 0; i < fullMatches; i++)
            {
                var wrestler1 = Group.Wrestlers[i * 2];
                var wrestler2 = Group.Wrestlers[i * 2 + 1];

                round.RoundMatches.Add(GenerateGroupMatch(round.RoundNumber, round.RoundName, wrestler1, wrestler2, i + 1, true));
            }

            for (int i = 0; i < freeMatches; i++)
            {
                var wrestler1 = Group.Wrestlers[fullMatches * 2 + i];
                round.RoundMatches.Add(GenerateGroupMatch(round.RoundNumber, round.RoundName, wrestler1, null, fullMatches + (i + 1), true));
            }

            */

            Group.Bracket.Rounds.Add(round);
        }
        private void FeelBracketWithEmptyMatches()
        {
            var firstRound = Group.Bracket.Rounds[0];
            int totalRounds = GetRoundsCount(Group.Bracket.WrestlersCount);
            int matches = firstRound.RoundMatches.Count / 2;
            var currentRound = Group.Bracket.Rounds.Count + 1;

            while (currentRound <= totalRounds)
            {
                var nextRound = new GroupRound
                {
                    RoundName = GetRoundNameForRound(currentRound, totalRounds),
                    RoundNumber = currentRound,
                    RoundType = GroupRoundTypeEnum.Main
                };

                for (int i = 0; i < matches; i++)
                {
                    nextRound.RoundMatches.Add(GenerateGroupMatch(nextRound.RoundNumber, nextRound.RoundName, null, null, i + 1, currentRound != totalRounds));
                }

                Group.Bracket.Rounds.Add(nextRound);

                currentRound = Group.Bracket.Rounds.Count + 1;
                matches = (int)Math.Floor((double)nextRound.RoundMatches.Count / 2);
            }

            // Remove NextMatchNumber from final
            Group.Bracket.Rounds[Group.Bracket.Rounds.Count - 1].RoundMatches[0].NextMatchBracketFullNumber = string.Empty;
        }
        private void CollectFreeWinsForFirstRound()
        {
            var firstRound = Group.Bracket.Rounds[0];

            foreach (var match in firstRound.RoundMatches)
            {
                if (match.WrestlerInBlue == null)
                {
                    CompleteMatch(match, true, MatchWinTypeEnum.FreeWin);
                }
            }
        }
        private string GetRoundNameForRound(int roundNumber, int totalRounds)
        {
            string result;

            if (roundNumber == totalRounds)
            {
                result = "Финал";
            }
            else if (roundNumber == totalRounds - 1)
            {
                result = "Полуфинал";
            }
            else
            {
                result = $"1/{Math.Pow(2, totalRounds - roundNumber)} финала";
            }

            return result;
        }
        private int GetTotalCellsForFirstRound(int wrestlers)
        {
            double result = 0;
            int n = 1;
            while (result < wrestlers)
            {
                result = Math.Pow(2, n);
                n++;
            }
            return (int)result;
        }
    }
}
