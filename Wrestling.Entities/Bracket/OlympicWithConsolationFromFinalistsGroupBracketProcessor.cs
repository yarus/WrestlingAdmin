using System;
using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Bracket
{
    public class OlympicWithConsolationFromFinalistsGroupBracketProcessor : OlympicGroupBracketProcessor
    {
        public override string Title => "Олимпийская с утешением от финалистов";
        public override string Code => BracketTypeEnum.OlympicConsilationFinalists.ToString();

        // Per UWW: when both finalists are mutually DSQ'd, the two bronze
        // medalists fight for places 1-2; the bronze losers move up from 5th
        // to 3rd, and everybody else shifts up by two. Implementation: instead
        // of growing a separate playoff round we *reuse the existing final
        // match* — once both bronzes have completed, clear the final's score
        // and replace its wrestlers with the two bronze winners. From then on
        // the final is a regular pending match that the operator plays as
        // usual; the original DSQ'd finalists keep IsDisqualified=true (set
        // by base.CompleteMatch) which excludes them from FinalPlace.
        public override void CompleteMatch(WrestlingMatch wrestlingMatch, bool? isRedWon, MatchWinTypeEnum winType)
        {
            base.CompleteMatch(wrestlingMatch, isRedWon, winType);
            TryRebuildFinalAfterMutualDsq();
        }

        // Lazy migration for files saved before the rebuild rule was wired up:
        // those files persist a final with WinType=MutualDisqualify alongside
        // two completed bronze matches, but the final never got reset and
        // promoted bronze winners. On Load we detect that state and fire the
        // rebuild — idempotent (no-op once final.WinType is null again, which
        // happens immediately after the rebuild resets the final).
        public override void Load(Tournament tournament, AgeWeightGroup group)
        {
            base.Load(tournament, group);
            TryRebuildFinalAfterMutualDsq();
        }

        // Triggers from CompleteMatch on either side of the order:
        //   final completes first (mutual DSQ) → no-op until both bronzes done
        //   bronze completes first → no-op until the final's mutual DSQ result
        // The dual-trigger is what lets us be order-agnostic without explicit
        // event wiring.
        //
        // Idempotency: once we rebuild, finalMatch.WinType is null (we cleared
        // it). The mutual-DSQ guard at the top short-circuits subsequent calls,
        // so the rebuild fires exactly once.
        private void TryRebuildFinalAfterMutualDsq()
        {
            if (Group?.Bracket == null) return;

            var mainRounds = Group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
            if (mainRounds.Count == 0) return;
            var finalMatch = mainRounds[mainRounds.Count - 1].RoundMatches.FirstOrDefault();
            if (finalMatch == null || finalMatch.WinType != MatchWinTypeEnum.MutualDisqualify) return;

            var addRounds = Group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
            if (addRounds.Count == 0) return;
            var bronzeRound = addRounds[addRounds.Count - 1];
            if (bronzeRound.RoundMatches.Count != 2) return;

            // Both bronze matches must have a clear winner. If a bronze is
            // itself mutual DSQ neither side has a candidate — operator has
            // to resolve that manually before the rebuild can fire.
            if (bronzeRound.RoundMatches.Any(m => !m.IsMatchCompleted || !m.IsRedWon.HasValue)) return;

            var bronze1 = bronzeRound.RoundMatches[0];
            var bronze2 = bronzeRound.RoundMatches[1];
            var bronze1Winner = bronze1.IsRedWon.Value ? bronze1.WrestlerInRed : bronze1.WrestlerInBlue;
            var bronze2Winner = bronze2.IsRedWon.Value ? bronze2.WrestlerInRed : bronze2.WrestlerInBlue;
            if (bronze1Winner == null || bronze2Winner == null) return;

            // Reset the final and replace wrestlers with the two bronze winners.
            // Original finalists stay in Group.Wrestlers with IsDisqualified=true.
            finalMatch.WrestlerInRed = bronze1Winner;
            finalMatch.WrestlerInBlue = bronze2Winner;
            finalMatch.Status = MatchStatusEnum.Pending;
            finalMatch.WinType = null;
            finalMatch.IsRedWon = null;
            finalMatch.PointsRed = 0;
            finalMatch.PointsBlue = 0;
            finalMatch.WarningsNumberRed = 0;
            finalMatch.WarningsNumberBlue = 0;
            finalMatch.LastSecondInMatch = 0;
            finalMatch.StartDateTime = null;
            finalMatch.MatchActions = new List<MatchAction>();
        }

        // True when the final's current red/blue are the two bronze MATCH
        // winners — that's only possible after a rebuild because in normal
        // flow finalists are SF winners and bronze winners are SF losers /
        // earlier-round losers. Used both to switch CalculateResults logic
        // and to guard revert paths.
        private bool IsFinalRebuiltAfterMutualDsq(GroupBracket bracket)
        {
            var mainRounds = bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
            if (mainRounds.Count == 0) return false;
            var finalMatch = mainRounds[mainRounds.Count - 1].RoundMatches.FirstOrDefault();
            if (finalMatch?.WrestlerInRed == null || finalMatch.WrestlerInBlue == null) return false;

            var addRounds = bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
            if (addRounds.Count == 0) return false;
            var bronzeRound = addRounds[addRounds.Count - 1];
            if (bronzeRound.RoundMatches.Count != 2) return false;

            foreach (var bronze in bronzeRound.RoundMatches)
            {
                if (!bronze.IsMatchCompleted || !bronze.IsRedWon.HasValue) return false;
                var winner = bronze.IsRedWon.Value ? bronze.WrestlerInRed : bronze.WrestlerInBlue;
                if (winner == null) return false;
                if (!winner.SameAs(finalMatch.WrestlerInRed) && !winner.SameAs(finalMatch.WrestlerInBlue)) return false;
            }
            return true;
        }

        private bool IsBronzeMatch(WrestlingMatch wrestlingMatch)
        {
            var addRounds = Group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Additional).ToList();
            if (addRounds.Count == 0) return false;
            var bronzeRound = addRounds[addRounds.Count - 1];
            return bronzeRound.RoundMatches.Contains(wrestlingMatch);
        }

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
                    RoundName = i < additionalRoundsCount ? "Утешение Круг " + i : "3-е место",
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

            // Mutual DSQ in semifinal (M2): consolation rebuild is manual.
            if (!wrestlingMatch.IsRedWon.HasValue) return;

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
                    && ((o.IsRedWinner && o.WrestlerInRed.SameAs(winner)) || (o.IsBlueWon && o.WrestlerInBlue.SameAs(winner))))
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
            // Capture rebuild state up-front: UnRebuildFinal mutates the final
            // and would falsify IsFinalRebuiltAfterMutualDsq mid-method.
            var bronzeRevertInRebuiltScenario =
                IsBronzeMatch(wrestlingMatch) && IsFinalRebuiltAfterMutualDsq(Group.Bracket);

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

            // Bronze revert in the rebuild scenario: undo the final rebuild
            // by restoring the original DSQ'd finalists (SF winners) and
            // re-marking the final as mutual DSQ. When the operator re-plays
            // the bronze, TryRebuildFinalAfterMutualDsq fires again and the
            // new bronze winner takes the finalist slot — exactly what the
            // operator expects ("другой спортсмен попадет в финал вместо
            // дисквалифицированного").
            if (bronzeRevertInRebuiltScenario)
            {
                UnRebuildFinal();
            }
        }

        // Reverse of TryRebuildFinalAfterMutualDsq: puts the final back into
        // the «mutual DSQ Completed» state with the SF winners (= original
        // DSQ'd finalists) restored. IsDisqualified flags on those wrestlers
        // were set at the original mutual-DSQ completion and remain true
        // through the un-rebuild (they are still disqualified).
        private void UnRebuildFinal()
        {
            var mainRounds = Group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
            if (mainRounds.Count < 2) return;

            var sfRound = mainRounds[mainRounds.Count - 2];
            if (sfRound.RoundMatches.Count != 2) return;
            var sf1 = sfRound.RoundMatches[0];
            var sf2 = sfRound.RoundMatches[1];
            if (!sf1.IsMatchCompleted || !sf1.IsRedWon.HasValue) return;
            if (!sf2.IsMatchCompleted || !sf2.IsRedWon.HasValue) return;

            var origRed = sf1.IsRedWon.Value ? sf1.WrestlerInRed : sf1.WrestlerInBlue;
            var origBlue = sf2.IsRedWon.Value ? sf2.WrestlerInRed : sf2.WrestlerInBlue;

            var finalMatch = mainRounds[mainRounds.Count - 1].RoundMatches.FirstOrDefault();
            if (finalMatch == null) return;

            finalMatch.WrestlerInRed = origRed;
            finalMatch.WrestlerInBlue = origBlue;
            finalMatch.Status = MatchStatusEnum.Completed;
            finalMatch.WinType = MatchWinTypeEnum.MutualDisqualify;
            finalMatch.IsRedWon = null;
            finalMatch.PointsRed = 0;
            finalMatch.PointsBlue = 0;
            finalMatch.WarningsNumberRed = 0;
            finalMatch.WarningsNumberBlue = 0;
            finalMatch.LastSecondInMatch = 0;
            finalMatch.StartDateTime = null;
            finalMatch.MatchActions = new List<MatchAction>();
        }

        public override bool CanMatchBeReverted(WrestlingMatch wrestlingMatch)
        {
            if (wrestlingMatch.Status == MatchStatusEnum.Pending || !wrestlingMatch.WinType.HasValue || (wrestlingMatch.RoundNumber == 1 && wrestlingMatch.WinType.Value == MatchWinTypeEnum.FreeWin)) return false;

            // Bronze revert in the post-mutual-DSQ rebuild: allowed while
            // the rebuilt final is still Pending (RevertAdditionalBracket
            // un-rebuilds, replay re-fires the rebuild). Blocked once the
            // rebuilt final has been replayed (Completed) — operator must
            // revert the final first to free the bronze.
            if (IsBronzeMatch(wrestlingMatch) && IsFinalRebuiltAfterMutualDsq(Group.Bracket))
            {
                var rebuiltMain = Group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();
                var rebuiltFinal = rebuiltMain.LastOrDefault()?.RoundMatches.FirstOrDefault();
                if (rebuiltFinal != null && rebuiltFinal.Status == MatchStatusEnum.Completed) return false;
                return true;
            }

            var baseCheck = true;

            if (!string.IsNullOrEmpty(wrestlingMatch.NextMatchBracketFullNumber))
            {
                var nextMatch = Group.Bracket.Rounds.SelectMany(p => p.RoundMatches).FirstOrDefault(x => x.BracketFullNumber == wrestlingMatch.NextMatchBracketFullNumber);
                if (nextMatch == null) throw new BracketStateException($"Next match '{wrestlingMatch.NextMatchBracketFullNumber}' referenced by match '{wrestlingMatch.BracketFullNumber}' does not exist in the bracket.");

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
            // Mutual DSQ: neither wrestler gets a rank — IsDisqualified flag drives UI.
            if (!match.IsRedWon.HasValue) return;

            if (match.WrestlerInRed != null && !match.WrestlerInRed.IsDisqualified) match.WrestlerInRed.FinalPlace = match.IsRedWon.Value ? winnerPlance : looserPlace;
            if (match.WrestlerInBlue != null && !match.WrestlerInBlue.IsDisqualified) match.WrestlerInBlue.FinalPlace = match.IsBlueWon ? winnerPlance : looserPlace;
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

        // Rebuild case: bronze winners moved to the final (where they get
        // 1st/2nd from DefineGoldAndSilver). Bronze losers — the wrestlers
        // who lost to the now-promoted finalists — take the two 3rd places.
        // No 5th from this round; everyone below shifts up via startPlace=5
        // in CalculateResults.
        private void DefineThirdPlaceFromBronzeLosers(GroupBracket bracket)
        {
            var addRounds = bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Additional).OrderBy(x => x.RoundNumber).ToList();
            if (addRounds.Count == 0) return;
            var bronzeRound = addRounds[addRounds.Count - 1];

            foreach (var bronze in bronzeRound.RoundMatches)
            {
                if (!bronze.IsMatchCompleted || !bronze.IsRedWon.HasValue) continue;
                var loser = bronze.IsRedWon.Value ? bronze.WrestlerInBlue : bronze.WrestlerInRed;
                if (loser != null && !loser.IsDisqualified) loser.FinalPlace = 3;
            }
        }

        protected override void CalculateResults()
        {
            if (Group.Bracket == null) return;

            var isRebuilt = IsFinalRebuiltAfterMutualDsq(Group.Bracket);

            // Set 1-2 places from final wrestlingMatch (works the same in
            // both flows — in the rebuild case the final has bronze winners
            // and a regular winner/loser).
            DefineGoldAndSilver(Group.Bracket);

            if (isRebuilt)
            {
                DefineThirdPlaceFromBronzeLosers(Group.Bracket);
            }
            else
            {
                // Get Additional bracket finals and define two 3rd places and two 5th places
                DefineBronzeAndFifth(Group.Bracket);
            }

            if (!Group.IsBracketCompleted) return;

            // Define remaining places by qualification points. After a rebuild
            // the bronze losers took 3 (not 5), so the next wrestler down is
            // 5th — the «остальные участники поднимутся» rule from UWW. DSQ'd
            // wrestlers (the original finalists in the rebuild case, or any
            // mutual-DSQ casualties from earlier rounds) stay placeless.
            var startPlace = isRebuilt ? 5 : 7;

            var statistics = GetStats().Where(x => !x.Wrestler.FinalPlace.HasValue && !x.Wrestler.IsDisqualified)
                .OrderByDescending(x => x.OverallTournamentClassificationPoints)
                .ThenByDescending(x => x.WinsByTushe)
                .ThenByDescending(x => x.WinsByDomination)
                .ThenByDescending(x => x.WinsByDominationWithPoints)
                .ThenByDescending(x => x.AllGainedPoints)
                .ThenBy(x => x.AllLostPoints)
                .ThenBy(x => x.Wrestler.SeedNumber)
                .ToList();

            var currentPlace = startPlace;
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
