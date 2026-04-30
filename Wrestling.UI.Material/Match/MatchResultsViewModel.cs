using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly List<IGroupBracketProcessor> _drawTypes;
        private IGroupBracketProcessor _processor;
        private ICommand _completeMatch;

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

                    if (WrestlingMatch != null && WrestlingMatch.Status == MatchStatusEnum.Completed && CanRejectResult)
                    {
                        _quickButtons.Add(new CommandButtonItem("Анулировать", PackIconKind.BlockHelper,
                            new AsyncRelayCommand(async param => await RejectAsync(),
                                param => WrestlingMatch != null && WrestlingMatch.Status == MatchStatusEnum.Completed &&
                                         CanRejectResult)));
                    }
                }

                return _quickButtons;
            }
        }
        
        public ICommand CompleteMatchCommand
        {
            get
            {
                if (_completeMatch == null)
                {
                    _completeMatch = new AsyncRelayCommand(
                        execute: async _ => await ApproveAsync(),
                        canExecute: _ => WrestlingMatch != null && WrestlingMatch.Status == MatchStatusEnum.Pending && Winner.HasValue && WinType.HasValue
                    );
                }
                return _completeMatch;
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
                _processor = GetProcessorForGroup(DataContext.Group.Bracket.BracketTypeCode);
                if (_processor == null) throw new ApplicationException("Can't find processor!");

                _processor.Load(DataContext.Tournament, DataContext.Group);
                CanRejectResult = _processor.CanMatchBeReverted(WrestlingMatch);
            }

            if (DataContext.WrestlingMatch.WrestlerInRed?.TeamID != null && DataContext.Tournament != null)
            {
                Wrestler1TeamEmblem = GetTeamEmblem(DataContext.WrestlingMatch.WrestlerInRed.TeamID.Value, DataContext.Tournament.TeamApplications);
            }

            if (DataContext.WrestlingMatch.WrestlerInBlue?.TeamID != null && DataContext.Tournament != null)
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
        private bool IsMatchCompletedInTime => IsMatchStarted && WrestlingMatch.LastSecondInMatch == WrestlingMatch.MaxRoundSecond * 2;
        public bool IsFreeWinEnabled => WrestlingMatch.WinType == MatchWinTypeEnum.FreeWin;
        public bool IsPointsWinEnabled => IsMatchCompletedInTime;
        public bool IsNoShowWinEnabled => !IsMatchStarted && !IsFreeWinEnabled;

        public bool IsWarningsLimitWinEnabled => WrestlingMatch.WarningsNumberRed == 3 || WrestlingMatch.WarningsNumberBlue == 3;
        public bool IsActionWinEnabled => IsMatchCompletedInTime && WrestlingMatch.PointsBlue == WrestlingMatch.PointsRed;
        public bool IsDominationWinEnabled => WrestlingMatch.PointsBlue - WrestlingMatch.PointsRed >= 10 || WrestlingMatch.PointsRed - WrestlingMatch.PointsBlue >= 10;
        public bool IsTusheWinEnabled => !IsFreeWinEnabled;
        public bool IsDisqualifyWinEnabled => !IsFreeWinEnabled;
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

        private bool _isNoteExpanded;
        public bool IsNoteExpanded
        {
            get { return _isNoteExpanded; }
            set
            {
                _isNoteExpanded = value;

                OnPropertyChanged("IsNoteExpanded");
            }
        }

        private bool _isActionsExpanded;
        public bool IsActionsExpanded
        {
            get { return _isActionsExpanded; }
            set
            {
                _isActionsExpanded = value;

                OnPropertyChanged("IsActionsExpanded");
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

        private async void SetWinType()
        {
            var availableWinTypes = new List<MatchWinTypeEnum>();
            
            if (IsTusheWinEnabled) availableWinTypes.Add(MatchWinTypeEnum.Tushe);
            
            availableWinTypes.Add(MatchWinTypeEnum.Injury);
            
            if (IsWarningsLimitWinEnabled) availableWinTypes.Add(MatchWinTypeEnum.WarningsLimit);
            
            if (IsNoShowWinEnabled) availableWinTypes.Add(MatchWinTypeEnum.NoShow);
            if (IsDisqualifyWinEnabled) availableWinTypes.Add(MatchWinTypeEnum.DisqualifyWin);

            if (IsDominationWinEnabled)
            {
                if (WrestlingMatch.PointsBlue > 0 && WrestlingMatch.PointsRed > 0)
                {
                    availableWinTypes.Add(MatchWinTypeEnum.DominationWinWithPoints);   
                }
                else
                {
                    availableWinTypes.Add(MatchWinTypeEnum.DominationWin);
                }
            }

            if (IsPointsWinEnabled)
            {
                if (WrestlingMatch.PointsBlue > 0 && WrestlingMatch.PointsRed > 0)
                {
                    availableWinTypes.Add(MatchWinTypeEnum.PointsWinWithPoints);   
                }
                else
                {
                    availableWinTypes.Add(MatchWinTypeEnum.PointsWin);
                }
            }
            
            if (IsActionWinEnabled) availableWinTypes.Add(MatchWinTypeEnum.ActionWin);

            var vm = new SetWinTypeViewModel(DiContainer, WinType, availableWinTypes);
            vm.InitData();

            var view = new SetWinTypeDialog
            {
                DataContext = vm
            };

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && Convert.ToBoolean(result))
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

        private async Task RejectAsync()
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
            // Bump version — Completed→Pending transition. Same rationale as in
            // ApproveAsync: peers with the prior Completed copy will see strictly
            // higher remote.Version and adopt the revert.
            WrestlingMatch.Version++;

            WrestlingMatch = null;
            WinType = null;
            Note = string.Empty;

            // Autosave after revert so peers importing over HTTP/UNC see the
            // Pending state. Without this, a stale .wrt on disk still reports
            // the cancelled match as Completed, and the importer's
            // "local Pending + remote Completed → apply" guard (TournamentImporter.cs:185)
            // re-applies the old result, masking a newer completion from
            // another peer.
            if (DataContext.Tournament != null)
            {
                await SaveIfAutosaveEnabledAsync();
            }

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

        private async Task ApproveAsync()
        {
            if (WrestlingMatch == null || WrestlingMatch.Status != MatchStatusEnum.Pending || !Winner.HasValue || !WinType.HasValue)
            {
                Dialog.ShowMessageBox(this,
                        "Ошибка завершения мачта! Возможно матч уже завершен.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (WrestlingMatch.StartDateTime == null)
            {
                WrestlingMatch.StartDateTime = Tournament?.StartDate ?? DateTime.Now;
            }

            WrestlingMatch.IsRedWon = Winner == WrestlingMatch.WrestlerInRed.ID;
            WrestlingMatch.WinType = WinType;
            WrestlingMatch.Note = Note;
            // Bump version — this is the only state-change point that turns a
            // Pending match into Completed, so it's the canonical place to mark
            // "newer than what any peer might still be holding". Importer treats
            // strict "remote.Version > local.Version" as the trigger to adopt
            // remote state.
            WrestlingMatch.Version++;
            WrestlingMatch.MatchActions.Add(new MatchAction
            {
                DateTime = DateTime.Now,
                RoundNumber = WrestlingMatch.LastSecondInMatch > WrestlingMatch.MaxRoundSecond ? 2 : 1,
                SecondInRound = WrestlingMatch.LastSecondInMatch > WrestlingMatch.MaxRoundSecond ? WrestlingMatch.LastSecondInMatch - WrestlingMatch.MaxRoundSecond : WrestlingMatch.LastSecondInMatch,
                Text = "Матч завершен"
            });

            CompleteMatch();

            if (DataContext.Tournament != null && WrestlingMatch.WinType.HasValue)
            {
                await SaveIfAutosaveEnabledAsync();

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
            WrestlingMatch.Version++;
            WrestlingMatch.Status = MatchStatusEnum.Completed;
            WrestlingMatch = null;

            NavigateToView<HomeViewModel>();
        }

        private IGroupBracketProcessor GetProcessorForGroup(string processorType)
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
            
            // Match already completed we just need to init binding properties
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
                
                return;
            }
            
            // If match not started select Tushe by default
            if (!IsMatchStarted)
            {
                WinType = MatchWinTypeEnum.Tushe;
                return;
            }
            
            // Check if it is 3 warnings win
            if (WrestlingMatch.WarningsNumberRed == 3)
            {
                Winner = WrestlingMatch.WrestlerInBlue.ID;
                WinType = MatchWinTypeEnum.WarningsLimit;
                return;
            }
            
            // Check if it is 3 warnings win
            if (WrestlingMatch.WarningsNumberBlue == 3)
            {
                Winner = WrestlingMatch.WrestlerInRed.ID;
                WinType = MatchWinTypeEnum.WarningsLimit;
                return;
            }

            // Match time completed so we should be able to determine win type
            if (IsMatchCompletedInTime)
            {
                if (WrestlingMatch.PointsRed > WrestlingMatch.PointsBlue)
                {
                    Winner = WrestlingMatch.WrestlerInRed.ID;

                    if (WrestlingMatch.PointsRed > 0 && WrestlingMatch.PointsBlue > 0)
                    {
                        WinType = MatchWinTypeEnum.PointsWinWithPoints;   
                    }
                    else
                    {
                        WinType = MatchWinTypeEnum.PointsWin;
                    }
                }
                else if (WrestlingMatch.PointsBlue > WrestlingMatch.PointsRed)
                {
                    Winner = WrestlingMatch.WrestlerInBlue.ID;
                    if (WrestlingMatch.PointsRed > 0 && WrestlingMatch.PointsBlue > 0)
                    {
                        WinType = MatchWinTypeEnum.PointsWinWithPoints;   
                    }
                    else
                    {
                        WinType = MatchWinTypeEnum.PointsWin;
                    }
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
                
                return;
            }
            
            // Check if it is a domination win
            if (WrestlingMatch.PointsRed - WrestlingMatch.PointsBlue >= 10)
            {
                Winner = WrestlingMatch.WrestlerInRed.ID;
                    
                if (WrestlingMatch.PointsRed > 0 && WrestlingMatch.PointsBlue > 0)
                {
                    WinType = MatchWinTypeEnum.DominationWinWithPoints;   
                }
                else
                {
                    WinType = MatchWinTypeEnum.DominationWin;
                }
                return;
            }
            
            // Check if it is a domination win
            if (WrestlingMatch.PointsBlue - WrestlingMatch.PointsRed >= 10)
            {
                Winner = WrestlingMatch.WrestlerInBlue.ID;
                if (WrestlingMatch.PointsRed > 0 && WrestlingMatch.PointsBlue > 0)
                {
                    WinType = MatchWinTypeEnum.DominationWinWithPoints;   
                }
                else
                {
                    WinType = MatchWinTypeEnum.DominationWin;
                }
                return;
            }

            // Use Tushe in other cases
            WinType = MatchWinTypeEnum.Tushe;
        }

        #endregion
    } 
}