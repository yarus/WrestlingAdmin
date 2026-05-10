using System;
using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities.Results;

namespace Wrestling.Entities.Bracket
{
    public abstract class GroupBracketProcessorBase : IGroupBracketProcessor
    {
        protected Tournament Tournament { get; set; }
        public abstract string Title { get; }
        public abstract string Code { get; }
        public virtual int? AthletesMinCount => 2;
        public virtual int? AthletesMaxCount => null;

        protected AgeWeightGroup Group { get; set; }

        public void LoadTournamentGroup(Tournament tournament, AgeWeightGroup group)
        {
            Tournament = tournament;
            Group = group;
        }

        public void Generate(Tournament tournament, AgeWeightGroup group)
        {
            if (tournament == null || group == null || group.Wrestlers.Count < 2) return;

            LoadTournamentGroup(tournament, group);

            Group.Bracket = new GroupBracket
            {
                BracketTypeLabel = Title,
                BracketTypeCode = Code,
                WrestlersCount = group.Wrestlers.Count
            };

            GenerateRounds();

            // Bump the per-group BracketVersion so peers detect the bracket
            // rebuild during import. This is the ONLY place BracketVersion is
            // bumped — field edits (timing, name, CarpetID) don't affect
            // bracket shape and use FieldsVersion instead. Since Generate()
            // builds from the current Wrestlers list, the new bracket and the
            // membership it implies travel together as one atomic unit.
            Group.BracketVersion++;
        }

        protected abstract void GenerateMainRounds();
        protected abstract void GenerateAdditionalRounds();

        protected virtual void GenerateRounds()
        {
            GenerateMainRounds();
            GenerateAdditionalRounds();
            SetMatchesCount();
        }

        private void SetMatchesCount()
        {
            if (Group.Bracket != null && Group.Bracket.Rounds.Count > 0)
            {
                var matches = Group.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();
                Group.Bracket.MatchesCount = matches.Count;
                Group.Bracket.CompletedMatchesCount = matches.Count(m => m.Status == MatchStatusEnum.Completed);
            }
        }

        public virtual IEnumerable<TournamentResult> GetResults()
        {
            var groupWrestlers = GetGroupWrestlers();

            // Clear results
            foreach (var wr in groupWrestlers)
            {
                wr.FinalPlace = null;
            }

            //var matches = Group.Bracket.Rounds.SelectMany(p => p.RoundMatches).ToList();
            //if (matches.FirstOrDefault(x => x.Status == MatchStatusEnum.Pending) != null) return null;

            CalculateResults();
            return GetStats();
        }

        protected abstract void CalculateResults();

        public virtual void Load(Tournament tournament, AgeWeightGroup group)
        {
            Tournament = tournament;
            Group = group;

            SetMatchesCount();
        }

