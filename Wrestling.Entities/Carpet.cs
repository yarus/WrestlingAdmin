using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Wrestling.Entities
{
    public class Carpet : INotifyPropertyChanged, ICloneable
    {
        private Guid? _id;
        private string _name;
        private ObservableCollection<AgeWeightGroup> _groups;

        public Carpet()
        {
            _groups = new ObservableCollection<AgeWeightGroup>();
        }

        public int MatchesCount => _groups.Sum(g => g.PendingMatchesCount);
        public int WrestlersCount => _groups.Sum(g => g.Wrestlers.Count);

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
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public object Clone()
        {
            var tmp = new Carpet();
            tmp.Sync(this);
            return tmp;
        }

        public void Sync(Carpet carpet)
        {
            ID = carpet.ID;
            Name = carpet.Name;
            Groups = carpet.Groups;
        }
    }
}