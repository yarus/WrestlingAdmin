using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    public class MatStats : ObservableObject
    {
        private Guid _matId;
        private string _matLabel;
        private int _groupsCount;
        private int _wrestlersCount;
        private bool _isExpanded;
        private ObservableCollection<WrestlingMatch> _matches;
        private readonly ObservableCollection<WrestlingMatch> _matchesReady;

        public MatStats()
        {
            _matches = new ObservableCollection<WrestlingMatch>();
            _matchesReady = new ObservableCollection<WrestlingMatch>();
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

        public Guid MatID
        {
            get { return _matId; }
            set
            {
                _matId = value;

                OnPropertyChanged("MatID");
            }
        }

        public string MatLabel
        {
            get { return _matLabel; }
            set
            {
                _matLabel = value;

                OnPropertyChanged("MatLabel");
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
                if (_matches != null)
                {
                    _matches.CollectionChanged -= OnMatchesCollectionChanged;
                    foreach (var match in _matches)
                    {
                        match.PropertyChanged -= OnMatchPropertyChanged;
                    }
                }

                _matches = value;

                if (_matches != null)
                {
                    _matches.CollectionChanged += OnMatchesCollectionChanged;
                    foreach (var match in _matches)
                    {
                        match.PropertyChanged += OnMatchPropertyChanged;
                    }
                }

                RebuildMatchesReady();

                OnPropertyChanged("Matches");
                OnPropertyChanged("MatchesCount");
                OnPropertyChanged("MatchesLeft");
                OnPropertyChanged("CompletedMatchesCount");
            }
        }

        // Stable ObservableCollection reference — bindings stay valid for the
        // VM's lifetime. Membership tracks each match's IsMatchCanStart and
        // any add/remove on the Matches source collection.
        public ObservableCollection<WrestlingMatch> MatchesReady => _matchesReady;

        public int MatchesCount => _matches?.Count ?? 0;
        public int MatchesLeft => _matches?.Count(m => !m.IsMatchCompleted) ?? 0;
        public int MatchesReadyCount => _matchesReady.Count;
        public int CompletedMatchesCount => _matches?.Count(m => m.IsMatchCompleted) ?? 0;

        private void OnMatchesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (WrestlingMatch m in e.OldItems) m.PropertyChanged -= OnMatchPropertyChanged;
            }
            if (e.NewItems != null)
            {
                foreach (WrestlingMatch m in e.NewItems) m.PropertyChanged += OnMatchPropertyChanged;
            }

            RebuildMatchesReady();

            OnPropertyChanged("MatchesCount");
            OnPropertyChanged("MatchesLeft");
            OnPropertyChanged("CompletedMatchesCount");
        }

        private void OnMatchPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsMatchCanStart" || e.PropertyName == "IsMatchCompleted" || e.PropertyName == "Status")
            {
                RebuildMatchesReady();
                OnPropertyChanged("MatchesLeft");
                OnPropertyChanged("CompletedMatchesCount");
            }
        }

        private void RebuildMatchesReady()
        {
            _matchesReady.Clear();
            if (_matches == null) return;

            foreach (var m in _matches.Where(x => x.IsMatchCanStart))
            {
                _matchesReady.Add(m);
            }

            OnPropertyChanged("MatchesReadyCount");
        }
    }
}