        public virtual void CompleteMatch(WrestlingMatch wrestlingMatch, bool? isRedWon, MatchWinTypeEnum winType)
        {
            if (wrestlingMatch == null)
            {
                throw new ArgumentNullException(nameof(wrestlingMatch));
            }

            wrestlingMatch.IsRedWon = isRedWon;
            wrestlingMatch.WinType = winType;
            wrestlingMatch.Status = MatchStatusEnum.Completed;

            // Mutual DSQ for brutality (UWW): both wrestlers placed last with
            // no rank — FinalPlace stays null, IsDisqualified is set so UI
            // shows the «DSQ» badge and team scoring contributes 0.
            if (winType == MatchWinTypeEnum.MutualDisqualify)
            {
                if (wrestlingMatch.WrestlerInRed != null) wrestlingMatch.WrestlerInRed.IsDisqualified = true;
                if (wrestlingMatch.WrestlerInBlue != null) wrestlingMatch.WrestlerInBlue.IsDisqualified = true;
            }
            // Mutual no-show (UWW «обоюдная неявка»): same outcome as mutual
            // DSQ — both placeless, 0 CP — but UI shows «Неявка» badge.
            else if (winType == MatchWinTypeEnum.MutualNoShow)
            {
                if (wrestlingMatch.WrestlerInRed != null && !wrestlingMatch.WrestlerInRed.IsDisqualified)
                    wrestlingMatch.WrestlerInRed.IsNoShow = true;
                if (wrestlingMatch.WrestlerInBlue != null && !wrestlingMatch.WrestlerInBlue.IsDisqualified)
                    wrestlingMatch.WrestlerInBlue.IsNoShow = true;
            }
            // Single DSQ (UWW): the loser is disqualified from the tournament
            // — FinalPlace stays null, IsDisqualified is set so UI shows the
            // «DSQ» badge and team scoring contributes 0. Mirrors mutual-DSQ
            // semantics for the loser side only; the winner advances.
            // WarningsLimit (3 предупреждения, VCA 5:0) shares the same DSQ
            // semantics — UWW classifies it as disqualification by warnings.
            else if ((winType == MatchWinTypeEnum.DisqualifyWin || winType == MatchWinTypeEnum.WarningsLimit)
                     && isRedWon.HasValue)
            {
                var dsqLoser = isRedWon.Value ? wrestlingMatch.WrestlerInBlue : wrestlingMatch.WrestlerInRed;
                if (dsqLoser != null) dsqLoser.IsDisqualified = true;
            }
            // NoShow (UWW): the absent wrestler is placeless with «Неявка»
            // badge and 0 CP, same outcome as DSQ but a different label.
            // Suppressed when the loser is already IsDisqualified — cascaded
            // NoShow matches against a previously-DSQ'd wrestler shouldn't
            // overwrite the DSQ badge.
            else if (winType == MatchWinTypeEnum.NoShow && isRedWon.HasValue)
            {
                var noShowLoser = isRedWon.Value ? wrestlingMatch.WrestlerInBlue : wrestlingMatch.WrestlerInRed;
                if (noShowLoser != null && !noShowLoser.IsDisqualified) noShowLoser.IsNoShow = true;
            }

            ProceedToNextMatch(wrestlingMatch);

            var round = Group.Bracket.Rounds.FirstOrDefault(r => r.RoundNumber == wrestlingMatch.RoundNumber);
            if (round != null && round.RoundType == GroupRoundTypeEnum.Main)
            {
                ProceedToAdditionalBracket(wrestlingMatch);
            }

            // If wintype is Disqualification, WarningsLimit, or NoShow we
            // should set results of this wrestler matches automatically
            if (winType == MatchWinTypeEnum.DisqualifyWin
                || winType == MatchWinTypeEnum.WarningsLimit
                || winType == MatchWinTypeEnum.NoShow)
            {
                var looser = isRedWon.Value ? wrestlingMatch.WrestlerInBlue : wrestlingMatch.WrestlerInRed;

                // UWW: a DSQ'd wrestler does not contest remaining matches —
                // they're recorded as NoShow losses. Even when the originating
                // event was DisqualifyWin, downstream auto-completions are
                // NoShow (the wrestler simply doesn't appear).
                CompleteFullLooserMatches(looser, MatchWinTypeEnum.NoShow);
                CompleteWinnerMatchesIfOtherWrestlersDisqualOrNoShow(isRedWon.Value
                    ? wrestlingMatch.WrestlerInRed
                    : wrestlingMatch.WrestlerInBlue);
            }
            else if (winType == MatchWinTypeEnum.MutualDisqualify
                     || winType == MatchWinTypeEnum.MutualNoShow
                     || winType == MatchWinTypeEnum.MutualInjury)
            {
                // Round-robin (M4): cascade NoShow to BOTH wrestlers' remaining
                // pending matches. Opponents get +5 CP — they did not
                // contribute to the brutality. In Olympic (M1), the only
                // pending matches for these two are downstream and unfilled,
                // so this loop is a no-op there; M1 propagation is handled
                // by ProceedToNextMatch via the sibling check.
                if (wrestlingMatch.WrestlerInRed != null)
                    CompleteFullLooserMatches(wrestlingMatch.WrestlerInRed, MatchWinTypeEnum.NoShow);
                if (wrestlingMatch.WrestlerInBlue != null)
                    CompleteFullLooserMatches(wrestlingMatch.WrestlerInBlue, MatchWinTypeEnum.NoShow);
            }

            SetMatchesCount();
        }

