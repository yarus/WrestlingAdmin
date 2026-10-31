using System;
using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities.Localization;
using Wrestling.Entities.Results;

namespace Wrestling.Entities.Bracket
{
    public class SubGroupsToOlympicBracketProcessor : GroupBracketProcessorBase
    {
        public override int? AthletesMinCount => 6;
        public override int? AthletesMaxCount => 8;

        private RoundRobinGroupBracketProcessor _groupAProcessor;
        private RoundRobinGroupBracketProcessor _groupBProcessor;

        private AgeWeightGroup _fakeGroupA;
        private AgeWeightGroup _fakeGroupB;
        private bool _resolvingPlayoffs;

        public override void Load(Tournament tournament, AgeWeightGroup group)
        {
            base.Load(tournament, group);
            ResolvePlayoffMatches();
        }

        public override void CompleteMatch(WrestlingMatch wrestlingMatch, bool? isRedWon, MatchWinTypeEnum winType)
        {
            base.CompleteMatch(wrestlingMatch, isRedWon, winType);
            ResolvePlayoffMatches();
        }

        private void ResolvePlayoffMatches()
        {
            // Automatic passes are derived from the source results, just like other
            // FreeWin propagation. Loading must not create new result versions.
            if (_resolvingPlayoffs || Group?.Bracket == null) return;
            var rounds = Group.Bracket.AdditionalRounds().ToList();
            if (rounds.Count != 3 || Group.Bracket.MainRounds().SelectMany(r => r.RoundMatches).Any(m => !m.IsMatchCompleted)) return;

            _resolvingPlayoffs = true;
            try
            {
                var semis = rounds[0].RoundMatches;
                if (semis.All(m => !m.IsMatchCompleted && m.WrestlerInRed == null && m.WrestlerInBlue == null))
                    ProceedToAdditionalBracket(Group.Bracket.MainRounds().Last().RoundMatches.Last());

                RestoreHistoricalExclusions(semis);
                foreach (var semi in semis) ReplaceExcludedSemifinalists(semi, semis);

                // Pool results are final: no new wrestler can fill an empty SF slot.
                foreach (var semi in semis) ResolveEmptyMatch(semi);

                // Medal slots must wait for BOTH semifinals, including automatic passes.
                if (semis.Any(m => !m.IsMatchCompleted)) return;
                SetMedalParticipants(rounds[1].RoundMatches[0], Winner(semis[0]), Winner(semis[1]));
                SetMedalParticipants(rounds[2].RoundMatches[0], Loser(semis[0]), Loser(semis[1]));
                ResolveEmptyMatch(rounds[1].RoundMatches[0]);
                ResolveEmptyMatch(rounds[2].RoundMatches[0]);
            }
            finally
            {
                _resolvingPlayoffs = false;
                // Keep counters consistent even when the last match has no winner.
                base.Load(Tournament, Group);
            }
        }

        private static Wrestler Winner(WrestlingMatch match) => !match.IsRedWon.HasValue ? null
            : match.IsRedWon.Value ? match.WrestlerInRed : match.WrestlerInBlue;

        private static Wrestler Loser(WrestlingMatch match)
        {
            var wrestler = !match.IsRedWon.HasValue ? null
                : match.IsRedWon.Value ? match.WrestlerInBlue : match.WrestlerInRed;
            return wrestler?.IsPlaceless == true ? null : wrestler;
        }

        private static void SetMedalParticipants(WrestlingMatch match, Wrestler red, Wrestler blue)
        {
            // A contested medal match remains authoritative. Its own sanctions must
            // not rewrite its participants when results are recalculated or loaded.
            if (match.IsMatchCompleted && match.WinType != MatchWinTypeEnum.FreeWin) return;
            if (match.WrestlerInRed?.ID == red?.ID && match.WrestlerInBlue?.ID == blue?.ID) return;
            if (match.IsMatchCompleted) ResetMatch(match);
            match.WrestlerInRed = red;
            match.WrestlerInBlue = blue;
        }

