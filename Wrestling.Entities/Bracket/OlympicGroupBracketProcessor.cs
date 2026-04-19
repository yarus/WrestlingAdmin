using System;
using System.Linq;

namespace Wrestling.Entities.Bracket
{
    public class OlympicGroupBracketProcessor : GroupBracketProcessorBase
    {
        public override string Title => "Олимпийская с матчем за 3-е место";
        public override string Code => BracketTypeEnum.Olympic.ToString();
        public override int? AthletesMinCount => 4;
        public override int? AthletesMaxCount => 64;

        protected override void GenerateMainRounds()
        {
            GenerateFirstRound();
            FillBracketWithEmptyMatches();
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
                    if (thirdPlaceMatch.WrestlerInRed.SameAs(wrestlingMatch.WrestlerInBlue)) thirdPlaceMatch.WrestlerInRed = null;
                    else if (thirdPlaceMatch.WrestlerInBlue.SameAs(wrestlingMatch.WrestlerInBlue)) thirdPlaceMatch.WrestlerInBlue = null;
                }
                else if (wrestlingMatch.IsRedWon.HasValue && !wrestlingMatch.IsRedWon.Value)
                {
                    if (thirdPlaceMatch.WrestlerInRed.SameAs(wrestlingMatch.WrestlerInRed)) thirdPlaceMatch.WrestlerInRed = null;
                    else if (thirdPlaceMatch.WrestlerInBlue.SameAs(wrestlingMatch.WrestlerInRed)) thirdPlaceMatch.WrestlerInBlue = null;
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
                // BracketNumber of the semifinal (1 or 2) drives 3rd-place slot.
                // MatchNumber is only populated after IMatchNumbersGenerator runs,
                // so relying on it here corrupted the additional round when matches
                // completed before scheduling numbers were assigned.
                if (wrestlingMatch.BracketNumber % 2 == 0)
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

            if (final.Status == MatchStatusEnum.Completed)
            {
                var winner = final.IsRedWon.Value ? final.WrestlerInRed : final.WrestlerInBlue;
                if (winner != null)
                {
                    winner.FinalPlace = 1;
                }

                var looser = final.IsRedWon.Value ? final.WrestlerInBlue : final.WrestlerInRed;
                if (looser != null)
                {
                    looser.FinalPlace = 2;
                }
            }

            var addRounds = Group.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Additional).ToList();
            if (addRounds.Count > 0)
            {
                var addFinal = addRounds[0].RoundMatches[0];

                if (addFinal.Status == MatchStatusEnum.Completed)
                {
                    var addWinner = addFinal.IsRedWon.Value ? addFinal.WrestlerInRed : addFinal.WrestlerInBlue;
                    if (addWinner != null)
                    {
                        addWinner.FinalPlace = 3;
                    }

                    var addLooser = addFinal.IsRedWon.Value ? addFinal.WrestlerInBlue : addFinal.WrestlerInRed;
                    if (addLooser != null)
                    {
                        addLooser.FinalPlace = 4;
                    }                    
                }
            }

            int currentPlace = 5;

            foreach (var match in mainRounds.OrderByDescending(p => p.RoundNumber).SelectMany(x => x.RoundMatches))
            {
                if (match.Status != MatchStatusEnum.Completed)
                {
                    continue;
                }
            
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
        private void FillBracketWithEmptyMatches()
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

        public override GroupRound Get3rdPlaceRound(AgeWeightGroup group)
        {
            if (group == null || group.Bracket == null || group.Bracket.Rounds == null || group.Bracket.Rounds.Count == 0)
            {
                return null;
            }

            var addRound = group.Bracket.Rounds.FirstOrDefault(r => r.RoundType == GroupRoundTypeEnum.Additional);

            return addRound;
        }
    }
}