        private void CompleteFullLooserMatches(Wrestler looser, MatchWinTypeEnum winType)
        {
            var uncompletedMatchesForLooser = Group.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                .Where(m => !m.IsMatchCompleted && m.WrestlerInRed != null && m.WrestlerInBlue != null &&
                            (m.WrestlerInRed.ID == looser.ID || m.WrestlerInBlue.ID == looser.ID))
                .ToList();

            if (uncompletedMatchesForLooser.Count == 0) return;
            
            CompleteMatch(uncompletedMatchesForLooser[0], uncompletedMatchesForLooser[0].WrestlerInRed.ID != looser.ID, winType);
        }

        private void CompleteWinnerMatchesIfOtherWrestlersDisqualOrNoShow(Wrestler winner)
        {
            var uncompletedMatchesForWinner = Group.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                .Where(m => !m.IsMatchCompleted && m.WrestlerInRed != null && m.WrestlerInBlue != null &&
                            (m.WrestlerInRed.ID == winner.ID || m.WrestlerInBlue.ID == winner.ID))
                .ToList();
            
            if (uncompletedMatchesForWinner.Count == 0) return;

            foreach (var match in uncompletedMatchesForWinner)
            {
                var anotherWrestler = match.WrestlerInRed.ID == winner.ID ? match.WrestlerInBlue : match.WrestlerInRed;
                
                // Check if another wrestler lost due to disqual or noshow
                var lostMatches = Group.Bracket.Rounds.SelectMany(r => r.RoundMatches)
                    .Where(m => m.IsMatchCompleted
                                && (m.WinType == MatchWinTypeEnum.DisqualifyWin || m.WinType == MatchWinTypeEnum.NoShow)
                                && ((m.IsRedWinner && m.WrestlerInBlue.ID == anotherWrestler.ID)
                                    || (m.IsBlueWon && m.WrestlerInRed.ID == anotherWrestler.ID)))
                    .ToList();

                if (lostMatches.Count > 0)
                {
                    var hasDisqual = lostMatches.Any(x => x.WinType == MatchWinTypeEnum.DisqualifyWin);
                    
                    CompleteMatch(match, match.WrestlerInRed.ID == winner.ID, hasDisqual ? MatchWinTypeEnum.DisqualifyWin : MatchWinTypeEnum.NoShow);
                }
            }
        }

