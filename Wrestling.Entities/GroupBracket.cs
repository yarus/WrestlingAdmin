using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Wrestling.Entities.Localization;

namespace Wrestling.Entities
{
    public class GroupBracket : INotifyPropertyChanged
    {
        private string _bracketTypeCode;
        private string _bracketTypeLabel;
        private int _wrestlersCount;
        private int _matchesCount;
        private int _completedMatchesCount;

        private List<GroupRound> _rounds;

        public GroupBracket()
        {
            _rounds = new List<GroupRound>();
        }

        public string BracketTypeCode
        {
            get { return _bracketTypeCode; }
            set
            {
                _bracketTypeCode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BracketTypeDisplay));
            }
        }

        public string BracketTypeLabel
        {
            get { return _bracketTypeLabel; }
            set
            {
                _bracketTypeLabel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BracketTypeDisplay));
            }
        }

        // Localized display name resolved per active language. Maps the
        // persisted BracketTypeCode (enum name) to a JSON key like
        // "BracketType_RoundRobin"; falls back to the persisted Russian
        // BracketTypeLabel for legacy entities and unknown codes.
        public string BracketTypeDisplay
        {
            get
            {
                var fallback = _bracketTypeLabel ?? string.Empty;
                if (string.IsNullOrEmpty(_bracketTypeCode)) return fallback;
                return EntityLocalization.T("BracketType_" + _bracketTypeCode, fallback);
            }
        }

        public int MatchesCount
        {
            get { return _matchesCount; }
            set
            {
                _matchesCount = value;
                OnPropertyChanged();
            }
        }

        public int CompletedMatchesCount
        {
            get { return _completedMatchesCount; }
            set
            {
                _completedMatchesCount = value;
                OnPropertyChanged();
            }
        }

        public int WrestlersCount
        {
            get { return _wrestlersCount; }
            set
            {
                _wrestlersCount = value;
                OnPropertyChanged();
            }
        }

        public List<GroupRound> Rounds
        {
            get { return _rounds; }
            set
            {
                _rounds = value;
                OnPropertyChanged();
            }
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}