using System.Linq;

namespace Wrestling.Entities.Results
{
    public class TournamentResult
    {
        private AgeWeightGroup _group;
        private Wrestler _wrestler;

        public TournamentResult()
        {
        }

        public TournamentResult(AgeWeightGroup group, Wrestler wrestler)
        {
            _group = group;
            _wrestler = wrestler;
        }

        public AgeWeightGroup Group
        {
            set { _group = value; }
            get { return _group; }
        }

        public Wrestler Wrestler
        {
            get { return _wrestler; }
            set { _wrestler = value; }
        }

        public int Wins => GetWinsByType(null);
        public int Loses => GetLoseByType(null);
        public int WinsByTushe => GetWinsByType(MatchWinTypeEnum.Tushe);
        public int WinsByDomination => GetWinsByType(MatchWinTypeEnum.DominationWin);
        public int WinsByPoints => GetWinsByType(MatchWinTypeEnum.PointsWin);
        public int WinsByAction => GetWinsByType(MatchWinTypeEnum.ActionWin);
        public int WinsByDisqual => GetWinsByType(MatchWinTypeEnum.DisqualifyWin);
        public int LoseByDisqual => GetLoseByType(MatchWinTypeEnum.DisqualifyWin);
        public int LoseByAction => GetLoseByType(MatchWinTypeEnum.ActionWin);
        public int LoseByPoints => GetLoseByType(MatchWinTypeEnum.PointsWin);
        public int LoseByDomination => GetLoseByType(MatchWinTypeEnum.DominationWin);
        public int LoseByTushe => GetLoseByType(MatchWinTypeEnum.Tushe);
        public int AutoWinsCount => GetWinsByType(MatchWinTypeEnum.FreeWin);

        public int OverallTournamentRating
        {
            get
            {
                var pointsGained = WinsByTushe * 6 + WinsByDomination * 5 + WinsByPoints * 4 + WinsByAction * 3 + TotalGainedPointsWithoutTusheAndDomination;
                var poinstLost = LoseByTushe * 6 + LoseByDomination * 5 + LoseByPoints * 4 + LoseByAction * 3 + TotalLostPointsWithoutTusheAndDomination;
                var result = pointsGained - poinstLost;
                return result;
            }
        }

        public int TotalGainedPointsWithoutTusheAndDomination
        {
            get
            {
                if (_group == null || _group.Bracket == null || _wrestler == null) return 0;

                var redPoints = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches).Where(x =>
                    x.Status == MatchStatusEnum.Completed
                    && x.WinType != MatchWinTypeEnum.Tushe
                    && x.WinType != MatchWinTypeEnum.DominationWin
                    && (x.WrestlerInRed == _wrestler)).Sum(x => x.PointsRed);

                var bluePoints = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches).Where(x =>
                    x.Status == MatchStatusEnum.Completed
                    && x.WinType != MatchWinTypeEnum.Tushe
                    && x.WinType != MatchWinTypeEnum.DominationWin
                    && (x.WrestlerInBlue == _wrestler)).Sum(x => x.PointsBlue);

