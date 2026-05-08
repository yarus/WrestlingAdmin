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

            ProceedToNextMatch(wrestlingMatch);

            var round = Group.Bracket.Rounds.FirstOrDefault(r => r.RoundNumber == wrestlingMatch.RoundNumber);
            if (round != null && round.RoundType == GroupRoundTypeEnum.Main)
            {
                ProceedToAdditionalBracket(wrestlingMatch);
            }

            // If wintype is Disqualification or NoShow we should set results of this wrestler matches automatically
            if (winType == MatchWinTypeEnum.DisqualifyWin || winType == MatchWinTypeEnum.NoShow)
            {
                var looser = isRedWon.Value ? wrestlingMatch.WrestlerInBlue : wrestlingMatch.WrestlerInRed;

                CompleteFullLooserMatches(looser, winType);
                CompleteWinnerMatchesIfOtherWrestlersDisqualOrNoShow(isRedWon.Value
                    ? wrestlingMatch.WrestlerInRed
                    : wrestlingMatch.WrestlerInBlue);
            }
            else if (winType == MatchWinTypeEnum.MutualDisqualify)
            {
                // Round-robin (M4): cascade DisqualifyWin to BOTH wrestlers'
                // remaining pending matches. Opponents get +5 CP — they did
                // not contribute to the brutality. In Olympic (M1), the only
                // pending matches for these two are downstream and unfilled,
                // so this loop is a no-op there; M1 propagation is handled
                // by ProceedToNextMatch via the sibling check.
                if (wrestlingMatch.WrestlerInRed != null)
                    CompleteFullLooserMatches(wrestlingMatch.WrestlerInRed, MatchWinTypeEnum.DisqualifyWin);
                if (wrestlingMatch.WrestlerInBlue != null)
                    CompleteFullLooserMatches(wrestlingMatch.WrestlerInBlue, MatchWinTypeEnum.DisqualifyWin);
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
            if (wrestlingMatch.WinType == MatchWinTypeEnum.MutualDisqualify)
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

            // M1 reverse direction: if our sibling source was a mutual DSQ,
            // the other slot in nextMatch will never be filled — auto-FreeWin
            // for the wrestler we just placed.
            var sib = FindSiblingInBracket(wrestlingMatch);
            if (sib != null && sib.Status == MatchStatusEnum.Completed && sib.WinType == MatchWinTypeEnum.MutualDisqualify)
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
