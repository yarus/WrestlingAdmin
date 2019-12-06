using System;
using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities.Results;

namespace Wrestling.Entities.Bracket
{
    public class SubGroupsToOlympicBracketPorcessor : GroupBracketProcessorBase
    {
        public override int? AthletsMinCount => 6;
        public override int? AthletsMaxCount => 7;

        private RoundRobinGroupBracketProcessor _groupAProcessor;
        private RoundRobinGroupBracketProcessor _groupBProcessor;

        private AgeWeightGroup _fakeGroupA;
        private AgeWeightGroup _fakeGroupB;

        public override string Title => "2 подгруппы в Олимпийскую с 3м местом";
        public override string Code => BracketTypeEnum.SubGroupsIntoOlympic.ToString();

        private void InitInternalProcessors()
        {
            _fakeGroupA = GenerateFakeGroup();
            _fakeGroupA.Wrestlers.AddRange(Group.Wrestlers.OrderBy(r => r.SeedNumber).Take(Group.Wrestlers.Count == 7 ? 4 : 3));

            _fakeGroupB = GenerateFakeGroup();
            _fakeGroupB.Wrestlers.AddRange(Group.Wrestlers.OrderByDescending(r => r.SeedNumber).Take(3));

            _groupAProcessor = new RoundRobinGroupBracketProcessor();
            _groupAProcessor.LoadTournamentGroup(Tournament, _fakeGroupA);

            _groupBProcessor = new RoundRobinGroupBracketProcessor();
            _groupBProcessor.LoadTournamentGroup(Tournament, _fakeGroupB);

            if (Group.Bracket != null)
            {
                _fakeGroupA.Bracket = new GroupBracket
                {
                    BracketTypeCode = Code,
                    BracketTypeLabel = Title,
                    Rounds = new List<GroupRound>()
                };

                _fakeGroupB.Bracket = new GroupBracket
                {
                    BracketTypeCode = Code,
                    BracketTypeLabel = Title,
                    Rounds = new List<GroupRound>()
                };

                foreach (var round in Group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main))
                {
                    var fakeRoundA = new GroupRound
                    {
                        RoundName = round.RoundName,
                        RoundNumber = round.RoundNumber,
                        RoundType = round.RoundType,
                        RoundMatches = new List<WrestlingMatch>()
                    };

                    var fakeRoundB = new GroupRound
                    {
                        RoundName = round.RoundName,
                        RoundNumber = round.RoundNumber,
                        RoundType = round.RoundType,
                        RoundMatches = new List<WrestlingMatch>()
                    };

                    fakeRoundA.RoundMatches.Add(round.RoundMatches[0]);

                    if (_fakeGroupA.Wrestlers.Count == 4)
                    {
                        fakeRoundA.RoundMatches.Add(round.RoundMatches[1]);
                        fakeRoundB.RoundMatches.Add(round.RoundMatches[2]);
                    }
                    else
                    {
                        fakeRoundB.RoundMatches.Add(round.RoundMatches[1]);
                    }

                    _fakeGroupA.Bracket.Rounds.Add(fakeRoundA);
                    _fakeGroupB.Bracket.Rounds.Add(fakeRoundB);
                }
            }
        }

