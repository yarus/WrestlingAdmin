using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
            }
        }

        public string BracketTypeLabel
        {
            get { return _bracketTypeLabel; }
            set
            {
                _bracketTypeLabel = value;
                OnPropertyChanged();
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