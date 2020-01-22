using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Entities.Results;
using Wrestling.Entities.Results.Achievements;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Dashboard;
using Wrestling.UI.Material.Tournament.Print.PrintResults;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Results
{
    public partial class ResultsViewModel : TournamentViewModelBase
    {
        #region Fields

        private ITeamResultsCalculator _teamCalculator;

        private bool _isOnlyMedalsVisible;
        private string _filterString;

        private ICommand _printTeamResultsOlympicCommand;
        private ICommand _printTeamResultsMedalsCommand;
        private ICommand _printTeamResultsPointsCommand;
        private ICommand _printPersonalResultsCommand;
        private ICommand _printWrestlerAchievementsCommand;

        private List<TournamentResult> _allResults;
        private List<TournamentResult> _visibleResults;
        private List<TournamentTeamResult> _teamResults;
        private List<IGroupBracketProcessor> _barcketProcessors;

        private List<TournamentTeamResult> _olympicTeamResults;
        private List<TournamentTeamResult> _medalsTeamResults;
        private List<TournamentTeamResult> _pointsTeamResults;
        private List<WrestlerAchievement> _achievements;

        private IList<CommandButtonItem> _quickButtons;

        #endregion

        public ResultsViewModel(IDiContainer container) : base(container)
        {
        }

        #region Commands

        public ICommand PrintPersonalResultsCommand
        {
            get
            {
                if (_printPersonalResultsCommand == null)
                {
                    _printPersonalResultsCommand = new RelayCommand(param => PrintPersonalResults(), param => true);
                }
                return _printPersonalResultsCommand;
            }
        }

        public ICommand PrintTeamResultsOlympicCommand
        {
            get
            {
                if (_printTeamResultsOlympicCommand == null)
                {
                    _printTeamResultsOlympicCommand = new RelayCommand(param => PrintTeamResults(new PrintOlympicTeamResultsViewModel(DiContainer) {TeamResults = OlympicTeamResults}), param => true);
                }
                return _printTeamResultsOlympicCommand;
            }
        }

        public ICommand PrintTeamResultsMedalsCommand
        {
            get
            {
                if (_printTeamResultsMedalsCommand == null)
                {
                    _printTeamResultsMedalsCommand = new RelayCommand(param => PrintTeamResults(new PrintMedalsTeamResultsViewModel(DiContainer) { TeamResults = MedalsTeamResults }), param => true);
                }
                return _printTeamResultsMedalsCommand;
            }
        }

        public ICommand PrintTeamResultsPointsCommand
        {
            get
            {
                if (_printTeamResultsPointsCommand == null)
                {
                    _printTeamResultsPointsCommand = new RelayCommand(param => PrintTeamResults(new PrintPointsTeamResultsViewModel(DiContainer) { TeamResults = PointsTeamResults }), param => true);
                }
                return _printTeamResultsPointsCommand;
            }
        }

        public ICommand PrintWrestlerAchievementsCommand
        {
            get
            {
                if (_printWrestlerAchievementsCommand == null)
                {
                    _printWrestlerAchievementsCommand = new RelayCommand(param => PrintWrestlerAchievements(new PrintAchievementNominantsViewModel(DiContainer) { Results = Achievements }), param => true);
                }
                return _printWrestlerAchievementsCommand;
            }
        }

        #endregion

        #region Binding properties

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                return _quickButtons ??
                       (
                           _quickButtons = new List<CommandButtonItem>
                           {
                               new CommandButtonItem("Просмотреть завершенные поединки", PackIconKind.CalendarCheck, new RelayCommand(param => OpenCompleted(), param => true))
                           }
                       );
            }
        }

        public override bool IsBackButtonAvailable => true;

        public override string PageTitle => "Итоги Соревнований";

        public string FilterString
        {
            get { return _filterString; }
            set
            {
                var prevValue = _filterString;
                if (_filterString != value)
                {
                    _filterString = value;
                    OnPropertyChanged("FilterString");

                    if (prevValue != null && prevValue.Length > 2 && _filterString.Length == 0 || _filterString.Length > 2)
                    {
                        PersonalResults = GetVisibleResults(_allResults, IsOnlyMedalsVisible, FilterString);
                    }
                }
            }
        }

        public List<WrestlerAchievement> Achievements
        {
            get { return _achievements; }
            set
            {
                _achievements = value;
                OnPropertyChanged("Achievements");
            }
        }

        public List<TournamentResult> PersonalResults
        {
            get { return _visibleResults; }
            set
            {
                _visibleResults = value;
                OnPropertyChanged("PersonalResults");
            }
        }

        public bool IsOnlyMedalsVisible
        {
            get { return _isOnlyMedalsVisible; }
            set
            {
                _isOnlyMedalsVisible = value;

                PersonalResults = GetVisibleResults(_allResults, IsOnlyMedalsVisible, FilterString);

                OnPropertyChanged("IsOnlyMedalsVisible");
            }
        }

        public List<TournamentTeamResult> PointsTeamResults
        {
            get { return _pointsTeamResults; }
            set
            {
                _pointsTeamResults = value;
                OnPropertyChanged("PointsTeamResults");
            }
        }

        public List<TournamentTeamResult> MedalsTeamResults
        {
            get { return _medalsTeamResults; }
            set
            {
                _medalsTeamResults = value;
                OnPropertyChanged("MedalsTeamResults");
            }
        }

        public List<TournamentTeamResult> OlympicTeamResults
        {
            get { return _olympicTeamResults; }
            set
            {
                _olympicTeamResults = value;
                OnPropertyChanged("OlympicTeamResults");
            }
        }

        #endregion

        public override void InitData()
        {
            base.InitData();

            _teamCalculator = Resolve<ITeamResultsCalculator>();

            _barcketProcessors = Resolve<List<IGroupBracketProcessor>>();

            _allResults = CalculateAllResults(DataContext.Tournament);

            _teamResults = _teamCalculator.GetTeamResults(_allResults, null);

            PersonalResults = GetVisibleResults(_allResults, IsOnlyMedalsVisible, FilterString);

            OlympicTeamResults = Resolve<ITeamResultsOrderer>("OlympicOrderer").GetOrderedResults(_teamResults);
            MedalsTeamResults = Resolve<ITeamResultsOrderer>("MedalsOrderer").GetOrderedResults(_teamResults);
            PointsTeamResults = Resolve<ITeamResultsOrderer>("PointsOrderer").GetOrderedResults(_teamResults);

            Achievements = CalculateAchievements(DataContext.Tournament, _allResults);
        }

        protected override void OnBackCommand()
        {
            NavigateToView<DashboardViewModel>();
        }

        #region Private methods

        private void OpenCompleted()
        {
            DataContext.IsBracketView = false;

            NavigateToView<CompletedMatchesViewModel>();
        }

        private void PrintTeamResults(PrintTeamResultsViewModel vm)
        {
            ShowPrintPreview(vm);
        }

        private void PrintWrestlerAchievements(PrintAchievementNominantsViewModel vm)
        {
            ShowPrintPreview(vm);
        }

        private void PrintPersonalResults()
        {
            ShowPrintPreview(new PrintPersonalResultsViewModel(DiContainer) {Results = PersonalResults});
        }

        private List<WrestlerAchievement> CalculateAchievements(Entities.Tournament tournament, List<TournamentResult> allResults)
        {
            var achievements = new List<WrestlerAchievement>();

            var calculators = Resolve<List<IAchievementCalculator>>();

            foreach (var calc in calculators)
            {
                var results = calc.CalculateAchievement(tournament, allResults);

                if (results != null && results.Count > 0)
                {
                    achievements.AddRange(results);
                }
            }

            return achievements;
        }

        private List<TournamentResult> CalculateAllResults(Entities.Tournament tournament)
        {
            var tmpResults = new List<TournamentResult>();

            foreach (var group in tournament.Groups)
            {
                if (group.Bracket == null) continue;

                var processor = _barcketProcessors.FirstOrDefault(p => p.Code == group.Bracket.BracketTypeCode);
                if (processor == null) throw new ApplicationException("Can't find processor!");

                processor.Load(DataContext.Tournament, group);
                var results = processor.GetResults();
                if (results != null)
                {
                    tmpResults.AddRange(results);
                }
            }

            return new List<TournamentResult>(tmpResults                
                .OrderBy(x => x.Group.Name)
                .ThenBy(p => p.Wrestler.FinalPlace));
        }

        private List<TournamentResult> GetVisibleResults(List<TournamentResult> allResults, bool getMedalistsOnly, string filterString)
        {
            return new List<TournamentResult>(allResults
                .Where(w => (!getMedalistsOnly || w.Wrestler.FinalPlace <= 3) && (string.IsNullOrEmpty(filterString) || w.Wrestler.LastName.StartsWith(filterString, true, CultureInfo.InvariantCulture)))
                .OrderBy(x => x.Group.Name).ThenBy(p => p.Wrestler.FinalPlace));
        }

        #endregion
    }
}