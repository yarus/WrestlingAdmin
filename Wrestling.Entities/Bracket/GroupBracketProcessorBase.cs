using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Wrestling.Entities.Results;

namespace Wrestling.Entities.Bracket
{
    public abstract class GroupBracketProcessorBase : IGroupBracketProcessor
    {
        protected Tournament Tournament { get; set; }
        public abstract string Title { get; }
        public abstract string Code { get; }
        public virtual int? AthletsMinCount => 2;
        public virtual int? AthletsMaxCount => null;

        protected AgeWeightGroup Group { get; set; }

        public void LoadTournamentGroup(Tournament tournament, AgeWeightGroup group)
        {
            Tournament = tournament;
            Group = group;
        }

        public void Generate(Tournament tournament, AgeWeightGroup group)
        {
            if (tournament == null || group == null || group.Wrestlers.Count == 0) return;

            LoadTournamentGroup(tournament, group);

            Group.Bracket = new GroupBracket
            {
                BracketTypeLabel = Title,
                BracketTypeCode = Code,
                WrestlersCount = group.Wrestlers.Count
            };

            GenerateRounds();
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

        public void Load(Tournament tournament, AgeWeightGroup group)
        {
            Tournament = tournament;
            Group = group;

            SetMatchesCount();
        }

        public virtual void CompleteMatch(WrestlingMatch wrestlingMatch, bool isRedWon, MatchWinTypeEnum winType)
        {
            if (wrestlingMatch == null)
            {
                throw new ApplicationException("WrestlingMatch not found!");
            }

            wrestlingMatch.IsRedWon = isRedWon;
            wrestlingMatch.WinType = winType;
            wrestlingMatch.Status = MatchStatusEnum.Completed;

            ProceedToNextMatch(wrestlingMatch);

            var round = Group.Bracket.Rounds.FirstOrDefault(r => r.RoundNumber == wrestlingMatch.RoundNumber);
            if (round != null && round.RoundType == GroupRoundTypeEnum.Main)
            {
                ProceedToAdditionalBracket(wrestlingMatch);
            }

            SetMatchesCount();
        }

        public virtual bool CanMatchBeReverted(WrestlingMatch wrestlingMatch)
        {
            // Match can be reverted if it is not Pending, WinType is set and it is not free win

            if (wrestlingMatch.Status == MatchStatusEnum.Pending || !wrestlingMatch.WinType.HasValue || (wrestlingMatch.RoundNumber == 1 && wrestlingMatch.WinType.Value == MatchWinTypeEnum.FreeWin)) return false;

            if (!string.IsNullOrEmpty(wrestlingMatch.NextMatchBracketFullNumber))
            {
                var nextMatch = Group.Bracket.Rounds.SelectMany(p => p.RoundMatches).FirstOrDefault(x => x.BracketFullNumber == wrestlingMatch.NextMatchBracketFullNumber);
                if (nextMatch == null) throw new ApplicationException("Next wrestlingMatch does not exist!");

                return nextMatch.Status == MatchStatusEnum.Pending;
            }

            return true;
        }

        public virtual void RevertMatch(WrestlingMatch wrestlingMatch)
        {
            if (!CanMatchBeReverted(wrestlingMatch)) throw new ApplicationException("WrestlingMatch can't be reverted!");

            if (!string.IsNullOrEmpty(wrestlingMatch.NextMatchBracketFullNumber) && wrestlingMatch.IsRedWon.HasValue)
            {
                var nextMatch = Group.Bracket.Rounds.SelectMany(p => p.RoundMatches).FirstOrDefault(x => x.BracketFullNumber == wrestlingMatch.NextMatchBracketFullNumber);

                if (nextMatch == null) throw new ApplicationException("Next wrestlingMatch does not exist!");

                if (wrestlingMatch.IsRedWon.Value)
                {
                    if (nextMatch.WrestlerInRed == wrestlingMatch.WrestlerInRed)
                    {
                        nextMatch.WrestlerInRed = null;
                    }
                    else if (nextMatch.WrestlerInBlue == wrestlingMatch.WrestlerInRed)
                    {
                        nextMatch.WrestlerInBlue = null;
                    }
                }
                else
                {
                    if (nextMatch.WrestlerInRed == wrestlingMatch.WrestlerInBlue)
                    {
                        nextMatch.WrestlerInRed = null;
                    }
                    else if (nextMatch.WrestlerInBlue == wrestlingMatch.WrestlerInBlue)
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
            //return Tournament.Wrestlers.Where(w => w.GroupID == Group.ID);
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
                roundsCount = roundsCount + 1;
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
            if (!string.IsNullOrEmpty(wrestlingMatch.NextMatchBracketFullNumber))
            {
                var nextMatch = Group.Bracket.Rounds.SelectMany(p => p.RoundMatches).FirstOrDefault(x => x.BracketFullNumber == wrestlingMatch.NextMatchBracketFullNumber);
                if (nextMatch == null) throw new ApplicationException("Can't find next wrestlingMatch!");

                var winner = wrestlingMatch.IsRedWon.HasValue && wrestlingMatch.IsRedWon.Value ? wrestlingMatch.WrestlerInRed : wrestlingMatch.WrestlerInBlue;

                if (wrestlingMatch.BracketNumber % 2 == 0 || nextMatch.WrestlerInRed != null)
                {
                    nextMatch.WrestlerInBlue = winner;
                }
                else
                {
                    nextMatch.WrestlerInRed = winner;
                }
            }
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
            Contract.Requires(list != null);
            Contract.Requires(i >= 0 && i < list.Count);
            Contract.Requires(j >= 0 && j < list.Count);

            var temp = list[i];
            list[i] = list[j];
            list[j] = temp;
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