        protected override void GenerateMainRounds()
        {
            // Main rounds are round-robbin for 2 sub groups
            InitInternalProcessors();

            _groupAProcessor.Generate(Tournament, _fakeGroupA);
            _groupBProcessor.Generate(Tournament, _fakeGroupB);

            // Now we have round robbin rounds for each group which has to be added into the same bracket
            for (int i = 0; i < _fakeGroupA.Bracket.Rounds.Count; i++)
            {
                var round = new GroupRound
                {
                    RoundNumber = i + 1,
                    RoundType = GroupRoundTypeEnum.Main,
                    RoundName = "Раунд " + (i + 1),
                    RoundMatches = new List<WrestlingMatch>()
                };

                for (var x = 0; x < _fakeGroupA.Bracket.Rounds[i].RoundMatches.Count; x++)
                {
                    var match = _fakeGroupA.Bracket.Rounds[i].RoundMatches[x];
                    match.GroupID = Group.ID;
                    match.GroupName = Group.Name;
                    match.RoundName = round.RoundName;
                    match.RoundNumber = round.RoundNumber;

                    round.RoundMatches.Add(match);
                }

                if (i < _fakeGroupB.Bracket.Rounds.Count)
                {
                    var lastMatch = round.RoundMatches[round.RoundMatches.Count - 1];

                    for (var j = 0; j < _fakeGroupB.Bracket.Rounds[i].RoundMatches.Count; j++)
                    {
                        var match = _fakeGroupB.Bracket.Rounds[i].RoundMatches[j];
                        match.GroupID = Group.ID;
                        match.GroupName = Group.Name;
                        match.RoundName = round.RoundName;
                        match.RoundNumber = round.RoundNumber;
                        match.BracketNumber = lastMatch.BracketNumber + j + 1;

                        round.RoundMatches.Add(match);
                    }
                }

                Group.Bracket.Rounds.Add(round);
            }
        }

        private AgeWeightGroup GenerateFakeGroup()
        {
            var fakeGroup = new AgeWeightGroup
            {
                BirthYearMax = Group.BirthYearMax,
                BirthYearMin = Group.BirthYearMin,
                CarpetID = Group.CarpetID,
                CarpetLabel = Group.CarpetLabel,
                IsFemale = Group.IsFemale,
                MaxActionSecond = Group.MaxActionSecond,
                MaxRoundSecond = Group.MaxRoundSecond,
                MaxTimeoutSecond = Group.MaxTimeoutSecond,
                WeightMax = Group.WeightMax,
                Wrestlers = new List<Wrestler>()
            };

            return fakeGroup;
        }

        protected override void GenerateAdditionalRounds()
        {
            GenerateSemiFinals();
            GenerateFinal();
            Generate3rdPlaceMatch();
        }

        private void GenerateFinal()
        {
            var final = new GroupRound
            {
                RoundName = "Финал",
                RoundNumber = Group.Bracket.Rounds.Count + 1,
                RoundType = GroupRoundTypeEnum.Additional,
                RoundMatches = new List<WrestlingMatch>()
            };

            var upperMatch = GenerateGroupMatch(final.RoundNumber, final.RoundName, null, null, 1, false);
            //var lowerMatch = GenerateGroupMatch(medalsMatches.RoundNumber, medalsMatches.RoundName, null, null, 2, false);

            final.RoundMatches.Add(upperMatch);
            //medalsMatches.RoundMatches.Add(lowerMatch);

            Group.Bracket.Rounds.Add(final);
        }

        private void Generate3rdPlaceMatch()
        {
            var thirdPlace = new GroupRound
            {
                RoundName = "3 место",
                RoundNumber = Group.Bracket.Rounds.Count + 1,
                RoundType = GroupRoundTypeEnum.Additional,
                RoundMatches = new List<WrestlingMatch>()
            };

            var upperMatch = GenerateGroupMatch(thirdPlace.RoundNumber, thirdPlace.RoundName, null, null, 1, false);

            thirdPlace.RoundMatches.Add(upperMatch);

            Group.Bracket.Rounds.Add(thirdPlace);
        }

