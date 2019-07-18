using System;
using System.Collections.ObjectModel;
using System.Linq;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    public class CarpetStats : ObservableObject
    {
        private Guid _carpetId;
        private string _carpetLabel;
        private int _groupsCount;
        private int _wrestlersCount;
        private bool _isExpanded;
        private ObservableCollection<WrestlingMatch> _matches;

        public CarpetStats()
        {
            _matches = new ObservableCollection<WrestlingMatch>();
        }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                _isExpanded = value;

                OnPropertyChanged("IsExpanded");
            }
        }

        public Guid CarpetID
        {
            get { return _carpetId; }
            set
            {
                _carpetId = value;

                OnPropertyChanged("CarpetID");
            }
        }

        public string CarpetLabel
        {
            get { return _carpetLabel; }
            set
            {
                _carpetLabel = value;

                OnPropertyChanged("CarpetLabel");
            }
        }

        public int GroupsCount
        {
            get { return _groupsCount; }
            set
            {
                _groupsCount = value;

                OnPropertyChanged("GroupsCount");
            }
        }

        public int WrestlersCount
        {
            get { return _wrestlersCount; }
            set
            {
                _wrestlersCount = value;

                OnPropertyChanged("WrestlersCount");
            }
        }

        public ObservableCollection<WrestlingMatch> Matches
        {
            get { return _matches; }
            set
            {
                _matches = value;

                OnPropertyChanged("Matches");
            }
        }


        public int MatchesCount => Matches?.Count ?? 0;
        public int CompletedMatchesCount => Matches?.Where(m => m.IsMatchCompleted).Count() ?? 0;
    }
}