        // Manual DSQ-clear path used by the bracket UI when an operator clicks
        // the orange-X icon next to a wrestler. Strategy:
        //   1. Find the originating mutual-DSQ match (the one whose
        //      CompleteMatch flipped IsDisqualified=true on this wrestler).
        //      That's any match in this bracket with WinType=MutualDisqualify
        //      containing the wrestler.
        //   2. Found → call RevertMatch on it. Standard mutual-DSQ revert path
        //      clears IsDisqualified on both wrestlers and frees the match
        //      cell (Status=Pending, WinType=null).
        //   3. Not found → bracket may have been regenerated since the DSQ
        //      was set, or processor-specific state may hide it; ask the
        //      override and otherwise just clear the flag as a fallback so
        //      the wrestler can rejoin downstream play.
        public virtual void ClearWrestlerDisqualify(Wrestler wrestler)
        {
            if (wrestler == null || !wrestler.IsDisqualified) return;
            if (Group?.Bracket == null)
            {
                wrestler.IsDisqualified = false;
                return;
            }

            var mutualMatch = Group.Bracket.Rounds
                .SelectMany(r => r.RoundMatches)
                .FirstOrDefault(m => m.WinType == MatchWinTypeEnum.MutualDisqualify
                                     && (m.WrestlerInRed.SameAs(wrestler) || m.WrestlerInBlue.SameAs(wrestler)));

            if (mutualMatch != null)
            {
                RevertMatch(mutualMatch);
                return;
            }

            // Subclass hook for processors with more elaborate state — e.g.
            // OlympicWithConsolationFromFinalists rebuilds the SF after mutual
            // DSQ, leaving the original match's WinType cleared. The override
            // returns the rebuilt match so we can run the two-step revert.
            var indirect = FindIndirectMutualDisqualifyMatch(wrestler);
            if (indirect != null)
            {
                // Two-step: first revert un-rebuilds (override hook in the
                // subclass restores the mutual-DSQ Completed state); the
                // second revert runs the standard mutual-DSQ revert path.
                RevertMatch(indirect);
                if (indirect.WinType == MatchWinTypeEnum.MutualDisqualify)
                {
                    RevertMatch(indirect);
                }
                return;
            }

            // No match left to revert (e.g. bracket regenerated). Just clear
            // the flag — graceful degradation for «stuck» DSQ markers.
            wrestler.IsDisqualified = false;
        }

        // Override to surface a match that DOES NOT currently have
        // WinType=MutualDisqualify but was the origin of a wrestler's DSQ
        // flag (e.g. a SF whose WinType was cleared by an auto-rebuild).
        // Returning the match enables a two-step revert in
        // ClearWrestlerDisqualify.
        protected virtual WrestlingMatch FindIndirectMutualDisqualifyMatch(Wrestler wrestler) => null;

        public virtual bool CanMatchBeReverted(WrestlingMatch wrestlingMatch)
        {
            // Match can be reverted if it is not Pending, WinType is set and it is not free win

            if (wrestlingMatch.Status == MatchStatusEnum.Pending || !wrestlingMatch.WinType.HasValue || (wrestlingMatch.RoundNumber == 1 && wrestlingMatch.WinType.Value == MatchWinTypeEnum.FreeWin)) return false;

            if (!string.IsNullOrEmpty(wrestlingMatch.NextMatchBracketFullNumber))
            {
                var nextMatch = Group.Bracket.Rounds.SelectMany(p => p.RoundMatches).FirstOrDefault(x => x.BracketFullNumber == wrestlingMatch.NextMatchBracketFullNumber);
                if (nextMatch == null) throw new BracketStateException($"Next match '{wrestlingMatch.NextMatchBracketFullNumber}' referenced by match '{wrestlingMatch.BracketFullNumber}' does not exist in the bracket.");

                return nextMatch.Status == MatchStatusEnum.Pending;
            }

            return true;
        }

