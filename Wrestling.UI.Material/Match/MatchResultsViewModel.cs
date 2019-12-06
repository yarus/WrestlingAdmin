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
using Wrestling.UI.Material.ReplayScreen;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Material.Tournament.Progress.Brackets;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Wrestling.UI.Material.Tournament.Results;
using Wrestling.UI.Material.Utils.Recording;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Match
{
    public class MatchResultsViewModel : TournamentViewModelBase
    {
        private readonly IMatchRecorder _recorder;

        private ScoreScreenViewModel _scoreScreenVm;
        private IList<CommandButtonItem> _quickButtons;

        private Guid? _winner;
        private MatchWinTypeEnum? _winType;
        private string _note;
        private bool _isFormEnabled;

        private readonly List<IGroupBracketProcessor> _drawTypes;
        private IGroupBracketProcessor _processor;

        private ICommand _setWinnerCommand;

        private GlobalSettings _settings;

        public MatchResultsViewModel(IDiContainer container) : base(container)
        {
            _drawTypes = Resolve<List<IGroupBracketProcessor>>();
            _recorder = Resolve<IMatchRecorder>();
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
                                         Winner.HasValue)));
                    }
                }

                return _quickButtons;
            }
        }

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

        private void ShowReplayScreen()
        {
            NavigateToView<ReplayScreenViewModel>();
        }

        private void SetWinner(string winner)
        {
            if (string.IsNullOrEmpty(winner)) return;

            if (winner == "Red") Winner = WrestlingMatch.WrestlerInRed.ID;
            if (winner == "Blue") Winner = WrestlingMatch.WrestlerInBlue.ID;
        }

        public bool CanRejectResult { get; private set; }

        public override bool IsBackButtonAvailable => true;

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

            IsFormEnabled = true;
        }

        #region Binding Properties

        private bool IsMatchStarted => WrestlingMatch.LastSecondInMatch > 0;
        private bool IsMatchCompletedInTime => WrestlingMatch.LastSecondInMatch == WrestlingMatch.MaxRoundSecond * 2;
        public bool IsFreeWinEnabled => WrestlingMatch.WinType == MatchWinTypeEnum.FreeWin;
        public bool IsPointsWinEnabled => !IsMatchStarted || IsMatchCompletedInTime;
        public bool IsActionWinEnabled => IsMatchCompletedInTime && WrestlingMatch.PointsBlue == WrestlingMatch.PointsRed;
        public bool IsDominationWinEnabled => WrestlingMatch.PointsBlue - WrestlingMatch.PointsRed >= 10 || WrestlingMatch.PointsRed - WrestlingMatch.PointsBlue >= 10;
        public bool IsTusheWinEnabled => !IsMatchStarted && !IsFreeWinEnabled;
        public bool IsTechWinEnabled => !IsMatchStarted && !IsFreeWinEnabled;
        public bool IsWinnerRed => Winner.HasValue && WrestlingMatch.WrestlerInRed != null && Winner.Value == WrestlingMatch.WrestlerInRed.ID;
        public bool IsWinnerBlue => Winner.HasValue && WrestlingMatch.WrestlerInBlue != null && Winner.Value == WrestlingMatch.WrestlerInBlue.ID;
        public bool IsSetWinnerRedVisible => WrestlingMatch.Status == MatchStatusEnum.Pending && (!Winner.HasValue || WrestlingMatch.WrestlerInBlue != null && WrestlingMatch.WrestlerInBlue.ID == Winner.Value);
        public bool IsSetWinnerBlueVisible => WrestlingMatch.Status == MatchStatusEnum.Pending && (!Winner.HasValue || WrestlingMatch.WrestlerInRed != null && WrestlingMatch.WrestlerInRed.ID == Winner.Value);

        public Guid? Winner
        {
            get { return _winner; }
            set
            {
                _winner = value;

                OnPropertyChanged("Winner");
                OnPropertyChanged("IsSetWinnerRedVisible");
                OnPropertyChanged("IsSetWinnerBlueVisible");
                OnPropertyChanged("IsWinnerRed");
                OnPropertyChanged("IsWinnerBlue");
            }
        }

        public MatchWinTypeEnum? WinType
        {
            get { return _winType; }
            set
            {
                _winType = value;

                OnPropertyChanged("WinType");
            }
        }

        public bool IsFormEnabled
        {
            get { return _isFormEnabled; }
            set
            {
                _isFormEnabled = value;

                OnPropertyChanged("IsFormEnabled");
            }
        }

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
                WrestlingMatch.StartDateTime = Tournament.StartDate ?? DateTime.Now;
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
            
            if (_settings.IsVideoRecordingEnabled)
            {
                _recorder.RecordingCompleted += OnRecordingCompleted;

                IsFormEnabled = false;

                CompleteMatch();

                ShowSnackMessage("Подождите, идет запись видео-файла...");

                _recorder.StopRecording();
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

            _recorder.RecordingCompleted -= OnRecordingCompleted;

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
            var group = DataContext.Tournament.Groups.FirstOrDefault(p => p.ID == WrestlingMatch.GroupID);
            if (group == null) throw new ApplicationException("Can't find group!");
            
            if (!WrestlingMatch.IsRedWon.HasValue || !WrestlingMatch.WinType.HasValue) throw new ApplicationException("Completed match does not have result provided!");

            _processor.CompleteMatch(WrestlingMatch, WrestlingMatch.IsRedWon.Value, WrestlingMatch.WinType.Value);

            _scoreScreenVm.ShowWinner(WrestlingMatch);
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
                            Winner = WrestlingMatch.BestActionRed > WrestlingMatch.BestActionBlue ? WrestlingMatch.WrestlerInRed.ID : WrestlingMatch.WrestlerInBlue.ID;
                            Note = "Победа присуждена по качеству результативного действия.";
                        }
                        else
                        {
                            Winner = WrestlingMatch.IsLastActionRed ? WrestlingMatch.WrestlerInRed.ID : WrestlingMatch.WrestlerInBlue.ID;
                            Note = "При равном счете и равном качестве результативных действий победа присуждена по последнему действию.";
                        }

                        WinType = MatchWinTypeEnum.ActionWin;
                    }
                }
                else
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
    } 
}