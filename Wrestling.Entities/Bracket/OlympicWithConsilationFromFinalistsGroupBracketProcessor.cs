using System;
using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Bracket
{
    public class OlympicWithConsilationFromFinalistsGroupBracketProcessor : OlympicGroupBracketProcessor
    {
        public override string Title => "Олимпийская с утешением от финалистов";
        public override string Code => BracketTypeEnum.OlympicConsilationFinalists.ToString();

        protected override void GenerateAdditionalRounds()
        {
            if (Group.Wrestlers.Count < 4) return;

            int additionalRoundsCount = Group.Bracket.Rounds.Count - 2;

            if (additionalRoundsCount <= 0) return;

            for (int i = 1; i <= additionalRoundsCount; i++)
            {
                var roundNumber = Group.Bracket.Rounds.Count + 1;

                var additionalRound = new GroupRound
                {
                    RoundNumber = roundNumber,
                    RoundName = i < additionalRoundsCount ? "Утешение Раунд " + i : "3-е место",
                    RoundType = GroupRoundTypeEnum.Additional,
                };

                var upperMatch = GenerateGroupMatch(additionalRound.RoundNumber, additionalRound.RoundName, null, null, 1, false);
                upperMatch.NextMatchBracketFullNumber = $"{upperMatch.RoundNumber + 1}.{1}";
                var lowerMatch = GenerateGroupMatch(additionalRound.RoundNumber, additionalRound.RoundName, null, null, 2, false);
                lowerMatch.NextMatchBracketFullNumber = $"{upperMatch.RoundNumber + 1}.{2}";

                additionalRound.RoundMatches.Add(upperMatch);
                additionalRound.RoundMatches.Add(lowerMatch);

                Group.Bracket.Rounds.Add(additionalRound);
            }

            //Clear last addtional round next wrestlingMatch
            Group.Bracket.Rounds[Group.Bracket.Rounds.Count - 1].RoundMatches[0].NextMatchBracketFullNumber = string.Empty;
            Group.Bracket.Rounds[Group.Bracket.Rounds.Count - 1].RoundMatches[1].NextMatchBracketFullNumber = string.Empty;
        }

        protected override void ProceedToAdditionalBracket(WrestlingMatch wrestlingMatch)
        {
            var addRounds = Group.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Additional).ToList();
            var mainRounds = Group.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Main).ToList();
            if (addRounds.Count == 0 || wrestlingMatch.RoundNumber != (mainRounds.Count - 1)) return;

            var winner = wrestlingMatch.IsRedWon.Value ? wrestlingMatch.WrestlerInRed : wrestlingMatch.WrestlerInBlue;
            var looser = wrestlingMatch.IsRedWon.Value ? wrestlingMatch.WrestlerInBlue : wrestlingMatch.WrestlerInRed;

            if (looser == null) return;

            // Put semi-final looser into last Additional bracket wrestlingMatch
            /*            
            var targetMatch = isUpperBracket ? lastAdditionalRound.RoundMatches[0] : lastAdditionalRound.RoundMatches[1];
            targetMatch.WrestlerInRed = looser;
            */
            var lastAdditionalRound = addRounds[addRounds.Count - 1];
            bool isUpperBracket = wrestlingMatch.MatchNumber % 2 != 0;

            // Get all previous loosers and fill other additional rounds with them
            var looseMatches = Group.Bracket.Rounds
                .Where(p => p.RoundType == GroupRoundTypeEnum.Main)
                .SelectMany(x => x.RoundMatches)
                .Where(o => o.Status == MatchStatusEnum.Completed 
                    && (o.IsRedWon.Value && o.WrestlerInRed == winner || o.IsBlueWon && o.WrestlerInBlue == winner))
                .OrderByDescending(a => a.RoundNumber)
                .ToList();
            
            for (int i = 0; i < looseMatches.Count; i++)
            {
                WrestlingMatch nextAddWrestlingMatch;

                if (addRounds.Count - 1 - i < 0)
                {
                    nextAddWrestlingMatch = isUpperBracket ? addRounds[0].RoundMatches[0] : addRounds[0].RoundMatches[1];
                }
                else
                {
                    nextAddWrestlingMatch = isUpperBracket ? addRounds[addRounds.Count - 1 - i].RoundMatches[0] : addRounds[addRounds.Count - 1 - i].RoundMatches[1];
                }
                
                if (nextAddWrestlingMatch.WrestlerInRed == null)
                {
                    nextAddWrestlingMatch.WrestlerInRed = looseMatches[i].IsRedWon.Value
                        ? looseMatches[i].WrestlerInBlue
                        : looseMatches[i].WrestlerInRed;
                }
                else if (nextAddWrestlingMatch.WrestlerInBlue == null)
                {
                    nextAddWrestlingMatch.WrestlerInBlue = looseMatches[i].IsRedWon.Value
                        ? looseMatches[i].WrestlerInBlue
                        : looseMatches[i].WrestlerInRed;
                }
            }

            var upperAddMatch = addRounds[0].RoundMatches[0];
            if (upperAddMatch.Status == MatchStatusEnum.Pending && upperAddMatch.WrestlerInRed != null && upperAddMatch.WrestlerInBlue == null)
            {
                CompleteMatch(upperAddMatch, true, MatchWinTypeEnum.FreeWin);
            }

            var lowerAddMatch = addRounds[0].RoundMatches[1];
            if (lowerAddMatch.Status == MatchStatusEnum.Pending && lowerAddMatch.WrestlerInRed != null && lowerAddMatch.WrestlerInBlue == null)
            {
                CompleteMatch(lowerAddMatch, true, MatchWinTypeEnum.FreeWin);
            }
        }

        protected override void RevertAdditionalBracket(WrestlingMatch wrestlingMatch)
        {
            if (wrestlingMatch.RoundNumber == Group.Bracket.Rounds.Count(p => p.RoundType == GroupRoundTypeEnum.Main) - 1)
            {
                // If it is semi-final, we need to clean Additional bracket which was build based on this wrestlingMatch result
                bool isUpperBracket = wrestlingMatch.BracketNumber % 2 != 0;

                var matches = Group.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Additional).SelectMany(m => m.RoundMatches).Where(x => x.BracketNumber == (isUpperBracket ? 1 : 2)).ToList();
                foreach (var addMatch in matches)
                {
                    addMatch.WrestlerInRed = null;
                    addMatch.WrestlerInBlue = null;
                    addMatch.Status = MatchStatusEnum.Pending;
                    addMatch.WinType = null;
                    addMatch.PointsRed = 0;
                    addMatch.PointsBlue = 0;
                    addMatch.WarningsNumberBlue = 0;
                    addMatch.WarningsNumberRed = 0;
                    addMatch.StartDateTime = null;
                }
            }
        }

        public override bool CanMatchBeReverted(WrestlingMatch wrestlingMatch)
        {
            if (wrestlingMatch.Status == MatchStatusEnum.Pending || !wrestlingMatch.WinType.HasValue || (wrestlingMatch.RoundNumber == 1 && wrestlingMatch.WinType.Value == MatchWinTypeEnum.FreeWin)) return false;

            var baseCheck = true;

            if (!string.IsNullOrEmpty(wrestlingMatch.NextMatchBracketFullNumber))
            {
                var nextMatch = Group.Bracket.Rounds.SelectMany(p => p.RoundMatches).FirstOrDefault(x => x.BracketFullNumber == wrestlingMatch.NextMatchBracketFullNumber);
                if (nextMatch == null) throw new ApplicationException("Next wrestlingMatch does not exist!");

                baseCheck = nextMatch.Status == MatchStatusEnum.Pending;
            }

            if(!baseCheck) return false;

            // If it is semi-final we need to check all additional rounds too which were build based on this wrestlingMatch result
            if (wrestlingMatch.RoundNumber == Group.Bracket.Rounds.Count(p => p.RoundType == GroupRoundTypeEnum.Main) - 1)
            {
                bool isUpperBracket = wrestlingMatch.BracketNumber % 2 != 0;

                var matches = Group.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Additional).SelectMany(m => m.RoundMatches).Where(x => x.BracketNumber == (isUpperBracket ? 1 : 2)).ToList();
                return matches.FirstOrDefault(m => m.Status == MatchStatusEnum.Completed && m.WinType != MatchWinTypeEnum.FreeWin) == null;
            }

            return true;
        }

        protected override void CalculateResults()
        {
            if (Group.Bracket == null) return;

            var addRounds = Group.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Additional).OrderBy(x => x.RoundNumber).ToList();
            var mainRounds = Group.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Main).OrderBy(x => x.RoundNumber).ToList();
            if (mainRounds.Count == 0) return;
            
            // Set 1-2 places from final wrestlingMatch
            if (mainRounds.Count > 0)
            {
                var finalMatch = mainRounds[mainRounds.Count - 1].RoundMatches.First();

                if (finalMatch.Status == MatchStatusEnum.Completed)
                {
                    if (finalMatch.WrestlerInRed != null) finalMatch.WrestlerInRed.FinalPlace = finalMatch.IsRedWon.Value ? 1 : 2;
                    if (finalMatch.WrestlerInBlue != null) finalMatch.WrestlerInBlue.FinalPlace = finalMatch.IsBlueWon ? 1 : 2;
                }
            }

            int currentPlace = 4;
            // Get Additional bracket finals and define two 3rd places
            if (addRounds.Count > 0)
            {
                var addFinals = addRounds[addRounds.Count - 1];

                var firstAddFinal = addFinals.RoundMatches[0];
                if (firstAddFinal != null && firstAddFinal.Status == MatchStatusEnum.Completed)
                {
                    if (firstAddFinal.WrestlerInRed != null)
                    {
                        firstAddFinal.WrestlerInRed.FinalPlace = firstAddFinal.IsRedWon.Value ? 3 : currentPlace;
                    }
                    if (firstAddFinal.WrestlerInBlue != null)
                    {
                        firstAddFinal.WrestlerInBlue.FinalPlace = firstAddFinal.IsBlueWon ? 3 : currentPlace;
                        currentPlace++;
                    }
                }

                if (addFinals.RoundMatches.Count > 1)
                {
                    var secondAddFinal = addFinals.RoundMatches[1];
                    if (secondAddFinal != null && secondAddFinal.Status == MatchStatusEnum.Completed)
                    {
                        if (secondAddFinal.WrestlerInRed != null)
                        {
                            if (secondAddFinal.IsRedWon.Value)
                            {
                                secondAddFinal.WrestlerInRed.FinalPlace = 3;
                            }
                            else
                            {
                                secondAddFinal.WrestlerInRed.FinalPlace = currentPlace;
                                currentPlace++;
                            }
                        }

                        if (secondAddFinal.WrestlerInBlue != null)
                        {
                            if (secondAddFinal.IsBlueWon)
                            {
                                secondAddFinal.WrestlerInBlue.FinalPlace = 3;
                            }
                            else
                            {
                                secondAddFinal.WrestlerInBlue.FinalPlace = currentPlace;
                                currentPlace++;
                            }
                        }
                    }
                }

                // Calculate wins from other additional bracket
                if (addRounds.Count > 1)
                {
                    for (int i = addRounds.Count - 2; i >= 0; i--)
                    {
                        var round = addRounds[i];

                        var firstRoundMatch = round.RoundMatches[0];
                        var secondRoundMatch = round.RoundMatches[1];

                        if (firstRoundMatch.Status == MatchStatusEnum.Completed)
                        {
                            if (firstRoundMatch.WrestlerInRed != null && firstRoundMatch.WrestlerInBlue == null && !firstRoundMatch.WrestlerInRed.FinalPlace.HasValue)
                            {
                                firstRoundMatch.WrestlerInRed.FinalPlace = currentPlace;
                                currentPlace++;
                            }
                            else if (firstRoundMatch.WrestlerInRed != null && firstRoundMatch.WrestlerInBlue != null)
                            {
                                if (firstRoundMatch.IsRedWon.Value)
                                {
                                    if (!firstRoundMatch.WrestlerInRed.FinalPlace.HasValue)
                                    {
                                        firstRoundMatch.WrestlerInRed.FinalPlace = currentPlace;
                                        currentPlace++;
                                    }

                                    if (!firstRoundMatch.WrestlerInBlue.FinalPlace.HasValue)
                                    {
                                        firstRoundMatch.WrestlerInBlue.FinalPlace = currentPlace;
                                        currentPlace++;
                                    }
                                }
                                else
                                {
                                    if (!firstRoundMatch.WrestlerInBlue.FinalPlace.HasValue)
                                    {
                                        firstRoundMatch.WrestlerInBlue.FinalPlace = currentPlace;
                                        currentPlace++;
                                    }

                                    if (!firstRoundMatch.WrestlerInRed.FinalPlace.HasValue)
                                    {
                                        firstRoundMatch.WrestlerInRed.FinalPlace = currentPlace;
                                        currentPlace++;
                                    }
                                }
                            }
                        }

                        if (secondRoundMatch.Status == MatchStatusEnum.Completed)
                        {
                            if (secondRoundMatch.WrestlerInRed != null && secondRoundMatch.WrestlerInBlue == null && !secondRoundMatch.WrestlerInRed.FinalPlace.HasValue)
                            {
                                secondRoundMatch.WrestlerInRed.FinalPlace = currentPlace;
                                currentPlace++;
                            }
                            else if (secondRoundMatch.WrestlerInRed != null && secondRoundMatch.WrestlerInBlue != null)
                            {
                                if (secondRoundMatch.IsRedWon.Value)
                                {
                                    if (!secondRoundMatch.WrestlerInRed.FinalPlace.HasValue)
                                    {
                                        secondRoundMatch.WrestlerInRed.FinalPlace = currentPlace;
                                        currentPlace++;
                                    }

                                    if (!secondRoundMatch.WrestlerInBlue.FinalPlace.HasValue)
                                    {
                                        secondRoundMatch.WrestlerInBlue.FinalPlace = currentPlace;
                                        currentPlace++;
                                    }
                                }
                                else
                                {
                                    if (!secondRoundMatch.WrestlerInBlue.FinalPlace.HasValue)
                                    {
                                        secondRoundMatch.WrestlerInBlue.FinalPlace = currentPlace;
                                        currentPlace++;
                                    }

                                    if (!secondRoundMatch.WrestlerInRed.FinalPlace.HasValue)
                                    {
                                        secondRoundMatch.WrestlerInRed.FinalPlace = currentPlace;
                                        currentPlace++;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Calculate wins from other main bracket
            // For finals places are already there so need to start from semi-finals
            if (mainRounds.Count > 1)
            {
                // Just proceed with 2 3rd places
                if (mainRounds.Count == 2)
                {
                    foreach (var wr in GetGroupWrestlers())
                    {
                        if (!wr.FinalPlace.HasValue) wr.FinalPlace = 3;
                    }
                }
                else
                {
                    for (int i = mainRounds.Count - 2; i >= 0; i--)
                    {
                        var round = mainRounds[i];

                        foreach (var match in round.RoundMatches)
                        {
                            if (match.Status == MatchStatusEnum.Pending) continue;

                            if (match.IsRedWon.Value && match.WrestlerInBlue != null && !match.WrestlerInBlue.FinalPlace.HasValue)
                            {
                                match.WrestlerInBlue.FinalPlace = currentPlace;
                                currentPlace++;
                            }
                            else if (match.IsBlueWon && match.WrestlerInRed != null && !match.WrestlerInRed.FinalPlace.HasValue)
                            {
                                match.WrestlerInRed.FinalPlace = currentPlace;
                                currentPlace++;
                            }
                        }
                    }
                }
            }

            if (Group.IsBracketCompleted)
            {
                foreach (var wr in GetGroupWrestlers())
                {
                    if (!wr.FinalPlace.HasValue)
                    {
                        wr.FinalPlace = currentPlace;
                        currentPlace++;
                    }
                }
            }
        }        

        public override GroupRound Get3rdPlaceRound(AgeWeightGroup group)
        {
            if (group == null || group.Bracket == null || group.Bracket.Rounds == null) return null;

            var addRounds = group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();

            if (addRounds.Count == 0) return null;

            return addRounds[addRounds.Count - 1];
        }

        public override List<GroupRound> GetAdditionalQualificationRounds(AgeWeightGroup group)
        {
            if (group == null || group.Bracket == null || group.Bracket.Rounds == null) return null;

            var addRounds = group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();

            if (addRounds.Count == 0) return null;

            addRounds.RemoveAt(addRounds.Count - 1);            

            return addRounds;
        }
    }
}
