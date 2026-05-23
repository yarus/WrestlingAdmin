using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Wrestling.Entities
{
    public class Mat : INotifyPropertyChanged, ICloneable
    {
        private Guid? _id;
        private string _name;
        private ObservableCollection<AgeWeightGroup> _groups;

        public Mat()
        {
            _groups = new ObservableCollection<AgeWeightGroup>();
        }

        public int MatchesCount => _groups.Sum(g => g.PendingMatchesCount);
        public int WrestlersCount => _groups.Sum(g => g.Wrestlers.Count);

        public int TotalMatchesCount => _groups.Where(g => g.Bracket != null).Sum(g => g.Bracket.MatchesCount);
        public int CompletedMatchesCount => _groups.Where(g => g.Bracket != null).Sum(g => g.Bracket.CompletedMatchesCount);
        public string ProgressLabel => $"{CompletedMatchesCount} / {TotalMatchesCount}";

        // Sum of (round * 2 + timeout) seconds across every pending match of
        // every group on this mat. Auto-completed FreeWin matches are
        // excluded by PendingMatchesCount, so this represents work remaining.
        public int ExpectedDurationSeconds => _groups.Sum(g => g.PendingMatchesCount * (g.MaxRoundSecond * 2 + g.MaxTimeoutSecond));

        public string ExpectedDurationLabel
        {
            get
            {
                var ts = TimeSpan.FromSeconds(ExpectedDurationSeconds);
                if ((int)ts.TotalHours >= 1)
                {
                    // (int)TotalHours auto-widens for 10+ hour tournaments — no
                    // pad on hours, two-digit pad on minutes for alignment.
                    return $"{(int)ts.TotalHours}ч {ts.Minutes:D2}м";
                }
                return $"{ts.Minutes}м";
            }
        }

        public Guid? ID
        {
            get { return _id; }
            set
            {
                _id = value;
                OnPropertyChanged("ID");
            }
        }

        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                OnPropertyChanged("Name");
            }
        }

        public ObservableCollection<AgeWeightGroup> Groups
        {
            get { return _groups; }
            set
            {
                _groups = value;
                OnPropertyChanged("Groups");
            }
        }

        public void RefreshStats()
        {
            OnPropertyChanged("MatchesCount");
            OnPropertyChanged("WrestlersCount");
            OnPropertyChanged("TotalMatchesCount");
            OnPropertyChanged("CompletedMatchesCount");
            OnPropertyChanged("ProgressLabel");
            OnPropertyChanged("ExpectedDurationSeconds");
            OnPropertyChanged("ExpectedDurationLabel");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public object Clone()
        {
            var tmp = new Mat();
            tmp.Sync(this);
            return tmp;
        }

        public void Sync(Mat mat)
        {
            ID = mat.ID;
            Name = mat.Name;
            Groups = mat.Groups;
        }
    }
}