        private void ResolveEmptyMatch(WrestlingMatch match)
        {
            if (match.IsMatchCompleted || match.WrestlerInRed != null && match.WrestlerInBlue != null) return;
            if (match.WrestlerInRed == null && match.WrestlerInBlue == null)
            {
                // There is no winner to propagate; Completed/FreeWin/null is also
                // used by the consolation processor for a wholly empty branch.
                match.Status = MatchStatusEnum.Completed;
                match.WinType = MatchWinTypeEnum.FreeWin;
                match.IsRedWon = null;
            }
            else
            {
                base.CompleteMatch(match, match.WrestlerInRed != null, MatchWinTypeEnum.FreeWin);
            }
        }

        private static void ResetMatch(WrestlingMatch match)
        {
            match.Status = MatchStatusEnum.Pending;
            match.WinType = null;
            match.IsRedWon = null;
            match.PointsRed = match.PointsBlue = 0;
            match.WarningsNumberRed = match.WarningsNumberBlue = 0;
            match.LastSecondInMatch = 0;
            match.StartDateTime = null;
            match.BestActionRed = match.BestActionBlue = 0;
            match.BestActionRedCount = match.BestActionBlueCount = 0;
            match.IsLastActionRed = false;
            match.Note = null;
            match.MatchActions.Clear();
        }

        private static bool IsMutualExclusion(WrestlingMatch match) => match.IsMatchCompleted
            && (match.WinType == MatchWinTypeEnum.MutualDisqualify || match.WinType == MatchWinTypeEnum.MutualNoShow);

        private static void RestoreHistoricalExclusions(IEnumerable<WrestlingMatch> semis)
        {
            foreach (var result in semis.SelectMany(m => m.PreviousResults).Where(IsMutualExclusion))
                foreach (var wrestler in new[] { result.WrestlerInRed, result.WrestlerInBlue }.Where(w => w != null))
                {
                    if (result.WinType == MatchWinTypeEnum.MutualDisqualify) wrestler.IsDisqualified = true;
                    else if (!wrestler.IsDisqualified) wrestler.IsNoShow = true;
                }
        }

        private void ReplaceExcludedSemifinalists(WrestlingMatch semi, List<WrestlingMatch> semis)
        {
            if (!IsMutualExclusion(semi)) return;
            if (Group.Bracket.AdditionalRounds().Skip(1).SelectMany(r => r.RoundMatches)
                .Any(m => m.IsMatchCompleted && m.WinType != MatchWinTypeEnum.FreeWin)) return;
            InitInternalProcessors();
            var other = semis.First(m => m != semi);
            var reserved = new HashSet<Guid>(new[] { other.WrestlerInRed, other.WrestlerInBlue }
                .Where(w => w != null).Select(w => w.ID));
            var resultsA = _groupAProcessor.GetResults().Where(r => !r.Wrestler.IsPlaceless && !reserved.Contains(r.Wrestler.ID)).ToList();
            var resultsB = _groupBProcessor.GetResults().Where(r => !r.Wrestler.IsPlaceless && !reserved.Contains(r.Wrestler.ID)).ToList();
            var nextA = resultsA.FirstOrDefault()?.Wrestler;
            var nextB = resultsB.FirstOrDefault()?.Wrestler;

            RevertAdditionalBracket(semi);
            semi.PreviousResults = semi.PreviousResults.Concat(new[] { semi.CaptureResult() }).ToList();
            ResetMatch(semi);
            var upper = semis.IndexOf(semi) == 0;
            semi.WrestlerInRed = upper ? nextA : nextB;
            semi.WrestlerInBlue = upper ? nextB : nextA;

            // A different pairing is a structural update. Send the whole bracket
            // atomically, including the preserved exclusion and its wrestler flags.
            Group.BracketVersion++;
        }

        private bool IsWaitingReplacement(WrestlingMatch match) => GetSemiFinalRound(Group)?.RoundMatches.Contains(match) == true
            && match.PreviousResults.Count > 0
            && (!match.IsMatchCompleted || match.WinType == MatchWinTypeEnum.FreeWin);

