using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wrestling.Entities
{
    public class MatchAction : INotifyPropertyChanged
    {
        private MatchActionType _type;
        private DateTime _dateTime;
        private int _roundNumber;
        private int _secondInRound;
        private bool? _isForRed;
        private int _points;

        // Discriminator. Persisted as a string in the .wrt schema; legacy
        // entries without a Type round-trip via LegacyMatchActionTypeInferrer.
        public MatchActionType Type
        {
            get { return _type; }
            set
            {
                _type = value;
                OnPropertyChanged();
            }
        }

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

        // Per-Type semantics:
        //   SetPoints / RevertPoints  → points awarded / reverted (positive int)
        //   ShowActionTimer           → activity-timer duration in seconds
        //   RoundFinished             → completed round number (1 or 2)
        //   TimerAdjusted             → delta in seconds (signed)
        //   everything else           → 0
        public int Points
        {
            get { return _points; }
            set
            {
                _points = value;
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