                return redPoints + bluePoints;
            }
        }

        public int AllGainedPoints
        {
            get
            {
                if (_group == null || _group.Bracket == null || _wrestler == null) return 0;

                var redPoints = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches)
                    .Where(x => x.Status == MatchStatusEnum.Completed && (x.WrestlerInRed == _wrestler))
                    .Sum(x => x.PointsRed);

                var bluePoints = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches)
                    .Where(x => x.Status == MatchStatusEnum.Completed && (x.WrestlerInBlue == _wrestler))
                    .Sum(x => x.PointsBlue);

                return redPoints + bluePoints;
            }
        }

        public int FastestActionSecond
        {
            get
            {
                if (_group == null || _group.Bracket == null || _wrestler == null) return 0;

                var redActions = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches)
                    .Where(x => x.Status == MatchStatusEnum.Completed && (x.WrestlerInRed == _wrestler) && x.LastSecondInMatch > 3)
                    .SelectMany(m => m.MatchActions)
                    .Where(a => a.Points > 0 && a.IsForRed.HasValue && a.IsForRed.Value && a.RoundNumber == 1 && a.SecondInRound > 1)
                    .OrderBy(a => a.SecondInRound)
                    .ToList();

                var blueActions = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches)
                    .Where(x => x.Status == MatchStatusEnum.Completed && (x.WrestlerInBlue == _wrestler) && x.LastSecondInMatch > 3)
                    .SelectMany(m => m.MatchActions)
                    .Where(a => a.Points > 0 && a.IsForRed.HasValue && !a.IsForRed.Value && a.RoundNumber == 1 && a.SecondInRound > 1)
                    .OrderBy(a => a.SecondInRound)
                    .ToList();

                var redSecond = _group.MaxRoundSecond * 2;
                if (redActions.Count > 0)
                {
                    redSecond = redActions[0].SecondInRound;
                }

                var blueSecond = _group.MaxRoundSecond * 2;
                if (blueActions.Count > 0)
                {
                    blueSecond = blueActions[0].SecondInRound;
                }

                return redSecond < blueSecond ? redSecond : blueSecond;
            }
        }
        
        public int FastestWinSecond
        {
            get
            {
                if (_group == null || _group.Bracket == null || _wrestler == null) return 0;

                var redFastestWinSecond = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches)
                        .Where(x => x.Status == MatchStatusEnum.Completed && x.WrestlerInRed == _wrestler && x.IsRedWon.Value && x.LastSecondInMatch > 3) // 3 because some matches can be completed manually
                        .ToList();

                var blueFastestWinSecond = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches)
                    .Where(x => x.Status == MatchStatusEnum.Completed && x.WrestlerInBlue == _wrestler && !x.IsRedWon.Value && x.LastSecondInMatch > 3)
                    .ToList();

                var redSecond = _group.MaxRoundSecond * 2;
                if (redFastestWinSecond != null && redFastestWinSecond.Count > 0)
                {
                    redSecond = redFastestWinSecond.Min(m => m.LastSecondInMatch);
                }

                var blueSecond = _group.MaxRoundSecond * 2;
                if (blueFastestWinSecond != null && blueFastestWinSecond.Count > 0)
                {
                    blueSecond = blueFastestWinSecond.Min(m => m.LastSecondInMatch);
                }

                return redSecond < blueSecond ? redSecond : blueSecond;
            }
        }        

        public int NumberOfAmplitudeActions
        {
            get
            {
                if (_group == null || _group.Bracket == null || _wrestler == null) return 0;

                var redActions = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches)
                    .Where(x => x.Status == MatchStatusEnum.Completed && (x.WrestlerInRed == _wrestler))
                    .SelectMany(m => m.MatchActions)
                    .Count(a => a.Points == 4 && a.IsForRed.HasValue && a.IsForRed.Value);

                var blueActions = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches)
                    .Where(x => x.Status == MatchStatusEnum.Completed && (x.WrestlerInBlue == _wrestler))
                    .SelectMany(m => m.MatchActions)
                    .Count(a => a.Points == 4 && a.IsForRed.HasValue && !a.IsForRed.Value);

                return redActions + blueActions;
            }
        }

        public int TotalLostPointsWithoutTusheAndDomination
        {
            get
            {
                if (_group == null || _group.Bracket == null || _wrestler == null) return 0;

                var redPoints = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches).Where(x =>
                    x.Status == MatchStatusEnum.Completed
                    && x.WinType != MatchWinTypeEnum.Tushe
                    && x.WinType != MatchWinTypeEnum.DominationWin
                    && (x.WrestlerInRed == _wrestler)).Sum(x => x.PointsBlue);

                var bluePoints = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches).Where(x =>
                    x.Status == MatchStatusEnum.Completed
                    && x.WinType != MatchWinTypeEnum.Tushe
                    && x.WinType != MatchWinTypeEnum.DominationWin
                    && (x.WrestlerInBlue == _wrestler)).Sum(x => x.PointsRed);

                return redPoints + bluePoints;
            }
        }


        public int MatchesCount
        {
            get
            {
                return _group != null && _group.Bracket != null && _wrestler != null
                    ? _group.Bracket.Rounds.SelectMany(p => p.RoundMatches).Where(x =>
                        x.Status == MatchStatusEnum.Completed
                        && (x.WrestlerInRed == _wrestler || x.WrestlerInBlue == _wrestler)).ToList().Count
                    : 0;
            }
        }

        public int TotalPoints => 1 + (Wins - AutoWinsCount) * 2 + GetPlacePoints(Wrestler.FinalPlace);

        private int GetWinsByType(MatchWinTypeEnum? winType)
        {
            return _group != null && _group.Bracket != null && _wrestler != null
                ? _group.Bracket.Rounds
                    .SelectMany(p => p.RoundMatches)
                    .Where(x =>
                        x.Status == MatchStatusEnum.Completed
                        && (x.IsRedWon.Value && x.WrestlerInRed == _wrestler || x.IsBlueWon && x.WrestlerInBlue == _wrestler)
                        && (winType == null || x.WinType == winType))
                    .ToList().Count
                : 0;
        }

        private int GetLoseByType(MatchWinTypeEnum? loseType)
        {
            return _group != null && _group.Bracket != null && _wrestler != null
                ? _group.Bracket.Rounds.SelectMany(p => p.RoundMatches).Where(x =>
                    x.Status == MatchStatusEnum.Completed
                    && (x.IsRedWon.Value && x.WrestlerInBlue == _wrestler || x.IsBlueWon && x.WrestlerInRed == _wrestler)
                    && (loseType == null || x.WinType == loseType)).ToList().Count
                : 0;

        }


        private int GetPlacePoints(int? finalPlace)
        {
            if (!finalPlace.HasValue) return 0;

            // Assume that we facing local event
            if (finalPlace.Value == 1) return 4;
            if (finalPlace.Value == 2) return 3;
            if (finalPlace.Value == 3) return 2;

            return 0;
        }

        public string GroupName => _group != null ? _group.Name : string.Empty;
        }
}