        public override void RevertMatch(WrestlingMatch wrestlingMatch)
        {
            if (!IsWaitingReplacement(wrestlingMatch))
            {
                base.RevertMatch(wrestlingMatch);
                return;
            }
            if (!CanMatchBeReverted(wrestlingMatch)) throw new InvalidOperationException("Revert the dependent medal matches first.");
            RevertAdditionalBracket(wrestlingMatch);
            var previous = wrestlingMatch.PreviousResults.Last();
            wrestlingMatch.PreviousResults = wrestlingMatch.PreviousResults.Take(wrestlingMatch.PreviousResults.Count - 1).ToList();
            wrestlingMatch.RestoreResult(previous);
            base.RevertMatch(wrestlingMatch);
            wrestlingMatch.Version++;
            Group.BracketVersion++;
        }

        public bool CanClearReplacementDisqualification(Wrestler wrestler)
        {
            var match = Group?.Bracket?.Rounds.SelectMany(r => r.RoundMatches).FirstOrDefault(m =>
                m.WinType == MatchWinTypeEnum.MutualDisqualify && (m.WrestlerInRed.SameAs(wrestler) || m.WrestlerInBlue.SameAs(wrestler))
                || m.PreviousResults.Any(r => r.WinType == MatchWinTypeEnum.MutualDisqualify
                    && (r.WrestlerInRed.SameAs(wrestler) || r.WrestlerInBlue.SameAs(wrestler))));
            return match == null || CanMatchBeReverted(match);
        }

        public override void ClearWrestlerDisqualify(Wrestler wrestler)
        {
            var match = GetSemiFinalRound(Group)?.RoundMatches.FirstOrDefault(m => m.PreviousResults.Any(r =>
                r.WinType == MatchWinTypeEnum.MutualDisqualify && (r.WrestlerInRed.SameAs(wrestler) || r.WrestlerInBlue.SameAs(wrestler))));
            if (match == null)
            {
                base.ClearWrestlerDisqualify(wrestler);
                return;
            }
            while (match.PreviousResults.Any(r => r.WrestlerInRed.SameAs(wrestler) || r.WrestlerInBlue.SameAs(wrestler)))
            {
                if (!CanMatchBeReverted(match)) throw new InvalidOperationException("Revert the dependent medal matches first.");
                if (!IsWaitingReplacement(match))
                {
                    base.RevertMatch(match);
                    match.Version++;
                }
                else RevertMatch(match);
            }
        }

        public override string Title => EntityLocalization.T("BracketType_SubGroupsToOlympic", "2 подгруппы в Олимпийскую с 3м местом");
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

                foreach (var round in Group.Bracket.MainRounds())
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
                    RoundName = "Круг " + (i + 1),
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
                MatID = Group.MatID,
                MatLabel = Group.MatLabel,
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

            final.RoundMatches.Add(upperMatch);

            Group.Bracket.Rounds.Add(final);
        }

