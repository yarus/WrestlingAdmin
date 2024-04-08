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

            var final = GetFinalRound(Group).RoundMatches[0];

            var isUpperBracket = final.WrestlerInRed != null && final.WrestlerInRed.ID == winner.ID;

            var lastAdditionalRound = addRounds[addRounds.Count - 1];
            
            // Put semi-final looser into last Additional bracket wrestlingMatch
            var targetMatch = isUpperBracket ? lastAdditionalRound.RoundMatches[0] : lastAdditionalRound.RoundMatches[1];
            targetMatch.WrestlerInRed = looser;

            // Get all previous loosers and fill other additional rounds with them
            var looseMatches = Group.Bracket.Rounds
                .Where(p => p.RoundType == GroupRoundTypeEnum.Main)
                .SelectMany(x => x.RoundMatches)
                .Where(o => o.Status == MatchStatusEnum.Completed 
                    && (o.IsRedWon.Value && o.WrestlerInRed == winner || o.IsBlueWon && o.WrestlerInBlue == winner))
                .Where(m => m.WrestlerInRed?.ID != looser.ID && m.WrestlerInBlue?.ID != looser.ID)
                .OrderByDescending(a => a.RoundNumber)
                .ToList();
            
            // If only one loose matches, add another looser as blue wrestler
            if (looseMatches.Count == 1)
            {
                targetMatch.WrestlerInBlue = looseMatches[0].IsRedWon.Value
                    ? looseMatches[0].WrestlerInBlue
                    : looseMatches[0].WrestlerInRed;
            }
            else
            {
                // If more then 1 match, we need to put them in previous brackets
                for (int i = 0; i < looseMatches.Count; i++)
                {
                    WrestlingMatch nextAddWrestlingMatch;

                    if (addRounds.Count - 2 - i < 0)
                    {
                        nextAddWrestlingMatch = isUpperBracket ? addRounds[0].RoundMatches[0] : addRounds[0].RoundMatches[1];
                    }
                    else
                    {
                        nextAddWrestlingMatch = isUpperBracket ? addRounds[addRounds.Count - 2 - i].RoundMatches[0] : addRounds[addRounds.Count - 2 - i].RoundMatches[1];
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

        private void DefineWinnerAndLoserPlace(WrestlingMatch match, int winnerPlance, int looserPlace)
        {
            if (match == null || !match.IsMatchCompleted) return;
            
            if (match.WrestlerInRed != null) match.WrestlerInRed.FinalPlace = match.IsRedWon.Value ? winnerPlance : looserPlace;
            if (match.WrestlerInBlue != null) match.WrestlerInBlue.FinalPlace = match.IsBlueWon ? winnerPlance : looserPlace;
        }
        
        private void DefineGoldAndSilver(GroupBracket bracket)
        {
            var mainRounds = bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Main).OrderBy(x => x.RoundNumber).ToList();
            
            if (mainRounds.Count == 0) return;
            
            var finalMatch = mainRounds[mainRounds.Count - 1].RoundMatches.First();

            DefineWinnerAndLoserPlace(finalMatch, 1, 2);
        }

        private void DefineBronzeAndFifthForAddFinal(WrestlingMatch addFinal)
        {
            if (addFinal == null || !addFinal.IsMatchCompleted) return;

            DefineWinnerAndLoserPlace(addFinal, 3, 5);
        }

        private void DefineBronzeAndFifth(GroupBracket bracket)
        {
            var addRounds = bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Additional).OrderBy(x => x.RoundNumber).ToList();
            
            if (addRounds.Count == 0) return;
            
            var addFinals = addRounds[addRounds.Count - 1];
            
            if (addFinals.RoundMatches.Count == 0) return;

            var firstAddFinal = addFinals.RoundMatches[0];
            DefineBronzeAndFifthForAddFinal(firstAddFinal);
            
            if (addFinals.RoundMatches.Count == 1) return;

            var secondAddFinal = addFinals.RoundMatches[1];
            DefineBronzeAndFifthForAddFinal(secondAddFinal);
        }
        
        protected override void CalculateResults()
        {
            if (Group.Bracket == null) return;

            // Set 1-2 places from final wrestlingMatch
            DefineGoldAndSilver(Group.Bracket);

            // Get Additional bracket finals and define two 3rd places and two 5th places
            DefineBronzeAndFifth(Group.Bracket);

            if (!Group.IsBracketCompleted) return;

            // Define all places from 7th based on qualification points
            // Remove wrestlers with place already set and order by qualification points
            var statistics = GetStats().Where(x => !x.Wrestler.FinalPlace.HasValue)
                .OrderByDescending(x => x.OverallTournamentClassificationPoints)
                .ThenByDescending(x => x.WinsByTushe)
                .ThenByDescending(x => x.WinsByDomination)
                .ThenByDescending(x => x.WinsByDominationWithPoints)
                .ThenByDescending(x => x.AllGainedPoints)
                .ThenBy(x => x.AllLostPoints)
                .ThenBy(x => x.Wrestler.SeedNumber)
                .ToList();

            var currentPlace = 7;
            foreach (var stat in statistics)
            {
                stat.Wrestler.FinalPlace = currentPlace;
                currentPlace++;
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
