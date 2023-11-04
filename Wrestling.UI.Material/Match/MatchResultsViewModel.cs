using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Material.Tournament.Progress.Brackets;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Wrestling.UI.Material.Tournament.Results;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Match
{
    public class MatchResultsViewModel : TournamentViewModelBase
    {
        #region Fields

        private IList<CommandButtonItem> _quickButtons;
        private ScoreScreenViewModel _scoreScreenVm;
        private GlobalSettings _settings;
        private readonly List<IGroupBracketProcessor> _drawTypes;
        private IGroupBracketProcessor _processor;

        #endregion

        public MatchResultsViewModel(IDiContainer container) : base(container)
        {
            _drawTypes = Resolve<List<IGroupBracketProcessor>>();
        }

        public override string PageTitle => "Результаты поединка";

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                if (_quickButtons == null)
                {
                    _quickButtons = new List<CommandButtonItem>();

                    var settings = DataContext.Tournament != null ? DataContext.Tournament.Settings : GlobalSettings;

                    if (WrestlingMatch != null && WrestlingMatch.Status == MatchStatusEnum.Completed && CanRejectResult)
                    {
                        _quickButtons.Add(new CommandButtonItem("Анулировать", PackIconKind.BlockHelper,
                            new RelayCommand(param => Reject(),
                                param => WrestlingMatch != null && WrestlingMatch.Status == MatchStatusEnum.Completed &&
                                         CanRejectResult)));

                        if (settings.IsVideoRecordingEnabled)
                        {
                            _quickButtons.Add(
                                new CommandButtonItem("Открыть запись", PackIconKind.Camcorder,
                                    new RelayCommand(param => ShowReplayScreen(),
                                        param => WrestlingMatch != null &&
                                                 WrestlingMatch.Status == MatchStatusEnum.Completed)));
                        }
                    }

                    if (WrestlingMatch != null && WrestlingMatch.Status == MatchStatusEnum.Pending)
                    {
                        _quickButtons.Add(new CommandButtonItem("Подтвердить", PackIconKind.CheckAll,
                            new RelayCommand(param => Approve(),
                                param => WrestlingMatch != null && WrestlingMatch.Status == MatchStatusEnum.Pending &&
                                         Winner.HasValue && WinType.HasValue)));
                    }
                }

                return _quickButtons;
            }
        }        

        public override void InitData()
        {
            base.InitData();

            _scoreScreenVm = Resolve<ScoreScreenViewModel>();

            if (WrestlingMatch == null) throw new ApplicationException("Матч не создан!");

            if (Tournament == null)
            {
                WrestlingMatch.RoundName = _scoreScreenVm.RoundName;
                WrestlingMatch.MatchNumber = Convert.ToInt32(_scoreScreenVm.MatchFullNumber);
                WrestlingMatch.WrestlerInRed.TeamName = _scoreScreenVm.Wrestler1TeamName;
                WrestlingMatch.WrestlerInBlue.TeamName = _scoreScreenVm.Wrestler2TeamName;
            }

            _quickButtons = null;

            SetWinnerAndWinType();

            if (DataContext.Group?.Bracket != null)
            {
                _processor = GetProcessoryForGroup(DataContext.Group.Bracket.BracketTypeCode);
                if (_processor == null) throw new ApplicationException("Can't find processor!");

                _processor.Load(DataContext.Tournament, DataContext.Group);
                CanRejectResult = _processor.CanMatchBeReverted(WrestlingMatch);
            }

            _settings = DataContext.Tournament != null ? DataContext.Tournament.Settings : GlobalSettings;

            if (DataContext.WrestlingMatch.WrestlerInRed.TeamID.HasValue && DataContext.Tournament != null)
            {
                Wrestler1TeamEmblem = GetTeamEmblem(DataContext.WrestlingMatch.WrestlerInRed.TeamID.Value, DataContext.Tournament.TeamApplications);
            }

            if (DataContext.WrestlingMatch.WrestlerInBlue.TeamID.HasValue && DataContext.Tournament != null)
            {
                Wrestler2TeamEmblem = GetTeamEmblem(DataContext.WrestlingMatch.WrestlerInBlue.TeamID.Value, DataContext.Tournament.TeamApplications);
            }

            IsFormEnabled = true;
        }

        public override bool IsBackButtonAvailable => true;

        protected override void OnBackCommand()
        {
            if (WrestlingMatch.Status != MatchStatusEnum.Completed)
            {
                Note = string.Empty;

                NavigateToView<MatchControlViewModel>();
            }
            else
            {
                WrestlingMatch = null;
                Note = string.Empty;

                if (Tournament == null)
                {
                    NavigateToView<HomeViewModel>();
                }
                else if (DataContext.IsBracketView)
                {
                    NavigateToView<BracketsViewModel>();
                }
                else
                {
                    NavigateToView<CompletedMatchesViewModel>();
                }
            }
        }

        #region Commands

        private ICommand _setWinnerCommand;
        public ICommand SetWinnerCommand
        {
            get
            {
                if (_setWinnerCommand == null)
                {
                    _setWinnerCommand = new RelayCommand(
                        param => SetWinner(param.ToString()),
                        param => true
                    );
                }
                return _setWinnerCommand;
            }
        }

        private ICommand _setWinTypeCommand;
        public ICommand SetWinTypeCommand
        {
            get
            {
                if (_setWinTypeCommand == null)
                {
                    _setWinTypeCommand = new RelayCommand(param => SetWinType(), param => true);
                }

                return _setWinTypeCommand;
            }
        }

        private ICommand _redSelectedCommand;
        public ICommand RedSelectedCommand
        {
            get
            {
                if (_redSelectedCommand == null)
                {
                    _redSelectedCommand = new RelayCommand(param => ChangeWrestlerSelected(true, true), param => true);
                }

                return _redSelectedCommand;
            }
        }

        private ICommand _redDeselectedCommand;
        public ICommand RedDeselectedCommand
        {
            get
            {
                if (_redDeselectedCommand == null)
                {
                    _redDeselectedCommand = new RelayCommand(param => ChangeWrestlerSelected(true, false), param => true);
                }

                return _redDeselectedCommand;
            }
        }

        private ICommand _blueSelectedCommand;
        public ICommand BlueSelectedCommand
        {
            get
            {
                if (_blueSelectedCommand == null)
                {
                    _blueSelectedCommand = new RelayCommand(param => ChangeWrestlerSelected(false, true), param => true);
                }

                return _blueSelectedCommand;
            }
        }

        private ICommand _blueDeselectedCommand;
        public ICommand BlueDeselectedCommand
        {
            get
            {
                if (_blueDeselectedCommand == null)
                {
                    _blueDeselectedCommand = new RelayCommand(param => ChangeWrestlerSelected(false, false), param => true);
                }

                return _blueDeselectedCommand;
            }
        }

        #endregion        

        #region Binding Properties

        public bool CanRejectResult { get; private set; }
        public bool IsWinTypeSet => WinType.HasValue;
        private bool IsMatchStarted => WrestlingMatch.LastSecondInMatch > 0;
        private bool IsMatchCompletedInTime => WrestlingMatch.LastSecondInMatch == WrestlingMatch.MaxRoundSecond * 2;
        public bool IsFreeWinEnabled => WrestlingMatch.WinType == MatchWinTypeEnum.FreeWin;
        public bool IsPointsWinEnabled => !IsMatchStarted || IsMatchCompletedInTime;
        public bool IsActionWinEnabled => IsMatchCompletedInTime && WrestlingMatch.PointsBlue == WrestlingMatch.PointsRed;
        public bool IsDominationWinEnabled => WrestlingMatch.PointsBlue - WrestlingMatch.PointsRed >= 10 || WrestlingMatch.PointsRed - WrestlingMatch.PointsBlue >= 10;
        public bool IsTusheWinEnabled => !IsFreeWinEnabled;
        public bool IsTechWinEnabled => !IsFreeWinEnabled;
        public bool IsWinnerRed => Winner.HasValue && WrestlingMatch.WrestlerInRed != null && Winner.Value == WrestlingMatch.WrestlerInRed.ID;
        public bool IsWinnerBlue => Winner.HasValue && WrestlingMatch.WrestlerInBlue != null && Winner.Value == WrestlingMatch.WrestlerInBlue.ID;

        private Guid? _winner;
        public Guid? Winner
        {
            get { return _winner; }
            set
            {
                _winner = value;

                OnPropertyChanged("Winner");
                OnPropertyChanged("IsWinnerRed");
                OnPropertyChanged("IsWinnerBlue");
            }
        }

        private bool _isPlayer1WithAdvantage;
        public bool IsPlayer1WithAdvantage
        {
            get { return _isPlayer1WithAdvantage; }
            set
            {
                _isPlayer1WithAdvantage = value;
                OnPropertyChanged("IsPlayer1WithAdvantage");
            }
        }

        private bool _isPlayer2WithAdvantage;
        public bool IsPlayer2WithAdvantage
        {
            get { return _isPlayer2WithAdvantage; }
            set
            {
                _isPlayer2WithAdvantage = value;
                OnPropertyChanged("IsPlayer2WithAdvantage");
            }
        }

        private bool _isRedSelected;
        public bool IsRedSelected
        {
            get { return _isRedSelected; }
            set
            {
                _isRedSelected = value;
                OnPropertyChanged("IsRedSelected");
            }
        }

        private bool _isBlueSelected;
        public bool IsBlueSelected
        {
            get { return _isBlueSelected; }
            set
            {
                _isBlueSelected = value;
                OnPropertyChanged("IsBlueSelected");
            }
        }

        private string _wrestler2TeamEmblem;
        public string Wrestler2TeamEmblem
        {
            get { return _wrestler2TeamEmblem; }
            set
            {
                _wrestler2TeamEmblem = value;
                OnPropertyChanged("Wrestler2TeamEmblem");
            }
        }

        private string _wrestler1TeamEmblem;
        public string Wrestler1TeamEmblem
        {
            get { return _wrestler1TeamEmblem; }
            set
            {
                _wrestler1TeamEmblem = value;
                OnPropertyChanged("Wrestler1TeamEmblem");
            }
        }

        private MatchWinTypeEnum? _winType;
        public MatchWinTypeEnum? WinType
        {
            get { return _winType; }
            set
            {
                _winType = value;

                OnPropertyChanged("WinType");
                OnPropertyChanged("IsWinTypeSet");
            }
        }

        private bool _isFormEnabled;
        public bool IsFormEnabled
        {
            get { return _isFormEnabled; }
            set
            {
                _isFormEnabled = value;

                OnPropertyChanged("IsFormEnabled");
            }
        }

        private string _note;
        public string Note
        {
            get { return _note; }
            set
            {
                _note = value;

                OnPropertyChanged("Note");
            }
        }

        public WrestlingMatch WrestlingMatch
        {
            get { return DataContext.WrestlingMatch; }
            set
            {
                DataContext.WrestlingMatch = value;                

                OnPropertyChanged("WrestlingMatch");
            }
        }

        #endregion

        #region Private Methods

        private void ChangeWrestlerSelected(bool isRed, bool isSelected)
        {
            if (WrestlingMatch.IsMatchCompleted)
            {
                return;
            }

            if (isRed)
            {
                IsRedSelected = isSelected;

                if (isSelected)
                {
                    IsBlueSelected = !isSelected;
                }
            }
            else
            {
                IsBlueSelected = isSelected;

                if (isSelected)
                {
                    IsRedSelected = !isSelected;
                }
            }
        }

        private void ShowReplayScreen()
        {
        }

        private async void SetWinType()
        {
            var availableWinTypes = new List<MatchWinTypeEnum>();

            if (IsTusheWinEnabled) availableWinTypes.Add(MatchWinTypeEnum.Tushe);
            if (IsDominationWinEnabled) availableWinTypes.Add(MatchWinTypeEnum.DominationWin);
            if (IsPointsWinEnabled) availableWinTypes.Add(MatchWinTypeEnum.PointsWin);
            if (IsTechWinEnabled) availableWinTypes.Add(MatchWinTypeEnum.DisqualifyWin);
            if (IsActionWinEnabled) availableWinTypes.Add(MatchWinTypeEnum.ActionWin);

            var vm = new SetWinTypeViewModel(DiContainer, WinType, availableWinTypes);
            vm.InitData();

            var view = new SetWinTypeDialog
            {
                DataContext = vm
            };

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                WinType = vm.SelectedItem;
            }

            OnPropertyChanged("IsWinTypeSet");
        }

        private void SetWinner(string winner)
        {
            if (WrestlingMatch.IsMatchCompleted) return;
            if (string.IsNullOrEmpty(winner)) return;

            if (winner == "Red")
            {
                if (Winner == null || Winner != WrestlingMatch.WrestlerInRed.ID)
                {
                    Winner = WrestlingMatch.WrestlerInRed.ID;
                }
                else
                {
                    Winner = null;
                }
            }
            if (winner == "Blue")
            {
                if (Winner == null || Winner != WrestlingMatch.WrestlerInBlue.ID)
                {
                    Winner = WrestlingMatch.WrestlerInBlue.ID;
                }
                else
                {
                    Winner = null;
                }
            }
        }

        private string GetTeamEmblem(Guid teamID, IEnumerable<TeamApplication> apps)
        {
            var team = apps.FirstOrDefault(t => t.ID == teamID);

            return team != null ? team.EmblemPath : string.Empty;
        }


        private void Reject()
        {
            if (Dialog.ShowMessageBox(this,
                    "Результат матча будет анулирован и сетка перестроена! Вы уверены?",
                    "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

            if (DataContext.Group?.Bracket != null)
            {
                _processor.RevertMatch(WrestlingMatch);
            }
            else
            {
                WrestlingMatch.Status = MatchStatusEnum.Pending;
                WrestlingMatch.LastSecondInMatch = 0;
                WrestlingMatch.PointsBlue = 0;
                WrestlingMatch.PointsRed = 0;
                WrestlingMatch.StartDateTime = null;
            }

            WrestlingMatch = null;
            WinType = null;
            Note = string.Empty;

            if (Tournament == null)
            {
                NavigateToView<HomeViewModel>();
            }
            else
            {
                if (DataContext.IsBracketView)
                {
                    NavigateToView<BracketsViewModel>();
                }
                else
                {
                    NavigateToView<ScheduleViewModel>();
                }
            }
        }

        private void Approve()
        {
            if (WrestlingMatch.StartDateTime == null)
            {
                WrestlingMatch.StartDateTime = Tournament?.StartDate ?? DateTime.Now;
            }

            WrestlingMatch.IsRedWon = Winner == WrestlingMatch.WrestlerInRed.ID;
            WrestlingMatch.WinType = WinType;
            WrestlingMatch.Note = Note;
            WrestlingMatch.MatchActions.Add(new MatchAction
            {
                DateTime = DateTime.Now,
                RoundNumber = WrestlingMatch.LastSecondInMatch > WrestlingMatch.MaxRoundSecond ? 2 : 1,
                SecondInRound = WrestlingMatch.LastSecondInMatch > WrestlingMatch.MaxRoundSecond ? WrestlingMatch.LastSecondInMatch - WrestlingMatch.MaxRoundSecond : WrestlingMatch.LastSecondInMatch,
                Text = "Матч завершен"
            });

            CompleteMatch();

            if (_settings.IsVideoRecordingEnabled)
            {
                IsFormEnabled = false;
            } 
            else
            {
                if (DataContext.Tournament != null && WrestlingMatch.WinType.HasValue)
                {
                    NavigateToMatches();
                }
                else
                {
                    BackToNavigateToHome();
                }
            }
        }

        private void OnRecordingCompleted(object sender, string e)
        {
            IsFormEnabled = true;

            ShowSnackMessage(e);

            if (DataContext.Tournament != null && WrestlingMatch.WinType.HasValue)
            {
                NavigateToMatches();
            }
            else
            {
                BackToNavigateToHome();
            }
        }

        private void CompleteMatch()
        {
            if (!WrestlingMatch.IsRedWon.HasValue || !WrestlingMatch.WinType.HasValue) throw new ApplicationException("Completed match does not have result provided!");

            if (DataContext.Tournament != null)
            {
                _processor.CompleteMatch(WrestlingMatch, WrestlingMatch.IsRedWon.Value, WrestlingMatch.WinType.Value);

                _scoreScreenVm.ShowWinner(WrestlingMatch);
            }
        }

        private void NavigateToMatches()
        {
            if (DataContext.IsBracketView)
            {
                NavigateToView<BracketsViewModel>();
            }
            else
            {
                NavigateToView<ScheduleViewModel>();
            }
        }

        private void BackToNavigateToHome()
        {
            WrestlingMatch.IsRedWon = Winner == WrestlingMatch.WrestlerInRed.ID;
            WrestlingMatch.WinType = WinType;
            WrestlingMatch.Note = Note;
            WrestlingMatch.Status = MatchStatusEnum.Completed;
            WrestlingMatch = null;

            NavigateToView<HomeViewModel>();
        }

        private IGroupBracketProcessor GetProcessoryForGroup(string processorType)
        {
            return _drawTypes.FirstOrDefault(p => p.Code == processorType);
        }

        private void SetWinnerAndWinType()
        {
            Note = string.Empty;
            WinType = null;
            Winner = null;
            IsPlayer1WithAdvantage = false;
            IsPlayer2WithAdvantage = false;

            if (WrestlingMatch.Status == MatchStatusEnum.Completed)
            {
                if (!WrestlingMatch.IsRedWon.HasValue || !WrestlingMatch.WinType.HasValue) throw new ApplicationException("Completed match does not have result provided!");

                WinType = WrestlingMatch.WinType.Value;

                if (WrestlingMatch.IsRedWon.Value && WrestlingMatch.WrestlerInRed != null)
                {
                    Winner = WrestlingMatch.WrestlerInRed.ID;
                }
                else if (!WrestlingMatch.IsRedWon.Value && WrestlingMatch.WrestlerInBlue != null)
                {
                    Winner = WrestlingMatch.WrestlerInBlue.ID;
                }

                Note = WrestlingMatch.Note;
            }
            else
            {
                Winner = null;

                if (IsMatchCompletedInTime)
                {
                    if (WrestlingMatch.PointsRed > WrestlingMatch.PointsBlue)
                    {
                        Winner = WrestlingMatch.WrestlerInRed.ID;
                        WinType = MatchWinTypeEnum.PointsWin;
                    }
                    else if (WrestlingMatch.PointsBlue > WrestlingMatch.PointsRed)
                    {
                        Winner = WrestlingMatch.WrestlerInBlue.ID;
                        WinType = MatchWinTypeEnum.PointsWin;
                    }
                    else
                    {
                        if (WrestlingMatch.BestActionRed != WrestlingMatch.BestActionBlue)
                        {
                            IsPlayer1WithAdvantage = WrestlingMatch.BestActionRed > WrestlingMatch.BestActionBlue;
                            IsPlayer2WithAdvantage = WrestlingMatch.BestActionRed < WrestlingMatch.BestActionBlue;
                            Winner = WrestlingMatch.BestActionRed > WrestlingMatch.BestActionBlue ? WrestlingMatch.WrestlerInRed.ID : WrestlingMatch.WrestlerInBlue.ID;
                            Note = "Победа присуждена по качеству результативного действия.";
                        }
                        else
                        {
                            IsPlayer1WithAdvantage = WrestlingMatch.IsLastActionRed;
                            IsPlayer2WithAdvantage = !WrestlingMatch.IsLastActionRed;
                            Winner = WrestlingMatch.IsLastActionRed ? WrestlingMatch.WrestlerInRed.ID : WrestlingMatch.WrestlerInBlue.ID;
                            Note = "При равном счете и равном качестве результативных действий победа присуждена по последнему действию.";
                        }

                        WinType = MatchWinTypeEnum.ActionWin;
                    }
                }

                if (Winner == null && WinType == null)
                {
                    if (WrestlingMatch.PointsRed - WrestlingMatch.PointsBlue >= 10)
                    {
                        Winner = WrestlingMatch.WrestlerInRed.ID;
                        WinType = MatchWinTypeEnum.DominationWin;
                    }
                    else if (WrestlingMatch.PointsBlue - WrestlingMatch.PointsRed >= 10)
                    {
                        Winner = WrestlingMatch.WrestlerInBlue.ID;
                        WinType = MatchWinTypeEnum.DominationWin;
                    }
                    else
                    {
                        WinType = MatchWinTypeEnum.Tushe;
                    }
                }
            }
        }

        #endregion
    } 
}