        private void GenerateSemiFinals()
        {
            var semiFinals = new GroupRound
            {
                RoundName = "Полуфинал",
                RoundNumber = Group.Bracket.Rounds.Count + 1,
                RoundType = GroupRoundTypeEnum.Additional,
                RoundMatches = new List<WrestlingMatch>()
            };

            var upperMatch = GenerateGroupMatch(semiFinals.RoundNumber, semiFinals.RoundName, null, null, 1, true);
            //upperMatch.NextMatchBracketFullNumber = $"{upperMatch.RoundNumber + 1}.{1}";

            var lowerMatch = GenerateGroupMatch(semiFinals.RoundNumber, semiFinals.RoundName, null, null, 2, true);
            //lowerMatch.NextMatchBracketFullNumber = $"{upperMatch.RoundNumber + 1}.{2}";

            semiFinals.RoundMatches.Add(upperMatch);
            semiFinals.RoundMatches.Add(lowerMatch);

            Group.Bracket.Rounds.Add(semiFinals);
        }

        protected override void CalculateResults()
        {
            if (Group.Bracket == null) return;

            var notCompletedMainMatches = Group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).SelectMany(p => p.RoundMatches).Where(x => x.Status != MatchStatusEnum.Completed).ToList();
            if (notCompletedMainMatches.Count > 0)
            {
                return;
            }

            InitInternalProcessors();

            var resultsA = _groupAProcessor.GetResults().ToList();
            var resultsB = _groupBProcessor.GetResults().ToList();

            var groupWrestlers = GetGroupWrestlers();
            foreach (var wr in groupWrestlers)
            {
                wr.FinalPlace = null;
            }