        public virtual void RevertMatch(WrestlingMatch wrestlingMatch)
        {
            if (!CanMatchBeReverted(wrestlingMatch)) throw new InvalidOperationException("Match cannot be reverted in its current state (next match is already completed or it is a round-1 free win).");

            // Revert of mutual DSQ: clear IsDisqualified set by this match.
            // Cascaded DisqualifyWin matches (M4) keep their state — operator
            // reverts those individually if needed (matches existing single-
            // DSQ revert behavior).
            if (wrestlingMatch.WinType == MatchWinTypeEnum.MutualDisqualify)
            {
                if (wrestlingMatch.WrestlerInRed != null) wrestlingMatch.WrestlerInRed.IsDisqualified = false;
                if (wrestlingMatch.WrestlerInBlue != null) wrestlingMatch.WrestlerInBlue.IsDisqualified = false;
            }
            // Revert of mutual no-show: clear IsNoShow on both wrestlers.
            else if (wrestlingMatch.WinType == MatchWinTypeEnum.MutualNoShow)
            {
                if (wrestlingMatch.WrestlerInRed != null) wrestlingMatch.WrestlerInRed.IsNoShow = false;
                if (wrestlingMatch.WrestlerInBlue != null) wrestlingMatch.WrestlerInBlue.IsNoShow = false;
            }
            // Revert of single DSQ / WarningsLimit: clear the loser's
            // IsDisqualified flag. Cascaded matches against the same wrestler
            // keep their state — operator reverts those individually if needed.
            else if ((wrestlingMatch.WinType == MatchWinTypeEnum.DisqualifyWin
                      || wrestlingMatch.WinType == MatchWinTypeEnum.WarningsLimit)
                     && wrestlingMatch.IsRedWon.HasValue)
            {
                var dsqLoser = wrestlingMatch.IsRedWon.Value ? wrestlingMatch.WrestlerInBlue : wrestlingMatch.WrestlerInRed;
                if (dsqLoser != null) dsqLoser.IsDisqualified = false;
            }
            // Revert of NoShow: clear the loser's IsNoShow flag. Same caveat
            // as DSQ revert — cascaded NoShow matches stay completed.
            else if (wrestlingMatch.WinType == MatchWinTypeEnum.NoShow && wrestlingMatch.IsRedWon.HasValue)
            {
                var noShowLoser = wrestlingMatch.IsRedWon.Value ? wrestlingMatch.WrestlerInBlue : wrestlingMatch.WrestlerInRed;
                if (noShowLoser != null) noShowLoser.IsNoShow = false;
            }

            if (!string.IsNullOrEmpty(wrestlingMatch.NextMatchBracketFullNumber) && wrestlingMatch.IsRedWon.HasValue)
            {
                var nextMatch = Group.Bracket.Rounds.SelectMany(p => p.RoundMatches).FirstOrDefault(x => x.BracketFullNumber == wrestlingMatch.NextMatchBracketFullNumber);

                if (nextMatch == null) throw new BracketStateException($"Next match '{wrestlingMatch.NextMatchBracketFullNumber}' referenced by match '{wrestlingMatch.BracketFullNumber}' does not exist in the bracket.");

                if (wrestlingMatch.IsRedWon.Value)
                {
                    if (nextMatch.WrestlerInRed.SameAs(wrestlingMatch.WrestlerInRed))
                    {
                        nextMatch.WrestlerInRed = null;
                    }
                    else if (nextMatch.WrestlerInBlue.SameAs(wrestlingMatch.WrestlerInRed))
                    {
                        nextMatch.WrestlerInBlue = null;
                    }
                }
                else
                {
                    if (nextMatch.WrestlerInRed.SameAs(wrestlingMatch.WrestlerInBlue))
                    {
                        nextMatch.WrestlerInRed = null;
                    }
                    else if (nextMatch.WrestlerInBlue.SameAs(wrestlingMatch.WrestlerInBlue))
                    {
                        nextMatch.WrestlerInBlue = null;
                    }
                }
            }

            RevertAdditionalBracket(wrestlingMatch);

            wrestlingMatch.Status = MatchStatusEnum.Pending;
            wrestlingMatch.LastSecondInMatch = 0;
            wrestlingMatch.PointsBlue = 0;
            wrestlingMatch.PointsRed = 0;
            wrestlingMatch.WarningsNumberBlue = 0;
            wrestlingMatch.WarningsNumberRed = 0;
            wrestlingMatch.StartDateTime = null;
            wrestlingMatch.IsRedWon = null;
            wrestlingMatch.WinType = null;

            SetMatchesCount();
        }

        protected IEnumerable<Wrestler> GetGroupWrestlers()
        {
            return Group.Wrestlers;
        }

        protected virtual void RevertAdditionalBracket(WrestlingMatch wrestlingMatch)
        {
            
        }

        protected virtual void ProceedToAdditionalBracket(WrestlingMatch wrestlingMatch)
        {
            
        }

