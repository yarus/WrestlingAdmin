using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
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
        private ICommand _clearDisqualifyCommand;
        private List<IGroupBracketProcessor> _processors;

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
            _processors = Resolve<List<IGroupBracketProcessor>>();
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

        // Bound to the orange-X icon overlays in BracketsView. Click → confirm
        // dialog → resolve the right processor for the wrestler's bracket →
        // ClearWrestlerDisqualify (finds the originating mutual-DSQ match and
        // reverts; falls back to clearing the flag if no match exists).
        public ICommand ClearDisqualifyCommand
        {
            get
            {
                if (_clearDisqualifyCommand == null)
                {
                    _clearDisqualifyCommand = new RelayCommand(
                        param => ClearDisqualify(param as Wrestler),
                        param => param is Wrestler w && w.IsDisqualified);
                }
                return _clearDisqualifyCommand;
            }
        }

        #endregion

        #region Private Methods

        private void ChangeCarpet(Carpet carpet)
        {
            SelectedCarpet = carpet;
        }

        private void ClearDisqualify(Wrestler wrestler)
        {
            if (wrestler == null || !wrestler.IsDisqualified) return;

            // Find the group whose bracket holds this wrestler so we can pick
            // the right processor (different bracket types share the
            // ClearWrestlerDisqualify entry point but their override behavior
            // differs — ConsolationFinalists handles rebuilt SF specially).
            var hostGroup = DataContext?.Tournament?.Groups
                .FirstOrDefault(g => g.Wrestlers.Any(w => w.SameAs(wrestler)));
            if (hostGroup?.Bracket == null) return;

            var msg = $"Снять дисквалификацию со спортсмена «{wrestler.FullName}»?\n" +
                      $"Матч с обоюдной дисквалификацией будет освобождён для повторной игры.";
            if (Dialog.ShowMessageBox(this, msg, "Подтверждение",
                    MessageBoxButton.OKCancel, MessageBoxImage.None) != MessageBoxResult.OK)
            {
                return;
            }

            var processor = _processors?.FirstOrDefault(p => p.Code == hostGroup.Bracket.BracketTypeCode);
            if (processor is GroupBracketProcessorBase concrete)
            {
                concrete.LoadTournamentGroup(DataContext.Tournament, hostGroup);
                concrete.ClearWrestlerDisqualify(wrestler);
                ShowSnackMessage("Дисквалификация снята.");
            }
            else
            {
                // Processor missing or doesn't expose ClearWrestlerDisqualify —
                // graceful fallback so the operator isn't stuck.
                wrestler.IsDisqualified = false;
                ShowSnackMessage("Дисквалификация снята.");
            }
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

            // Empty consolation slots resolved via auto-FreeWin (no wrestlers,
            // no winner — see OlympicWithConsolationFromFinalists sweep) have
            // nothing to display and break MatchResultsViewModel's invariants.
            if (match.WrestlerInRed == null && match.WrestlerInBlue == null) return;

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