            var addRounds = Group.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Additional).ToList();
            var final = addRounds[addRounds.Count - 2].RoundMatches[0];

            int currentPlace = 1;

            var finalists = new List<Wrestler>();

            // 1-2 place
            if (final.Status == MatchStatusEnum.Completed && final.IsRedWon.HasValue)
            {
                var winner = final.IsRedWon.Value ? final.WrestlerInRed : final.WrestlerInBlue;
                if (winner != null)
                {
                    finalists.Add(winner);
                    winner.FinalPlace = currentPlace;
                    currentPlace++;
                }

                var looser = final.IsRedWon.Value ? final.WrestlerInBlue : final.WrestlerInRed;
                if (looser != null)
                {
                    finalists.Add(looser);
                    looser.FinalPlace = currentPlace;
                    currentPlace++;
                }
            } 
            else
            {
                // can't calculate yet
                currentPlace += 2;
            }

            // 3-4 place
            var thirdPlace = addRounds[addRounds.Count - 1].RoundMatches[0];

            if (thirdPlace.Status == MatchStatusEnum.Completed && thirdPlace.IsRedWon.HasValue)
            {
                var bronzeWinner = thirdPlace.IsRedWon.Value ? thirdPlace.WrestlerInRed : thirdPlace.WrestlerInBlue;
                if (bronzeWinner != null)
                {
                    finalists.Add(bronzeWinner);
                    bronzeWinner.FinalPlace = currentPlace;
                    currentPlace++;
                }

                var bronzeLooser = thirdPlace.IsRedWon.Value ? thirdPlace.WrestlerInBlue : thirdPlace.WrestlerInRed;
                if (bronzeLooser != null)
                {
                    finalists.Add(bronzeLooser);
                    bronzeLooser.FinalPlace = currentPlace;
                    currentPlace++;
                }
            }
            else
            {
                // can't calculate yet
                currentPlace += 2;
            }

            // Other places should be set based on main round robin results
            var matches = Group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).SelectMany(p => p.RoundMatches).Where(x => x.Status == MatchStatusEnum.Completed).ToList();
            if (matches.Count == 0)
            {
                return;
            }

            if (_groupAProcessor == null || _groupBProcessor == null)
            {
                return;
            }

            var comparedResults = new List<TournamentResult>();
            comparedResults.AddRange(resultsA.Where(r => !finalists.Contains(r.Wrestler)).Distinct());
            comparedResults.AddRange(resultsB.Where(r => !finalists.Contains(r.Wrestler)).Distinct());

            var finalOrder = comparedResults
                .OrderByDescending(x => x.OverallTournamentRating)
                .ToList();

            foreach (var tournamentResult in finalOrder)
            {
                tournamentResult.Wrestler.FinalPlace = currentPlace;
                currentPlace++;
            }
        }

        protected override void ProceedToNextMatch(WrestlingMatch wrestlingMatch)
        {
            base.ProceedToNextMatch(wrestlingMatch);

            var matchRound = Group.Bracket.Rounds.First(r => r.RoundType == GroupRoundTypeEnum.Additional);
            if (matchRound != null && matchRound.RoundNumber == wrestlingMatch.RoundNumber)
            {
                var thirdfPlaceRound = Group.Bracket.Rounds.Last(r => r.RoundType == GroupRoundTypeEnum.Additional);
                if (thirdfPlaceRound != null)
                {
                    var matchLooser = wrestlingMatch.IsRedWon.HasValue && wrestlingMatch.IsRedWon.Value
                        ? wrestlingMatch.WrestlerInBlue
                        : wrestlingMatch.WrestlerInRed;

                    var match = thirdfPlaceRound.RoundMatches[0];
                    if (match.WrestlerInRed == null)
                    {
                        match.WrestlerInRed = matchLooser;
                    }
                    else if (match.WrestlerInBlue == null)
                    {
                        match.WrestlerInBlue = matchLooser;
                    }
                }
            }
        }

        protected override void ProceedToAdditionalBracket(WrestlingMatch wrestlingMatch)
        {
            // Semi-finals already formed, no need to do anything
            var semiFinalRound = Group.Bracket.Rounds.First(r => r.RoundType == GroupRoundTypeEnum.Additional);
            if (semiFinalRound.RoundMatches[0].WrestlerInRed != null &&
                semiFinalRound.RoundMatches[1].WrestlerInRed != null)
            {
                return;
            }

            // If this is the last match in last main round we are ready to calculate results and move athletes to additional bracket
            var notCompletedMainMatches = Group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main)
                .SelectMany(r => r.RoundMatches).Where(rm => !rm.IsMatchCompleted).ToList();

            if (notCompletedMainMatches.Count > 0) return;

            // Promote 2 best athletes to semi-final
            InitInternalProcessors();

            var resultsA = _groupAProcessor.GetResults().ToList();
            var resultsB = _groupBProcessor.GetResults().ToList();

            var groupAGold = resultsA[0].Wrestler;
            var groupASilver = resultsA[1].Wrestler;
            var groupBGold = resultsB[0].Wrestler;
            var groupBSilver = resultsB[1].Wrestler;

            var semiFinalMatch1 = semiFinalRound.RoundMatches[0];
            semiFinalMatch1.WrestlerInRed = groupAGold;
            semiFinalMatch1.WrestlerInBlue = groupBSilver;

            var semiFinalMatch2 = semiFinalRound.RoundMatches[1];
            semiFinalMatch2.WrestlerInRed = groupBGold;
            semiFinalMatch2.WrestlerInBlue = groupASilver;
        }

        protected override void RevertAdditionalBracket(WrestlingMatch wrestlingMatch)
        {
            var round = Group.Bracket.Rounds.First(r => r.RoundNumber == wrestlingMatch.RoundNumber);

            if (round.RoundType == GroupRoundTypeEnum.Additional)
            {
                if (!string.IsNullOrEmpty(wrestlingMatch.NextMatchBracketFullNumber))
                {
                    // If we are reverting semi-final, we need to clear final and 3rd place match
                    var finalRound = Group.Bracket.Rounds.First(r => r.RoundNumber == (wrestlingMatch.RoundNumber + 1));
                    var thirdPlaceRound = Group.Bracket.Rounds.First(r => r.RoundNumber == (wrestlingMatch.RoundNumber + 2));

                    if (wrestlingMatch.IsRedWon.HasValue && wrestlingMatch.IsRedWon.Value)
                    {
                        if (finalRound.RoundMatches[0].WrestlerInRed == wrestlingMatch.WrestlerInRed)
                        {
                            finalRound.RoundMatches[0].WrestlerInRed = null;
                        } else if (finalRound.RoundMatches[0].WrestlerInBlue == wrestlingMatch.WrestlerInRed)
                        {
                            finalRound.RoundMatches[0].WrestlerInBlue = null;
                        }

                        if (thirdPlaceRound.RoundMatches[0].WrestlerInRed == wrestlingMatch.WrestlerInBlue)
                        {
                            thirdPlaceRound.RoundMatches[0].WrestlerInRed = null;
                        } else if (thirdPlaceRound.RoundMatches[0].WrestlerInBlue == wrestlingMatch.WrestlerInBlue)
                        {
                            thirdPlaceRound.RoundMatches[0].WrestlerInBlue = null;
                        }
                    }
                    else
                    {
                        if (finalRound.RoundMatches[0].WrestlerInRed == wrestlingMatch.WrestlerInBlue)
                        {
                            finalRound.RoundMatches[0].WrestlerInRed = null;
                        }
                        else if (finalRound.RoundMatches[0].WrestlerInBlue == wrestlingMatch.WrestlerInBlue)
                        {
                            finalRound.RoundMatches[0].WrestlerInBlue = null;
                        }

                        if (thirdPlaceRound.RoundMatches[0].WrestlerInRed == wrestlingMatch.WrestlerInRed)
                        {
                            thirdPlaceRound.RoundMatches[0].WrestlerInRed = null;
                        }
                        else if (thirdPlaceRound.RoundMatches[0].WrestlerInBlue == wrestlingMatch.WrestlerInRed)
                        {
                            thirdPlaceRound.RoundMatches[0].WrestlerInBlue = null;
                        }
                    }
                }
            }
            else
            {
                // If all main bracket matches were completed we need to clear entire additional bracket
                if (Group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main)
                        .SelectMany(r => r.RoundMatches).Count(m => m.Status == MatchStatusEnum.Pending) == 0)
                {
                    foreach (var match in Group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).SelectMany(r => r.RoundMatches))
                    {
                        match.WrestlerInRed = null;
                        match.WrestlerInBlue = null;
                        match.Status = MatchStatusEnum.Pending;
                        match.LastSecondInMatch = 0;
                        match.PointsBlue = 0;
                        match.PointsRed = 0;
                        match.WarningsNumberBlue = 0;
                        match.WarningsNumberRed = 0;
                        match.StartDateTime = null;
                        match.IsRedWon = null;
                        match.WinType = null;
                    }
                }
            }
        }

        public override bool CanMatchBeReverted(WrestlingMatch wrestlingMatch)
        {
            var baseCheck = base.CanMatchBeReverted(wrestlingMatch);

            if (!baseCheck) return false;

            var round = Group.Bracket.Rounds.First(r => r.RoundNumber == wrestlingMatch.RoundNumber);

            if (round.RoundType == GroupRoundTypeEnum.Additional)
            {
                if (string.IsNullOrEmpty(wrestlingMatch.NextMatchBracketFullNumber))
                {
                    // Final or 3rd place match can be reverted any time
                    return true;
                }

                // Semi-finals can be reverted only of final and 3rd place matches are not completed
                var finalRound = Group.Bracket.Rounds.First(r => r.RoundNumber == (wrestlingMatch.RoundNumber + 1));
                var thirdPlaceRound = Group.Bracket.Rounds.First(r => r.RoundNumber == (wrestlingMatch.RoundNumber + 2));

                return finalRound.RoundMatches[0].Status == MatchStatusEnum.Pending && thirdPlaceRound.RoundMatches[0].Status == MatchStatusEnum.Pending;
            }

            // Main bracket match can be reverted only if no additional bracket matches completed
            return Group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional)
                       .SelectMany(r => r.RoundMatches).Count(m => m.Status == MatchStatusEnum.Completed) == 0;
        }
    }
}