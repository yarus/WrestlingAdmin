using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.UI.Material.Match;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Progress.Brackets;
using Wrestling.UI.Material.Tournament.Results.PersonalResults;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Tournament.Results
{
    public class CompletedMatchesViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        // T inherited from TournamentViewModelBase.
        public string PageName => T("Completed_PageName", "Журнал матчей");


        private ICommand _openMatchCommand;

        private ObservableCollection<Mat> _mats;
        private ObservableCollection<MatStats> _filteredStats;

        private IList<CommandButtonItem> _quickButtons;
        
        private string _filterString;
        
        public override string PageTitle => T("Completed_PageTitle", "Завершенные поединки");

        public int MatsCount => DataContext.Tournament.Mats.Count;
        public int MatchesCount => DataContext.Tournament.Groups.Sum(g => g.Bracket?.MatchesCount ?? 0);
        public int CompletedMatchesCount => DataContext.Tournament.Groups.Sum(g => g.Bracket?.CompletedMatchesCount ?? 0);

        public CompletedMatchesViewModel(IDiContainer container) : base(container)
        {

        }

        public override bool IsBackButtonAvailable => true;

        public override void InitData()
        {
            base.InitData();

            _quickButtons = null;
            Stats = null;

            _mats = DataContext.Tournament.Mats;

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

        public ObservableCollection<MatStats> Stats
        {
            get { return _filteredStats; }
            set
            {
                _filteredStats = value;

                OnPropertyChanged("Stats");
            }
        }

        public override IList<CommandButtonItem> QuickButtons =>
            _quickButtons ?? (_quickButtons = new List<CommandButtonItem>());

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

        // Back-command no-op in the new shell — CompletedMatches is hosted
        // inside ResultsViewModel which itself sets IsBackButtonAvailable=false.
        
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

        private ObservableCollection<MatStats> GenerateStats()
        {
            var result = new ObservableCollection<MatStats>();

            foreach (var mat in _mats)
            {
                var groupsWithBrackets = mat.Groups.Where(x => x.Bracket != null).SelectMany(g => g.Bracket.Rounds);

                var matches = new ObservableCollection<WrestlingMatch>(groupsWithBrackets.SelectMany(r => r.RoundMatches).OrderBy(m => m.MatchNumber));

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
            foreach (var stat in _filteredStats)
            {
                var mat = _mats.First(c => c.ID == stat.MatID);

                var groupsWithBrackets = mat.Groups.Where(x => x.Bracket != null).SelectMany(g => g.Bracket.Rounds);

                stat.Matches = new ObservableCollection<WrestlingMatch>(groupsWithBrackets
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
    }
}