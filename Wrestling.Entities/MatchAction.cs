using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wrestling.Entities
{
    public class MatchAction : INotifyPropertyChanged
    {
        private DateTime _dateTime;
        private int _roundNumber;
        private int _secondInRound;
        private bool? _isForRed;
        private int _points;
        private string _text;
        
        public DateTime DateTime
        {
            get { return _dateTime; }
            set
            {
                _dateTime = value;
                OnPropertyChanged();
            }
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

        public int SecondInRound
        {
            get { return _secondInRound; }
            set
            {
                _secondInRound = value;
                OnPropertyChanged();
            }
        }

        public bool? IsForRed
        {
            get { return _isForRed; }
            set
            {
                _isForRed = value;
                OnPropertyChanged();
            }
        }

        public int Points
        {
            get { return _points; }
            set
            {
                _points = value;
                OnPropertyChanged();
            }
        }

        public string Text
        {
            get { return _text; }
            set
            {
                _text = value;
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
