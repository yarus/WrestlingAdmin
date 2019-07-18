using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wrestling.Entities
{
    public class GroupRound : INotifyPropertyChanged
    {
        private int _roundNumber;
        private string _roundName;
        private GroupRoundTypeEnum _roundType;

        private List<WrestlingMatch> _roundMatches;

        public GroupRound()
        {
            _roundMatches = new List<WrestlingMatch>();
        }

        public int RoundNumber
        {
            get { return _roundNumber; }
            set
            {
                _roundNumber = value;
                OnPropertyChanged();
            }
        }

        public GroupRoundTypeEnum RoundType
        {
            get { return _roundType; }
            set
            {
                _roundType = value;
                OnPropertyChanged();
            }
        }

        public string RoundName
        {
            get { return _roundName; }
            set
            {
                _roundName = value;
                OnPropertyChanged();
            }
        }

        public List<WrestlingMatch> RoundMatches
        {
            get { return _roundMatches; }
            set
            {
                _roundMatches = value;
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