        private void Generate3rdPlaceMatch()
        {
            var thirdPlace = new GroupRound
            {
                // Hyphenated spelling matches OlympicGroupBracketProcessor —
                // single source of truth for the "3rd place" round label so
                // RoundNameLocalizationConverter only has one exact-match key
                // to resolve (Round_ThirdPlace).
                RoundName = "3-е место",
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

            var lowerMatch = GenerateGroupMatch(semiFinals.RoundNumber, semiFinals.RoundName, null, null, 2, true);

            semiFinals.RoundMatches.Add(upperMatch);
            semiFinals.RoundMatches.Add(lowerMatch);

            Group.Bracket.Rounds.Add(semiFinals);
        }

        protected override void CalculateResults()
        {
            if (Group.Bracket == null) return;

            var notCompletedMainMatches = Group.Bracket.MainRounds().SelectMany(p => p.RoundMatches).Where(x => x.Status != MatchStatusEnum.Completed).ToList();
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

            var addRounds = Group.Bracket.AdditionalRounds().ToList();
            var final = addRounds[addRounds.Count - 2].RoundMatches[0];

            int currentPlace = 1;

            var finalists = new List<Wrestler>();

            var thirdPlace = addRounds[addRounds.Count - 1].RoundMatches[0];

            // UWW: mutual DSQ in the final leaves both finalists DSQ'd; the
            // bronze match then decides 1-2 and everyone else shifts up by 2.
            // Single DSQ in the final: winner stays gold, bronze winner → 2,
            // bronze loser → 3, everyone else shifts up by 1.
            // (Sibling rule of OlympicConsilationFinalists, adapted for the
            // single-bronze SubGroupsToOlympic format.)
            var finalMutualExclusion = final.Status == MatchStatusEnum.Completed
                && (final.WinType == MatchWinTypeEnum.MutualDisqualify || final.WinType == MatchWinTypeEnum.MutualNoShow);
            var bronzeHasWinner = thirdPlace.Status == MatchStatusEnum.Completed
                                  && thirdPlace.IsRedWon.HasValue;
            Wrestler dsqFinalist = null;
            if (final.Status == MatchStatusEnum.Completed && final.IsRedWon.HasValue
                && (final.WinType == MatchWinTypeEnum.DisqualifyWin || final.WinType == MatchWinTypeEnum.NoShow
                    || final.WinType == MatchWinTypeEnum.WarningsLimit))
            {
                dsqFinalist = final.IsRedWon.Value ? final.WrestlerInBlue : final.WrestlerInRed;
                if (dsqFinalist == null || !dsqFinalist.IsPlaceless) dsqFinalist = null;
            }
            var finalSingleDsq = dsqFinalist != null;

            if (finalMutualExclusion && bronzeHasWinner)
            {
                var promoted1st = thirdPlace.IsRedWon.Value ? thirdPlace.WrestlerInRed : thirdPlace.WrestlerInBlue;
                if (promoted1st != null && !promoted1st.IsPlaceless)
                {
                    finalists.Add(promoted1st);
                    promoted1st.FinalPlace = currentPlace;
                    currentPlace++;
                }

                var promoted2nd = thirdPlace.IsRedWon.Value ? thirdPlace.WrestlerInBlue : thirdPlace.WrestlerInRed;
                if (promoted2nd != null && !promoted2nd.IsPlaceless)
                {
                    finalists.Add(promoted2nd);
                    promoted2nd.FinalPlace = currentPlace;
                    currentPlace++;
                }
            }
            else if (finalSingleDsq && bronzeHasWinner)
            {
                var gold = final.IsRedWon.Value ? final.WrestlerInRed : final.WrestlerInBlue;
                if (gold != null && !gold.IsPlaceless)
                {
                    finalists.Add(gold);
                    gold.FinalPlace = currentPlace;
                    currentPlace++;
                }

                var silver = thirdPlace.IsRedWon.Value ? thirdPlace.WrestlerInRed : thirdPlace.WrestlerInBlue;
                if (silver != null && !silver.IsPlaceless)
                {
                    finalists.Add(silver);
                    silver.FinalPlace = currentPlace;
                    currentPlace++;
                }

                var bronze = thirdPlace.IsRedWon.Value ? thirdPlace.WrestlerInBlue : thirdPlace.WrestlerInRed;
                if (bronze != null && !bronze.IsPlaceless)
                {
                    finalists.Add(bronze);
                    bronze.FinalPlace = currentPlace;
                    currentPlace++;
                }
            }
            else
            {
                // Standard 1-2 from final
                if (AssignSharedInjuryPlace(final, currentPlace))
                {
                    finalists.AddRange(new[] { final.WrestlerInRed, final.WrestlerInBlue }
                        .Where(w => w != null && !w.IsPlaceless));
                    currentPlace += 2;
                }
                else if (final.Status == MatchStatusEnum.Completed && final.IsRedWon.HasValue)
                {
                    var winner = final.IsRedWon.Value ? final.WrestlerInRed : final.WrestlerInBlue;
                    if (winner != null && !winner.IsPlaceless)
                    {
                        finalists.Add(winner);
                        winner.FinalPlace = currentPlace;
                        currentPlace++;
                    }

                    var looser = final.IsRedWon.Value ? final.WrestlerInBlue : final.WrestlerInRed;
                    if (looser != null && !looser.IsPlaceless)
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

                // Standard 3-4 from bronze
                if (AssignSharedInjuryPlace(thirdPlace, currentPlace))
                {
                    finalists.AddRange(new[] { thirdPlace.WrestlerInRed, thirdPlace.WrestlerInBlue }
                        .Where(w => w != null && !w.IsPlaceless));
                    currentPlace += 2;
                }
                else if (bronzeHasWinner)
                {
                    var bronzeWinner = thirdPlace.IsRedWon.Value ? thirdPlace.WrestlerInRed : thirdPlace.WrestlerInBlue;
                    if (bronzeWinner != null && !bronzeWinner.IsPlaceless)
                    {
                        finalists.Add(bronzeWinner);
                        bronzeWinner.FinalPlace = currentPlace;
                        currentPlace++;
                    }

                    var bronzeLooser = thirdPlace.IsRedWon.Value ? thirdPlace.WrestlerInBlue : thirdPlace.WrestlerInRed;
                    if (bronzeLooser != null && !bronzeLooser.IsPlaceless)
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
            }

            // Pool standings are not final category places while medal matches
            // are pending (including a replacement semifinal still to be played).
            if (!Group.IsBracketCompleted) return;
            currentPlace = Math.Min(currentPlace, finalists.Count + 1);

            // Other places should be set based on main round robin results
            var matches = Group.Bracket.MainRounds().SelectMany(p => p.RoundMatches).Where(x => x.Status == MatchStatusEnum.Completed).ToList();
            if (matches.Count == 0)
            {
                return;
            }

            if (_groupAProcessor == null || _groupBProcessor == null)
            {
                return;
            }

            var comparedResults = new List<TournamentResult>();
            comparedResults.AddRange(resultsA.Where(r => !finalists.Contains(r.Wrestler) && !r.Wrestler.IsPlaceless).Distinct());
            comparedResults.AddRange(resultsB.Where(r => !finalists.Contains(r.Wrestler) && !r.Wrestler.IsPlaceless).Distinct());

            var finalOrder = comparedResults
                .OrderByDescending(x => x.Wins)
                .ThenByDescending(x => x.OverallTournamentClassificationPoints)
                .ThenByDescending(x => x.WinsByTushe)
                .ThenByDescending(x => x.WinsByDomination)
                .ThenByDescending(x => x.WinsByDominationWithPoints)
                .ThenByDescending(x => x.AllGainedPoints)
                .ThenBy(x => x.AllLostPoints)
                .ThenBy(x => x.Wrestler.SeedNumber)
                .ToList();

            foreach (var tournamentResult in finalOrder)
            {
                tournamentResult.Wrestler.FinalPlace = currentPlace;
                currentPlace++;
            }
        }

        protected override void ProceedToNextMatch(WrestlingMatch wrestlingMatch)
        {
            var semis = GetSemiFinalRound(Group);
            if (semis?.RoundMatches.Contains(wrestlingMatch) != true)
            {
                base.ProceedToNextMatch(wrestlingMatch);
                return;
            }

            // Stable source slots avoid moving a late semifinal winner to a slot
            // vacated by a mutual outcome in the other semifinal.
            int index = semis.RoundMatches.IndexOf(wrestlingMatch);
            var final = GetFinalRound(Group).RoundMatches[0];
            var bronze = Get3rdPlaceRound(Group).RoundMatches[0];
            if (index == 0)
            {
                if (!final.IsMatchCompleted) final.WrestlerInRed = Winner(wrestlingMatch);
                if (!bronze.IsMatchCompleted) bronze.WrestlerInRed = Loser(wrestlingMatch);
            }
            else
            {
                if (!final.IsMatchCompleted) final.WrestlerInBlue = Winner(wrestlingMatch);
                if (!bronze.IsMatchCompleted) bronze.WrestlerInBlue = Loser(wrestlingMatch);
            }
        }

        protected override void ProceedToAdditionalBracket(WrestlingMatch wrestlingMatch)
        {
            // Semi-finals already formed, no need to do anything
            var semiFinalRound = Group.Bracket.AdditionalRounds().First();
            if (semiFinalRound.RoundMatches.Any(m => m.IsMatchCompleted || m.WrestlerInRed != null || m.WrestlerInBlue != null))
            {
                return;
            }

            // If this is the last match in last main round we are ready to calculate results and move athletes to additional bracket
            var notCompletedMainMatches = Group.Bracket.MainRounds()
                .SelectMany(r => r.RoundMatches).Where(rm => !rm.IsMatchCompleted).ToList();

            if (notCompletedMainMatches.Count > 0) return;

            // Promote 2 best athletes to semi-final
            InitInternalProcessors();

            // Filter DSQ wrestlers explicitly. RoundRobin's GetStats() orders
            // results by nullable FinalPlace ascending, which puts null (=DSQ)
            // FIRST — so [0]/[1] would be the disqualified pair if a mutual DSQ
            // happened in a subgroup. Mutual DSQ in any subgroup match cascades
            // auto-wins to the third wrestler, who legitimately becomes that
            // subgroup's gold; silver simply doesn't exist.
            var resultsA = _groupAProcessor.GetResults().Where(r => !r.Wrestler.IsPlaceless).ToList();
            var resultsB = _groupBProcessor.GetResults().Where(r => !r.Wrestler.IsPlaceless).ToList();

            var groupAGold = resultsA.Count > 0 ? resultsA[0].Wrestler : null;
            var groupASilver = resultsA.Count > 1 ? resultsA[1].Wrestler : null;
            var groupBGold = resultsB.Count > 0 ? resultsB[0].Wrestler : null;
            var groupBSilver = resultsB.Count > 1 ? resultsB[1].Wrestler : null;

            var semiFinalMatch1 = semiFinalRound.RoundMatches[0];
            semiFinalMatch1.WrestlerInRed = groupAGold;
            semiFinalMatch1.WrestlerInBlue = groupBSilver;

            var semiFinalMatch2 = semiFinalRound.RoundMatches[1];
            semiFinalMatch2.WrestlerInRed = groupBGold;
            semiFinalMatch2.WrestlerInBlue = groupASilver;

            ResolvePlayoffMatches();
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

                    foreach (var match in finalRound.RoundMatches.Concat(thirdPlaceRound.RoundMatches))
                        if (match.IsMatchCompleted && match.WinType == MatchWinTypeEnum.FreeWin) ResetMatch(match);

                    if (!wrestlingMatch.IsRedWon.HasValue)
                    {
                        // Mutual outcomes populated neither target slot.
                        return;
                    }

                    if (wrestlingMatch.IsRedWon.HasValue && wrestlingMatch.IsRedWon.Value)
                    {
                        if (finalRound.RoundMatches[0].WrestlerInRed.SameAs(wrestlingMatch.WrestlerInRed))
                        {
                            finalRound.RoundMatches[0].WrestlerInRed = null;
                        }
                        else if (finalRound.RoundMatches[0].WrestlerInBlue.SameAs(wrestlingMatch.WrestlerInRed))
                        {
                            finalRound.RoundMatches[0].WrestlerInBlue = null;
                        }

                        if (thirdPlaceRound.RoundMatches[0].WrestlerInRed.SameAs(wrestlingMatch.WrestlerInBlue))
                        {
                            thirdPlaceRound.RoundMatches[0].WrestlerInRed = null;
                        }
                        else if (thirdPlaceRound.RoundMatches[0].WrestlerInBlue.SameAs(wrestlingMatch.WrestlerInBlue))
                        {
                            thirdPlaceRound.RoundMatches[0].WrestlerInBlue = null;
                        }
                    }
                    else
                    {
                        if (finalRound.RoundMatches[0].WrestlerInRed.SameAs(wrestlingMatch.WrestlerInBlue))
                        {
                            finalRound.RoundMatches[0].WrestlerInRed = null;
                        }
                        else if (finalRound.RoundMatches[0].WrestlerInBlue.SameAs(wrestlingMatch.WrestlerInBlue))
                        {
                            finalRound.RoundMatches[0].WrestlerInBlue = null;
                        }

                        if (thirdPlaceRound.RoundMatches[0].WrestlerInRed.SameAs(wrestlingMatch.WrestlerInRed))
                        {
                            thirdPlaceRound.RoundMatches[0].WrestlerInRed = null;
                        }
                        else if (thirdPlaceRound.RoundMatches[0].WrestlerInBlue.SameAs(wrestlingMatch.WrestlerInRed))
                        {
                            thirdPlaceRound.RoundMatches[0].WrestlerInBlue = null;
                        }
                    }
                }
            }
            else
            {
                // If all main bracket matches were completed we need to clear entire additional bracket
                if (Group.Bracket.MainRounds()
                        .SelectMany(r => r.RoundMatches).Count(m => m.Status == MatchStatusEnum.Pending) == 0)
                {
                    foreach (var match in Group.Bracket.AdditionalRounds().SelectMany(r => r.RoundMatches))
                    {
                        ResetMatch(match);
                        match.WrestlerInRed = null;
                        match.WrestlerInBlue = null;
                    }
                }
            }
        }

        public override bool CanMatchBeReverted(WrestlingMatch wrestlingMatch)
        {
            if (!IsWaitingReplacement(wrestlingMatch) && (wrestlingMatch.Status == MatchStatusEnum.Pending || !wrestlingMatch.WinType.HasValue
                || wrestlingMatch.RoundNumber == 1 && wrestlingMatch.WinType == MatchWinTypeEnum.FreeWin)) return false;

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

                return finalRound.RoundMatches.Concat(thirdPlaceRound.RoundMatches)
                    .All(m => !m.IsMatchCompleted || m.WinType == MatchWinTypeEnum.FreeWin);
            }

            // Main bracket match can be reverted only if no additional bracket matches completed
            return Group.Bracket.AdditionalRounds().SelectMany(r => r.RoundMatches)
                .All(m => m.PreviousResults.Count == 0 && (!m.IsMatchCompleted || m.WinType == MatchWinTypeEnum.FreeWin));
        }

        public override List<GroupRound> GetMainQualificationRounds(AgeWeightGroup group)
        {
            if (group?.Bracket?.Rounds == null) return null;

            return group.Bracket.MainRounds().ToList();
        }

        public override GroupRound GetSemiFinalRound(AgeWeightGroup group)
        {
            if (group?.Bracket?.Rounds == null || group.Bracket.Rounds.Count == 0) return null;

            var additionalRounds = group.Bracket.AdditionalRounds().ToList();

            if (additionalRounds.Count == 0) return null;

            return additionalRounds[0];
        }

        public override GroupRound Get3rdPlaceRound(AgeWeightGroup group)
        {
            if (group?.Bracket?.Rounds == null || group.Bracket.Rounds.Count == 0) return null;

            var additionalRounds = group.Bracket.AdditionalRounds().ToList();

            if (additionalRounds.Count == 0) return null;

            return additionalRounds[additionalRounds.Count - 1];
        }

        public override GroupRound GetFinalRound(AgeWeightGroup group)
        {
            if (group?.Bracket?.Rounds == null) return null;

            var additionalRounds = group.Bracket.AdditionalRounds().ToList();

            if (additionalRounds.Count < 2) return null;

            return additionalRounds[additionalRounds.Count - 2];
        }
    }
}