        protected WrestlingMatch GenerateGroupMatch(int roundNumber, string roundName, Wrestler red, Wrestler blue, int bracketMatchNumber, bool hasNextMatch)
        {
            int nextMatchNumber = (int)Math.Ceiling((double)bracketMatchNumber / 2);
            
            string nextNumber = $"{roundNumber + 1}.{nextMatchNumber}";

            var match = new WrestlingMatch
            {
                WrestlerInRed = red,
                WrestlerInBlue = blue,
                RoundNumber = roundNumber,
                RoundName = roundName,
                BracketNumber = bracketMatchNumber,
                GroupID = Group.ID,
                GroupName = Group.Name,
                MaxRoundSecond = Group.MaxRoundSecond,
                MaxTimeoutSecond = Group.MaxTimeoutSecond,
                MaxActionSecond = Group.MaxActionSecond,
                NextMatchBracketFullNumber = hasNextMatch ? nextNumber : string.Empty,
                Status = MatchStatusEnum.Pending
            };

            return match;
        }

        protected int GetRoundsCount(int wrestlers)
        {
            double roundsCount = Math.Log(wrestlers, 2);
            if (!IsDoubleInteger(roundsCount))
            {
                roundsCount += 1;
            }

            return (int)roundsCount;
        }

        protected bool IsMatchOfRoundType(WrestlingMatch wrestlingMatch, GroupRoundTypeEnum roundType)
        {
            return Group.Bracket.Rounds.Where(p => p.RoundType == roundType)
                .SelectMany(x => x.RoundMatches).FirstOrDefault(o => o == wrestlingMatch) != null;
        }

        protected virtual void ProceedToNextMatch(WrestlingMatch wrestlingMatch)
        {
            // Proceed to next stage
            if (string.IsNullOrEmpty(wrestlingMatch.NextMatchBracketFullNumber)) return;

            var nextMatch = Group.Bracket.Rounds.SelectMany(p => p.RoundMatches).FirstOrDefault(x => x.BracketFullNumber == wrestlingMatch.NextMatchBracketFullNumber);
            if (nextMatch == null) throw new BracketStateException($"Next match '{wrestlingMatch.NextMatchBracketFullNumber}' referenced by match '{wrestlingMatch.BracketFullNumber}' does not exist in the bracket.");

            // Mutual DSQ (M1): neither wrestler advances. If the sibling
            // source for nextMatch is already completed with a real winner,
            // that wrestler will stand alone — auto-FreeWin nextMatch for
            // them. Otherwise leave nextMatch pending; the sibling's own
            // ProceedToNextMatch will detect this via the symmetric branch
            // below when it eventually completes.
            if (wrestlingMatch.WinType == MatchWinTypeEnum.MutualDisqualify
                || wrestlingMatch.WinType == MatchWinTypeEnum.MutualNoShow
                || wrestlingMatch.WinType == MatchWinTypeEnum.MutualInjury)
            {
                var sibling = FindSiblingInBracket(wrestlingMatch);
                if (sibling != null && sibling.Status == MatchStatusEnum.Completed && sibling.IsRedWon.HasValue)
                {
                    var siblingWinner = sibling.IsRedWon.Value ? sibling.WrestlerInRed : sibling.WrestlerInBlue;
                    var isRedTheWinner = nextMatch.WrestlerInRed != null && nextMatch.WrestlerInRed.SameAs(siblingWinner);
                    CompleteMatch(nextMatch, isRedTheWinner, MatchWinTypeEnum.FreeWin);
                }
                return;
            }

            var winner = wrestlingMatch.IsRedWon.HasValue && wrestlingMatch.IsRedWon.Value ? wrestlingMatch.WrestlerInRed : wrestlingMatch.WrestlerInBlue;

            if (wrestlingMatch.BracketNumber % 2 == 0 || nextMatch.WrestlerInRed != null)
            {
                nextMatch.WrestlerInBlue = winner;
            }
            else
            {
                nextMatch.WrestlerInRed = winner;
            }

            // M1 reverse direction: if our sibling source was a mutual
            // DSQ / NoShow / Injury, the other slot in nextMatch will never
            // be filled — auto-FreeWin for the wrestler we just placed.
            var sib = FindSiblingInBracket(wrestlingMatch);
            if (sib != null && sib.Status == MatchStatusEnum.Completed
                && (sib.WinType == MatchWinTypeEnum.MutualDisqualify
                    || sib.WinType == MatchWinTypeEnum.MutualNoShow
                    || sib.WinType == MatchWinTypeEnum.MutualInjury))
            {
                var isRedTheWinner = nextMatch.WrestlerInRed != null && nextMatch.WrestlerInRed.SameAs(winner);
                CompleteMatch(nextMatch, isRedTheWinner, MatchWinTypeEnum.FreeWin);
            }
        }

