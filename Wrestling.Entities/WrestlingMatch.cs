using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Wrestling.Entities
{
    public class WrestlingMatch : INotifyPropertyChanged
    {
        private int _bracketNumber;
        private int _roundNumber;
        private int _matchNumber;
        private string _roundName;
        private Guid _groupId;
        private string _groupName;
        private int _pointsRed;
        private int _pointsBlue;
        private bool? _isRedWon;
        private int _lastSecondInMatch;
        private int _maxRoundSecond;
        private int _maxTimeoutSecond;
        private int _maxActionSecond;
        private DateTime? _startDateTime;
        private string _note;
        private MatchStatusEnum _status;
        private MatchWinTypeEnum? _winType;
        private int _version;

        private Wrestler _wrestlerInRed;
        private Wrestler _wrestlerInBlue;
        private string _nextMatchBracketFullNumber;
        private int _bestActionRed;
        private int _bestActionBlue;
        private int _bestActionRedCount;
        private int _bestActionBlueCount;
        private bool _isLastActionRed;

        private int _warningsNumberRed;
        private int _warningsNumberBlue;

        private List<MatchAction> _matchActions;

        public WrestlingMatch()
        {
            _matchActions = new List<MatchAction>();

            _status = MatchStatusEnum.Pending;
        }
        
        public string FileName { get; set; }

        public string BracketFullNumber => $"{RoundNumber}.{BracketNumber}";

        public int BracketNumber
        {
            get { return _bracketNumber; }
            set
            {
                _bracketNumber = value;
                OnPropertyChanged("BracketNumber");
            }
        }

        public int WarningsNumberRed
        {
            get { return _warningsNumberRed; }
            set
            {
                _warningsNumberRed = value;
                OnPropertyChanged("WarningsNumberRed");
            }
        }

        public int WarningsNumberBlue
        {
            get { return _warningsNumberBlue; }
            set
            {
                _warningsNumberBlue = value;
                OnPropertyChanged("WarningsNumberBlue");
            }
        }

        public string RoundName
        {
            get { return _roundName; }
            set
            {
                _roundName = value;
                OnPropertyChanged("RoundName");
            }
        }

        public bool IsLastActionRed
        {
            get { return _isLastActionRed; }
            set
            {
                _isLastActionRed = value;
                OnPropertyChanged("IsLastActionRed");
            }
        }

        public int MaxRoundSecond
        {
            get { return _maxRoundSecond; }
            set
            {
                _maxRoundSecond = value;
                OnPropertyChanged("MaxRoundSecond");
            }
        }

        public int MaxTimeoutSecond
        {
            get { return _maxTimeoutSecond; }
            set
            {
                _maxTimeoutSecond = value;
                OnPropertyChanged("MaxTimeoutSecond");
            }
        }

        public int MaxActionSecond
        {
            get { return _maxActionSecond; }
            set
            {
                _maxActionSecond = value;
                OnPropertyChanged("MaxActionSecond");
            }
        }

        public string MatchResult
        {
            get
            {
                if (Status == MatchStatusEnum.Completed)
                {
                    return $"{PointsRed} : {PointsBlue}";
                }
                return string.Empty;
            }
        } 

        public int MatchNumber
        {
            get { return _matchNumber; }
            set
            {
                _matchNumber = value;
                OnPropertyChanged("MatchNumber");
            }
        }

        public Guid GroupID
        {
            get { return _groupId; }
            set
            {
                _groupId = value;
                OnPropertyChanged("GroupID");
            }
        }

        public string GroupName
        {
            get { return _groupName; }
            set
            {
                _groupName = value;
                OnPropertyChanged("GroupName");
            }
        }

        public bool IsMatchCanStart => Status != MatchStatusEnum.Completed && WrestlerInRed != null && WrestlerInBlue != null;

        public MatchWinTypeEnum? WinType
        {
            get { return _winType; }
            set
            {
                _winType = value;
                OnPropertyChanged("WinType");
                OnPropertyChanged("IsMutualDisqualify");
            }
        }

        public List<MatchAction> MatchActions
        {
            get { return _matchActions; }
            set
            {
                _matchActions = value;
                OnPropertyChanged("MatchActions");
            }
        }

        public string NextMatchBracketFullNumber
        {
            get { return _nextMatchBracketFullNumber; }
            set
            {
                _nextMatchBracketFullNumber = value;
                OnPropertyChanged("NextMatchBracketFullNumber");
            }
        }

        public int RoundNumber
        {
            get { return _roundNumber; }
            set
            {
                _roundNumber = value;
                OnPropertyChanged("RoundNumber");
            }
        }

        public Wrestler WrestlerInRed
        {
            get { return _wrestlerInRed; }
            set
            {
                _wrestlerInRed = value;
                OnPropertyChanged("WrestlerInRed");
                OnPropertyChanged("IsMatchCanStart");
                OnPropertyChanged("HasViewableResults");
            }
        }

        public Wrestler WrestlerInBlue
        {
            get { return _wrestlerInBlue; }
            set
            {
                _wrestlerInBlue = value;
                OnPropertyChanged("WrestlerInBlue");
                OnPropertyChanged("IsMatchCanStart");
                OnPropertyChanged("HasViewableResults");
            }
        }

        public int PointsRed
        {
            get { return _pointsRed; }
            set
            {
                _pointsRed = value;
                OnPropertyChanged("PointsRed");
            }
        }

        public int PointsBlue
        {
            get { return _pointsBlue; }
            set
            {
                _pointsBlue = value;
                OnPropertyChanged("PointsBlue");
            }
        }

        public bool? IsRedWon
        {
            get { return _isRedWon; }
            set
            {
                _isRedWon = value;
                OnPropertyChanged("IsRedWon");
                OnPropertyChanged("IsBlueWon");
                OnPropertyChanged("IsRedWinner");
            }
        }

        public bool IsBlueWon
        {
            get { return !_isRedWon ?? false; }
            set
            {
                _isRedWon = !value;
                OnPropertyChanged("IsBlueWon");
                OnPropertyChanged("IsRedWon");
                OnPropertyChanged("IsRedWinner");
            }
        }

        // Null-safe red-winner check. Mirror of IsBlueWon: returns false for
        // any state where the red corner did not strictly win — including
        // mutual DSQ (IsRedWon=null) where neither wrestler won. Prefer this
        // over `IsRedWon.HasValue && IsRedWon.Value` and over `IsRedWon.Value`
        // (which throws NRE on mutual DSQ).
        public bool IsRedWinner => _isRedWon == true;

        public int LastSecondInMatch
        {
            get { return _lastSecondInMatch; }
            set
            {
                _lastSecondInMatch = value;
                OnPropertyChanged("LastSecondInMatch");
            }
        }

        public DateTime? StartDateTime
        {
            get { return _startDateTime; }
            set
            {
                _startDateTime = value;
                OnPropertyChanged("StartDateTime");
            }
        }

        public string Note
        {
            get { return _note; }
            set
            {
                _note = value;
                OnPropertyChanged("Note");
            }
        }

        public MatchStatusEnum Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged("Status");
                OnPropertyChanged("IsMatchCanStart");
                OnPropertyChanged("IsMatchCompleted");
                OnPropertyChanged("HasViewableResults");
            }
        }

        public bool IsMatchCompleted => _status == MatchStatusEnum.Completed;
        public bool IsMutualDisqualify => _winType == MatchWinTypeEnum.MutualDisqualify;

        // Auto-FreeWin'd empty consolation slots (no wrestlers, no winner) are
        // Completed but have nothing to show — opening them throws because
        // MatchResultsViewModel requires IsRedWon for non-mutual-DSQ wins. Use
        // this flag to gate the «open results» button in the bracket UI.
        public bool HasViewableResults => _status == MatchStatusEnum.Completed
                                          && (_wrestlerInRed != null || _wrestlerInBlue != null);

        // Monotonic per-match counter. Bumped exactly once on every state
        // transition the importer propagates (Pending→Completed by ApproveAsync,
        // Completed→Pending by RejectAsync). Never touched on mid-match scoring,
        // timer adjustments, or bracket structure changes — those are not
        // exported by the importer. Import comparison is strict ">" so equal
        // versions keep the local copy; this is the cheap escape hatch when two
        // peers concurrently approve the same match. Legacy .wrt files load with
        // 0 for Pending and 1 for Completed via the adapter (see
        // EntityToInfoAdapter.GetEntityFromInfo).
        public int Version
        {
            get { return _version; }
            set
            {
                _version = value;
                OnPropertyChanged("Version");
            }
        }

        public int BestActionRed
        {
            get { return _bestActionRed; }
            set
            {
                _bestActionRed = value;
                OnPropertyChanged("BestActionRed");
            }
        }

        public int BestActionBlue
        {
            get { return _bestActionBlue; }
            set
            {
                _bestActionBlue = value;
                OnPropertyChanged("BestActionBlue");
            }
        }

        public int BestActionRedCount
        {
            get { return _bestActionRedCount; }
            set
            {
                _bestActionRedCount = value;
                OnPropertyChanged("BestActionRedCount");
            }
        }

        public int BestActionBlueCount
        {
            get { return _bestActionBlueCount; }
            set
            {
                _bestActionBlueCount = value;
                OnPropertyChanged("BestActionBlueCount");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}