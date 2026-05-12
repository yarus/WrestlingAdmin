using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.UI.Material.Match;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Tournament.Progress.Brackets;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Progress.Schedule
{
    public class ScheduleViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        private ObservableCollection<Mat> _mats;
        private ObservableCollection<MatStats> _filteredStats;
        private MatStats _selectedMat;
        // Optional single-mat view: null = show all mats (default,
        // matches the legacy "mat schedule" page); set to a mat ID
        // by Phase 5 → Ковер wrapper so the operator only sees their own
        // mat's queue. GenerateStats() uses it to scope iteration.
        private Guid? _matIdFilter;
        // One-shot preselection — set by callers (e.g. Conducting mat
        // cards) before navigation; consumed in InitData and immediately
        // cleared so it doesn't leak into later visits.
        private Guid? _preselectedMatId;

        private ICommand _openMatchCommand;
        private ICommand _changeMatCommand;

        private IList<CommandButtonItem> _quickButtons;

        private string _filterString;

        private IKeyHandler _keyHandler;

        private IPanelView _scoreScreenView;
        private ScoreScreenViewModel _scoreScreen;

        public string PageName => T("Nav_Schedule", "Расписание");
        public override string PageTitle => T("Schedule_PageTitle", "Расписание схваток по коврам");

        public int MatsCount => DataContext.Tournament.Mats.Count;
        public int MatchesCount => DataContext.Tournament.Groups.Sum(g => g.Bracket?.MatchesCount ?? 0);
        public int CompletedMatchesCount => DataContext.Tournament.Groups.Sum(g => g.Bracket?.CompletedMatchesCount ?? 0);
        public int LeftMatchesCount => MatchesCount - CompletedMatchesCount;

        public ScheduleViewModel(IDiContainer container) : base(container)
        {

        }

        public override bool IsBackButtonAvailable => true;


        public override void InitData()
        {
            base.InitData();

            _quickButtons = null;
            _mats = DataContext.Tournament.Mats;

            if (_mats.Count == 0 || (Stats != null && _mats.Count != Stats.Count))
            {
                Stats = null;
            }

            var expandedMats = _filteredStats?.Where(x => x.IsExpanded).ToList();

            _filteredStats = GenerateStats();

            if (expandedMats?.Count > 0)
            {
                foreach (var item in expandedMats)
                {
                    var newMatData = _filteredStats.First(x => x.MatID == item.MatID);
                    newMatData.IsExpanded = item.IsExpanded;
                }
            }

            Filter(FilterString);

            if (SelectedMat == null && _filteredStats?.Count > 0)
            {
                SelectedMat = _filteredStats[0];
            }
            else if (_filteredStats?.Count > 0)
            {
                var newMat = _filteredStats.First(x => x.MatID == SelectedMat.MatID);
                SelectedMat = newMat;
            }

            // One-shot preselection wins over the previously-selected mat —
            // a Conducting card click should always land the operator on the
            // requested mat's queue.
            if (_preselectedMatId.HasValue && _filteredStats?.Count > 0)
            {
                var preselect = _filteredStats.FirstOrDefault(x => x.MatID == _preselectedMatId.Value);
                if (preselect != null) SelectedMat = preselect;
                _preselectedMatId = null;
            }

            // Enter hotkey starts the top startable match in the selected
            // mat's queue. Singleton VM, so guard with -=/+= to avoid
            // double subscription on revisits.
            if (_keyHandler == null) _keyHandler = Resolve<IKeyHandler>();
            if (_keyHandler != null)
            {
                _keyHandler.KeyPressed -= KeyHandler_KeyPressed;
                _keyHandler.KeyPressed += KeyHandler_KeyPressed;
            }
        }

        protected override void OnNavigatingOut()
        {
            base.OnNavigatingOut();
            if (_keyHandler != null)
            {
                _keyHandler.KeyPressed -= KeyHandler_KeyPressed;
            }
        }

        private void KeyHandler_KeyPressed(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;
            var topMatch = SelectedMat?.Matches?.FirstOrDefault(m => m != null && m.IsMatchCanStart);
            if (topMatch == null) return;
            OpenMatch(topMatch);
            e.Handled = true;
        }

        // Caller sets this immediately before NavigateToView<ScheduleViewModel>().
        // The next InitData consumes and clears it.
        public Guid? PreselectedMatId
        {
            get => _preselectedMatId;
            set => _preselectedMatId = value;
        }

        // Set by Phase 5 → Ковер wrapper before navigation. Switching
        // it forces a stats rebuild so the user sees only their mat's
        // queue. Null re-broadens to "all mats".
        public Guid? MatIdFilter
        {
            get => _matIdFilter;
            set
            {
                if (_matIdFilter == value) return;
                _matIdFilter = value;
                OnPropertyChanged(nameof(MatIdFilter));

                // Drop the cached Stats so the next Filter() call regenerates
                // through GenerateStats() with the new filter applied. If we
                // were never InitData'd yet, the next InitData call covers it.
                if (_mats != null)
                {
                    Stats = null;
                    Filter(FilterString);
                }
            }
        }

        public MatStats SelectedMat
        {
            get { return _selectedMat; }
            set
            {
                _selectedMat = value;

                OnPropertyChanged("SelectedMat");
            }
        }

        public string FilterString
        {
            get { return _filterString; }
            set
            {
                _filterString = value;
                OnPropertyChanged("FilterString");

                Filter(_filterString);
            }
        }
        
        public ObservableCollection<MatStats> Stats
        {
            get { return _filteredStats; }
            set
            {
                _filteredStats = value;

                OnPropertyChanged("Stats");
            }
        }

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                return _quickButtons ??
                       (
                           _quickButtons = new List<CommandButtonItem>
                           {
                               new CommandButtonItem(T("Schedule_OpenScoreScreen_Tooltip", "Открыть электронное табло"), PackIconKind.Monitor, new AsyncRelayCommand(_ => ShowScoreScreenAsync(), _ => true)),
                               new CommandButtonItem(T("Schedule_OpenBrackets_Tooltip", "Открыть турнирную сетку"), PackIconKind.Dns, new RelayCommand(param => OpenBrackets(), param => true)),
                           }
                       );
            }
        }

        #region Commands

        public ICommand ChangeMatCommand
        {
            get
            {
                if (_changeMatCommand == null)
                {
                    _changeMatCommand = new RelayCommand(param => ChangeMat(param as MatStats), param => param != null);
                }
                return _changeMatCommand;
            }
        }

        public ICommand OpenMatchCommand
        {
            get
            {
                if (_openMatchCommand == null)
                {
                    _openMatchCommand = new RelayCommand(param => OpenMatch(param as WrestlingMatch), param => param != null && (((WrestlingMatch)param).IsMatchCompleted || ((WrestlingMatch)param).IsMatchCanStart));
                }
                return _openMatchCommand;
            }
        }

        #endregion

        // Schedule is a fullscreen overlay launched from Conducting — back returns
        // to the admin landing. We don't read GetReturnVmType() here because
        // that mechanism feeds the match-overlay chain (MatchControl/Results),
        // which would loop us back into the just-completed match.
        protected override void OnBackCommand()
        {
            NavigateToView<Conducting.ConductingViewModel>();
        }

        private void ChangeMat(MatStats mat)
        {
            SelectedMat = mat;
        }

        private void Filter(string filter)
        {
            if (Stats == null || Stats.Count == 0)
            {
                var expandedMats = _filteredStats?.Where(x => x.IsExpanded).ToList();

                _filteredStats = GenerateStats();

                if (expandedMats != null && expandedMats.Count > 0)
                {
                    foreach (var item in expandedMats)
                    {
                        var newMatData = _filteredStats.FirstOrDefault(x => x.MatID == item.MatID);
                        if (newMatData != null) newMatData.IsExpanded = item.IsExpanded;
                    }
                }

                FilterStats(filter);
            }
            else
            {
                FilterStats(filter);
            }
        }

        private ObservableCollection<MatStats> GenerateStats()
        {
            var result = new ObservableCollection<MatStats>();

            var source = _matIdFilter.HasValue
                ? _mats.Where(c => c.ID == _matIdFilter.Value)
                : (IEnumerable<Mat>)_mats;

            foreach (var mat in source)
            {
                var matches = new ObservableCollection<WrestlingMatch>(mat.Groups.Where(g => g.Bracket != null)
                    .SelectMany(g => g.Bracket.Rounds).SelectMany(r => r.RoundMatches).OrderBy(m => m.MatchNumber));

                var stat = new MatStats
                {
                    MatID = mat.ID.Value,
                    MatLabel = mat.Name,
                    WrestlersCount = mat.WrestlersCount,
                    GroupsCount = mat.Groups.Count,
                    Matches = matches
                };

                if (stat.Matches.Count > 0)
                {
                    result.Add(stat);
                }
            }

            return result;
        }

        private void FilterStats(string filter)
        {
            // Mirrors registration-screen filter (TeamApplicationViewModel.Wrestlers):
            // case-insensitive substring match across wrestler FullName + team name + city,
            // skipped entirely until the user has typed at least 3 characters.
            var hasTextFilter = !string.IsNullOrEmpty(filter) && filter.Length > 2;

            foreach (var stat in Stats)
            {
                var mat = _mats.FirstOrDefault(c => c.ID == stat.MatID);

                if (mat == null) continue;

                stat.Matches = new ObservableCollection<WrestlingMatch>(mat.Groups.Where(x => x.Bracket != null).SelectMany(g => g.Bracket.Rounds)
                    .SelectMany(r => r.RoundMatches)
                    .Where(m => !hasTextFilter || MatchPassesFilter(m, filter))
                    .OrderBy(m => m.MatchNumber));

                if (hasTextFilter && stat.Matches.Count > 0)
                {
                    stat.IsExpanded = true;
                }
            }
        }

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

        private void OpenBrackets()
        {
            NavigateToView<BracketsViewModel>();
        }

        // Opens (or re-shows) the projector window. Moved here from
        // MatchControl so the operator can launch the score display once at
        // the start of the day from the mat queue and have it persist
        // across matches — no need to re-open it every time MatchControl is
        // entered. Both _scoreScreenView and _scoreScreen are DI singletons,
        // so the same instances stay alive throughout the session.
        private async Task ShowScoreScreenAsync()
        {
            if (_scoreScreenView == null) _scoreScreenView = Resolve<IPanelView>("ScoreScreen");
            if (_scoreScreen == null) _scoreScreen = Resolve<ScoreScreenViewModel>();
            if (_scoreScreenView == null || _scoreScreen == null) return;

            if (!_scoreScreenView.WasShown)
            {
                var monitor = await MonitorPicker.PickAsync();
                if (monitor == null) return;

                if (_scoreScreenView is PanelViewBase panel)
                {
                    panel.TargetMonitor = monitor;
                }
            }

            _scoreScreenView.ShowScreen(_scoreScreen);
        }

        private void OpenMatch(WrestlingMatch match)
        {
            if (match == null) return;

            if (match.Status == MatchStatusEnum.Completed)
            {
                DataContext.WrestlingMatch = match;
                DataContext.Group = DataContext.Tournament.Groups.FirstOrDefault(g => g.ID == match.GroupID);
                NavigateToView<MatchResultsViewModel>();
            }
            else if (match.IsMatchCanStart)
            {
                DataContext.WrestlingMatch = match;
                DataContext.Group = DataContext.Tournament.Groups.FirstOrDefault(g => g.ID == match.GroupID);
                NavigateToView<MatchControlViewModel>();
            }
        }
    }
}