        // Finds the parallel match in the same round that feeds the same
        // next-round match. In a standard elimination bracket, every two
        // matches in round R feed one match in R+1 — those two are siblings.
        protected WrestlingMatch FindSiblingInBracket(WrestlingMatch match)
        {
            if (string.IsNullOrEmpty(match.NextMatchBracketFullNumber)) return null;
            var round = Group.Bracket.Rounds.FirstOrDefault(r => r.RoundNumber == match.RoundNumber);
            if (round == null) return null;
            return round.RoundMatches.FirstOrDefault(m =>
                m != match && m.NextMatchBracketFullNumber == match.NextMatchBracketFullNumber);
        }

        private bool IsDoubleInteger(double value)
        {
            return Math.Abs(value % 1) <= (Double.Epsilon * 100);
        }

        protected List<TournamentResult> GetStats()
        {
            var groupWrestlers = GetGroupWrestlers();

            return groupWrestlers.Select(wr => new TournamentResult(Group, wr)).OrderBy(p => p.Wrestler.FinalPlace).ToList();
        }

        protected void ShuffleWrestlers(IList<Wrestler> list)
        {
            var rnd = new Random();

            for (var i = 0; i < list.Count; i++)
            {
                Swap(list, i, rnd.Next(i, list.Count));
            }
        }

        private void Swap(IList<Wrestler> list, int i, int j)
        {
            (list[i], list[j]) = (list[j], list[i]);
        }

        public virtual GroupRound GetSemiFinalRound(AgeWeightGroup group)
        {
            if (group == null || group.Bracket == null || group.Bracket.Rounds == null || group.Bracket.Rounds.Count < 2) return null;

            var mainRounds = group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();

            if (mainRounds.Count < 2) return null;

            return mainRounds[mainRounds.Count - 2];
        }

        public virtual List<GroupRound> GetMainQualificationRounds(AgeWeightGroup group)
        {
            if (group == null || group.Bracket == null || group.Bracket.Rounds == null) return null;

            var result = new List<GroupRound>();

            var semiFinalRound = GetSemiFinalRound(group);

            if (semiFinalRound == null) return result;

            foreach(var round in group.Bracket.Rounds)
            {
                if (round.RoundNumber < semiFinalRound.RoundNumber)
                {
                    result.Add(round);
                }
            }

            return result;
        }

        public virtual GroupRound Get3rdPlaceRound(AgeWeightGroup group)
        {
            return null;
        }

        public virtual List<GroupRound> GetAdditionalQualificationRounds(AgeWeightGroup group)
        {
            return new List<GroupRound>();
        }

        public virtual GroupRound GetFinalRound(AgeWeightGroup group)
        {
            if (group == null || group.Bracket == null || group.Bracket.Rounds == null) return null;

            var mainRounds = group.Bracket.Rounds.Where(r => r.RoundType == GroupRoundTypeEnum.Main).ToList();

            if (mainRounds.Count == 0) return null;

            var finalRound = mainRounds[mainRounds.Count - 1];

            return finalRound;
        }
    }
}
