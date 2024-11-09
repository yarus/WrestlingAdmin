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
        public int WinsByInjury => GetWinsByType(MatchWinTypeEnum.Injury);
        public int WinsByWarningLimit => GetWinsByType(MatchWinTypeEnum.WarningsLimit);
        public int WinsByNoShow => GetWinsByType(MatchWinTypeEnum.NoShow);
        public int WinsByDisqual => GetWinsByType(MatchWinTypeEnum.DisqualifyWin);
        public int WinsByDomination => GetWinsByType(MatchWinTypeEnum.DominationWin);
        public int WinsByDominationWithPoints => GetWinsByType(MatchWinTypeEnum.DominationWinWithPoints);
        public int WinsByDominationTotal => WinsByDomination + WinsByDominationWithPoints;
        public int WinsByPoints => GetWinsByType(MatchWinTypeEnum.PointsWin);
        public int WinsByPointsWithPoints => GetWinsByType(MatchWinTypeEnum.PointsWinWithPoints);
        public int WinsByPointsTotal => WinsByPoints + WinsByPointsWithPoints;
        public int WinsByAction => GetWinsByType(MatchWinTypeEnum.ActionWin);
        public int AutoWinsCount => GetWinsByType(MatchWinTypeEnum.FreeWin);

        public int LoseByAction => GetLoseByType(MatchWinTypeEnum.ActionWin);
        public int LoseByPoints => GetLoseByType(MatchWinTypeEnum.PointsWinWithPoints);
        public int LoseByDomination => GetLoseByType(MatchWinTypeEnum.DominationWinWithPoints);

        public int LosesWithPoints => LoseByAction + LoseByPoints + LoseByDomination;

        public int FivePointWins => WinsByTushe + WinsByInjury + WinsByWarningLimit + WinsByNoShow + WinsByDisqual;
        public int FourPointWins => WinsByDomination + WinsByDominationWithPoints;
        public int ThreePointWin => WinsByPoints + WinsByPointsWithPoints + WinsByAction;

        public int OverallTournamentClassificationPoints
        {
            get
            {
                var fivePoints = FivePointWins * 5;
                var fourPoints = FourPointWins * 4;
                var threePoints = ThreePointWin * 3;
                var losePoints = LoseByAction + LoseByDomination + LoseByPoints;
                
                var result = fivePoints + fourPoints + threePoints + losePoints;
                return result;
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

        public int AllLostPoints
        {
            get
            {
                if (_group == null || _group.Bracket == null || _wrestler == null) return 0;

                var redPoints = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches)
                    .Where(x => x.Status == MatchStatusEnum.Completed && (x.WrestlerInRed == _wrestler))
                    .Sum(x => x.PointsBlue);

                var bluePoints = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches)
                    .Where(x => x.Status == MatchStatusEnum.Completed && (x.WrestlerInBlue == _wrestler))
                    .Sum(x => x.PointsRed);

                return redPoints + bluePoints;
            }
        }

        public int FastestActionSecond
        {
            get
            {
                if (_group == null || _group.Bracket == null || _wrestler == null) return 0;

                var redActions = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches)
                    .Where(x => x.Status == MatchStatusEnum.Completed && (x.WrestlerInRed == _wrestler) && x.LastSecondInMatch > 0)
                    .SelectMany(m => m.MatchActions)
                    .Where(a => a.Points > 0 && a.IsForRed.HasValue && a.IsForRed.Value && a.RoundNumber == 1 && a.SecondInRound > 2)
                    .OrderBy(a => a.SecondInRound)
                    .ToList();

                var blueActions = _group.Bracket.Rounds.SelectMany(p => p.RoundMatches)
                    .Where(x => x.Status == MatchStatusEnum.Completed && (x.WrestlerInBlue == _wrestler) && x.LastSecondInMatch > 0)
                    .SelectMany(m => m.MatchActions)
                    .Where(a => a.Points > 0 && a.IsForRed.HasValue && !a.IsForRed.Value && a.RoundNumber == 1 && a.SecondInRound > 2)
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

        public int TotalPoints => GetPlacePoints(Wrestler.FinalPlace);

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
            if (finalPlace.Value == 1) return 25;
            if (finalPlace.Value == 2) return 20;
            if (finalPlace.Value == 3) return 15;
            if (finalPlace.Value == 4) return 12;
            if (finalPlace.Value == 5) return 10;
            if (finalPlace.Value == 6) return 9;
            if (finalPlace.Value == 7) return 8;
            if (finalPlace.Value == 8) return 6;
            if (finalPlace.Value == 9) return 4;
            if (finalPlace.Value == 10) return 2;

            return 0;
        }

        public string GroupName => _group != null ? _group.Name : string.Empty;
        public string GroupBracketLabel => _group != null && _group.Bracket != null ? _group.Bracket.BracketTypeLabel : string.Empty;

        public string GroupLabel => $"{GroupName} - {GroupBracketLabel}";
    }
}