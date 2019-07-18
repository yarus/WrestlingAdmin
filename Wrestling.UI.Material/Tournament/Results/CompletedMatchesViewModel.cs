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
using Wrestling.UI.Material.Tournament.Progress.Brackets;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Results
{
    public class CompletedMatchesViewModel : TournamentViewModelBase
    {
        private ICommand _openMatchCommand;

        private ObservableCollection<Carpet> _carpets;
        private ObservableCollection<CarpetStats> _filteredStats;

        private IList<CommandButtonItem> _quickButtons;
        
        private string _filterString;
        
        public override string PageTitle => "Завершенные поединки";

        public int CarpetsCount => DataContext.Tournament.Carpets.Count;
        public int MatchesCount => DataContext.Tournament.Groups.Sum(g => g.Bracket?.MatchesCount ?? 0);
        public int CompletedMatchesCount => DataContext.Tournament.Groups.Sum(g => g.Bracket?.CompletedMatchesCount ?? 0);

        public CompletedMatchesViewModel(IDiContainer container) : base(container)
        {

        }

        public override bool IsBackButtonAvailable => true;

        public override void InitData()
        {
            base.InitData();

            Stats = null;

            _carpets = DataContext.Tournament.Carpets;

            Filter(FilterString);
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
                               new CommandButtonItem("Перейти к итоговой таблице", PackIconKind.Trophy, new RelayCommand(param => OpenResults(), param => true))
                           }
                       );
            }
        }

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

        protected override void OnBackCommand()
        {
            if (DataContext.IsBracketView)
            {
                NavigateToView<BracketsViewModel>();
            }
            else
            {
                NavigateToView<DashboardViewModel>();
            }
        }
        
        private void Filter(string filter)
        {
            if (Stats == null)
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
            foreach (var stat in _filteredStats)
            {
                var carpet = _carpets.First(c => c.ID == stat.CarpetID);

                stat.Matches = new ObservableCollection<WrestlingMatch>(carpet.Groups.SelectMany(g => g.Bracket.Rounds)
                    .SelectMany(r => r.RoundMatches)
                    .Where(m => (m.IsMatchCompleted)
                                && (string.IsNullOrEmpty(filter) ||
                                    (m.WrestlerInRed != null && m.WrestlerInRed.LastName.StartsWith(filter, true, CultureInfo.InvariantCulture)) ||
                                    (m.WrestlerInBlue != null && m.WrestlerInBlue.LastName.StartsWith(filter, true, CultureInfo.InvariantCulture))))
                    .OrderBy(m => m.MatchNumber));

                if (!string.IsNullOrEmpty(filter) && stat.Matches.Count > 0)
                {
                    stat.IsExpanded = true;
                }
            }
        }

        private void OpenMatch(WrestlingMatch match)
        {
            if (match?.Status == MatchStatusEnum.Completed)
            {
                DataContext.WrestlingMatch = match;
                DataContext.Group = DataContext.Tournament.Groups.FirstOrDefault(g => g.ID == match.GroupID);
                NavigateToView<MatchResultsViewModel>();
            }
        }

        private void OpenResults()
        {
            NavigateToView<ResultsViewModel>();
        }
    }
}