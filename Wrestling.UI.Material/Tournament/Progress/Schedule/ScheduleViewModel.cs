using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.UI.Material.Match;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Dashboard;
using Wrestling.UI.Material.Tournament.Print.PrintSchedule;
using Wrestling.UI.Material.Tournament.Progress.Brackets;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Progress.Schedule
{
    public class ScheduleViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        private ObservableCollection<Carpet> _carpets;
        private ObservableCollection<CarpetStats> _filteredStats;

        private ICommand _openMatchCommand;
        private ICommand _printProtocolCommand;

        private IList<CommandButtonItem> _quickButtons;
        
        private string _filterString;

        public string PageName => "Расписание";
        public override string PageTitle => "Расписание схваток по коврам";

        public int CarpetsCount => DataContext.Tournament.Carpets.Count;
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

            _carpets = DataContext.Tournament.Carpets;

            Filter(FilterString);

            DataContext.IsBracketView = false;
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
        
        public ObservableCollection<CarpetStats> Stats
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
                               new CommandButtonItem("Открыть турнирную сетку", PackIconKind.Dns, new RelayCommand(param => OpenBrackets(), param => true))
                           }
                       );
            }
        }

        #region Commands

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


        public ICommand PrintProtocolCommand
        {
            get
            {
                if (_printProtocolCommand == null)
                {
                    _printProtocolCommand = new RelayCommand(param => PrintSchedule((Guid)param), param => param != null);
                }
                return _printProtocolCommand;
            }
        }

        #endregion

        protected override void OnBackCommand()
        {
            NavigateToView<DashboardViewModel>();
        }

        private void PrintSchedule(Guid carpetID)
        {
            ShowPrintPreview(new PrintScheduleViewModel(DiContainer, _carpets.FirstOrDefault(c => c.ID == carpetID)));
        }

        private void Filter(string filter)
        {
            if (Stats == null || Stats.Count == 0)
            {
                _filteredStats = GenerateStats();
                FilterStats(filter);
            }
            else
            {
                FilterStats(filter);
            }
        }

        private ObservableCollection<CarpetStats> GenerateStats()
        {
            var result = new ObservableCollection<CarpetStats>();

            foreach (var carpet in _carpets)
            {
                var matches = new ObservableCollection<WrestlingMatch>(carpet.Groups
                    .SelectMany(g => g.Bracket.Rounds).SelectMany(r => r.RoundMatches).OrderBy(m => m.MatchNumber));

                var stat = new CarpetStats
                {
                    CarpetID = carpet.ID.Value,
                    CarpetLabel = carpet.Name,
                    WrestlersCount = carpet.WrestlersCount,
                    GroupsCount = carpet.Groups.Count,
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
            foreach (var stat in Stats)
            {
                var carpet = _carpets.FirstOrDefault(c => c.ID == stat.CarpetID);

                if (carpet == null) continue;

                stat.Matches = new ObservableCollection<WrestlingMatch>(carpet.Groups.SelectMany(g => g.Bracket.Rounds)
                    .SelectMany(r => r.RoundMatches)
                    .Where(m => m.IsMatchCanStart && (string.IsNullOrEmpty(filter) ||
                                    (m.WrestlerInRed != null && m.WrestlerInRed.LastName.StartsWith(filter, true, CultureInfo.InvariantCulture)) ||
                                    (m.WrestlerInBlue != null && m.WrestlerInBlue.LastName.StartsWith(filter, true, CultureInfo.InvariantCulture))))
                    .OrderBy(m => m.MatchNumber));

                if (!string.IsNullOrEmpty(filter) && stat.Matches.Count > 0)
                {
                    stat.IsExpanded = true;
                }
            }
        }

        private void OpenBrackets()
        {
            NavigateToView<BracketsViewModel>();
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