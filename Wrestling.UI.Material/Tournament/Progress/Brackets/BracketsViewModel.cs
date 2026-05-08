using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.UI.Material.Match;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Progress.Brackets
{
    public class BracketsViewModel : TournamentViewModelBase
    {
        #region Fields

        private Carpet _selectedCarpet;
        private ObservableCollection<Carpet> _carpets;
        private ObservableCollection<AgeWeightGroup> _filteredGroups;
        private string _filterString;

        private ICommand _openMatchCommand;
        private ICommand _changeCarpetCommand;

        private IList<CommandButtonItem> _quickButtons;

        #endregion

        public BracketsViewModel(IDiContainer container) : base(container)
        {
        }
        
        public override string PageTitle => "Турнирная Сетка";

        public override bool IsBackButtonAvailable => true;

        public override void InitData()
        {
            base.InitData();

            if (Tournament == null)
            {
                throw new InvalidOperationException("Tournament is not set on the data context. Navigate to a tournament before opening this view.");
            }

            _quickButtons = null;
            Carpets = DataContext.Tournament.Carpets;

            if (Carpets.Count > 0 && _selectedCarpet == null || (Carpets.Count > 0 && !Carpets.Contains(SelectedCarpet))) SelectedCarpet = Carpets[0];

            RefreshFilteredGroups();
        }

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                return _quickButtons ??
                       (
                           _quickButtons = new List<CommandButtonItem>
                           {
                               new CommandButtonItem("Открыть расписание схваток", PackIconKind.Timetable, new RelayCommand(param => OpenSchedule(), param => true))
                           }
                       );
            }
        }

        #region Binding Properties
        
        public ObservableCollection<Carpet> Carpets
        {
            get { return _carpets; }
            set
            {
                _carpets = value;

                OnPropertyChanged("Carpets");
            }
        }

        public Carpet SelectedCarpet
        {
            get { return _selectedCarpet; }
            set
            {
                _selectedCarpet = value;

                OnPropertyChanged("SelectedCarpet");
                RefreshFilteredGroups();
            }
        }

        public string FilterString
        {
            get => _filterString;
            set
            {
                if (_filterString == value) return;
                _filterString = value;

                OnPropertyChanged(nameof(FilterString));
                RefreshFilteredGroups();
            }
        }

        public ObservableCollection<AgeWeightGroup> FilteredGroups
        {
            get => _filteredGroups;
            private set
            {
                _filteredGroups = value;

                OnPropertyChanged(nameof(FilteredGroups));
            }
        }

        #endregion

        #region Command Properties
        
        public ICommand OpenMatchCommand
        {
            get
            {
                if (_openMatchCommand == null)
                {
                    _openMatchCommand = new RelayCommand(param => OpenMatch(param as WrestlingMatch), param => param != null);
                }
                return _openMatchCommand;
            }
        }

        public ICommand ChangeCarpetCommand
        {
            get
            {
                if (_changeCarpetCommand == null)
                {
                    _changeCarpetCommand = new RelayCommand(param => ChangeCarpet(param as Carpet), param => param != null);
                }
                return _changeCarpetCommand;
            }
        }

        #endregion

        #region Private Methods

        private void ChangeCarpet(Carpet carpet)
        {
            SelectedCarpet = carpet;
        }

        // Mirrors Schedule's filter behavior: case-insensitive substring match across
        // wrestler FullName + team name + city, ignored until at least 3 characters.
        // When active, shows only groups whose bracket contains a passing match and
        // auto-expands those groups so the operator sees the result immediately.
        private void RefreshFilteredGroups()
        {
            if (_selectedCarpet == null)
            {
                FilteredGroups = new ObservableCollection<AgeWeightGroup>();
                return;
            }

            var hasTextFilter = !string.IsNullOrEmpty(_filterString) && _filterString.Length > 2;

            if (!hasTextFilter)
            {
                FilteredGroups = new ObservableCollection<AgeWeightGroup>(_selectedCarpet.Groups);
                return;
            }

            var matched = _selectedCarpet.Groups
                .Where(g => g.Bracket != null && BracketHasMatchPassingFilter(g.Bracket, _filterString))
                .ToList();

            foreach (var group in matched)
            {
                group.IsExpanded = true;
            }

            FilteredGroups = new ObservableCollection<AgeWeightGroup>(matched);
        }

        private static bool BracketHasMatchPassingFilter(GroupBracket bracket, string filter)
            => bracket.Rounds.SelectMany(r => r.RoundMatches).Any(m => MatchPassesFilter(m, filter));

        private static bool MatchPassesFilter(WrestlingMatch match, string filter)
            => WrestlerPassesFilter(match.WrestlerInRed, filter)
               || WrestlerPassesFilter(match.WrestlerInBlue, filter);

        private static bool WrestlerPassesFilter(Wrestler wrestler, string filter)
            => wrestler != null
               && (ContainsCi(wrestler.FullName, filter)
                   || ContainsCi(wrestler.TeamName, filter)
                   || ContainsCi(wrestler.TeamCity, filter));

        private static bool ContainsCi(string source, string value)
            => !string.IsNullOrEmpty(source)
               && source.IndexOf(value, StringComparison.InvariantCultureIgnoreCase) >= 0;

        private void OpenSchedule()
        {
            NavigateToView<ScheduleViewModel>();
        }

        // Brackets is a fullscreen overlay launched from Conducting (and reachable
        // via the toggle from Schedule). Back goes to the admin landing — same
        // reasoning as ScheduleViewModel.OnBackCommand.
        protected override void OnBackCommand()
        {
            NavigateToView<Conducting.ConductingViewModel>();
        }

        private void OpenMatch(WrestlingMatch match)
        {
            if (match == null) return;

            if (match.Status == MatchStatusEnum.Completed)
            {
                DataContext.Group = DataContext.Tournament.Groups.FirstOrDefault(g => g.ID == match.GroupID);
                DataContext.WrestlingMatch = match;
                NavigateToView<MatchResultsViewModel>();
            }
            else if (match.IsMatchCanStart)
            {
                DataContext.Group = DataContext.Tournament.Groups.FirstOrDefault(g => g.ID == match.GroupID);
                DataContext.WrestlingMatch = match;
                NavigateToView<MatchControlViewModel>();
            }
        }

        #endregion
    }
}