using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

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
        private IKeyHandler _keyHandler;
        private ILocalizationService _localization;

        #endregion

        public MatchResultsViewModel(IDiContainer container) : base(container)
        {
            _drawTypes = Resolve<List<IGroupBracketProcessor>>();
        }

        // T inherited from TournamentViewModelBase.
        public override string PageTitle => T("MatchResults_PageTitle", "Результаты поединка");

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                if (_quickButtons == null)
                {
                    _quickButtons = new List<CommandButtonItem>();

                    if (WrestlingMatch != null && WrestlingMatch.Status == MatchStatusEnum.Completed && CanRejectResult)
                    {
                        _quickButtons.Add(new CommandButtonItem(T("MatchResults_Cancel", "Анулировать"), PackIconKind.BlockHelper,
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
                        // Mutual DSQ has no winner — allow complete when only WinType is set.
                        canExecute: _ => WrestlingMatch != null && WrestlingMatch.Status == MatchStatusEnum.Pending && WinType.HasValue
                            && (Winner.HasValue || WinType == MatchWinTypeEnum.MutualDisqualify)
                    );
                }
                return _completeMatch;
            }
        }

        public override void InitData()
        {
            base.InitData();

            _scoreScreenVm = Resolve<ScoreScreenViewModel>();
            if (_localization == null) _localization = Resolve<ILocalizationService>();

            if (WrestlingMatch == null) throw new InvalidOperationException("Match is not set on the data context — navigation reached MatchResultsView without a current match.");

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
                if (_processor == null) throw new InvalidOperationException($"No processor registered for bracket type '{DataContext.Group.Bracket.BracketTypeCode}'. Check DI registration in App.xaml.cs.");

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

            // Arrow-key shortcuts: Up/Down cycle WinType, Left/Right pick
            // winner side. Singleton VM, so guard with -=/+= on revisit.
            if (_keyHandler == null) _keyHandler = Resolve<IKeyHandler>();
            if (_keyHandler != null)
            {
                _keyHandler.KeyPressed -= KeyHandler_KeyPressed;
                _keyHandler.KeyPressed += KeyHandler_KeyPressed;
            }

            OnPropertyChanged("IsWinnerNotSelected");
        }

        protected override void OnNavigatingOut()
        {
            base.OnNavigatingOut();
            if (_keyHandler != null)
            {
                _keyHandler.KeyPressed -= KeyHandler_KeyPressed;
            }
        }

        private void KeyHandler_KeyPressed(object sender, KeyEventArgs e)
        {
            // Don't steal arrow keys from the notes textbox.
            if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase) return;
            if (WrestlingMatch == null || WrestlingMatch.Status != MatchStatusEnum.Pending) return;

            switch (e.Key)
            {
                case Key.Up:
                    CycleWinType(-1);
                    e.Handled = true;
                    break;
                case Key.Down:
                    CycleWinType(+1);
                    e.Handled = true;
                    break;
                case Key.Left:
                    SelectWinnerSide(true);
                    e.Handled = true;
                    break;
                case Key.Right:
                    SelectWinnerSide(false);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    // Mirror the «ЗАВЕРШИТЬ» button: only fire when the
                    // command's CanExecute is true (WinType set + winner
                    // selected, or mutual-DSQ outcome).
                    var cmd = CompleteMatchCommand;
                    if (cmd != null && cmd.CanExecute(null))
                    {
                        cmd.Execute(null);
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void CycleWinType(int direction)
        {
            var available = BuildAvailableWinTypes();
            if (available.Count == 0) return;
            int current = WinType.HasValue ? available.IndexOf(WinType.Value) : -1;
            int next;
            if (current < 0)
            {
                next = direction > 0 ? 0 : available.Count - 1;
            }
            else
            {
                next = (current + direction + available.Count) % available.Count;
            }
            WinType = available[next];
        }

        private void SelectWinnerSide(bool isRed)
        {
            if (WrestlingMatch == null || WrestlingMatch.IsMatchCompleted) return;
            // Mutual outcomes don't take a winner. Match the SetWinner guard.
            if (WinType == MatchWinTypeEnum.MutualDisqualify
                || WinType == MatchWinTypeEnum.MutualNoShow
                || WinType == MatchWinTypeEnum.MutualInjury) return;

            if (isRed && WrestlingMatch.WrestlerInRed != null)
            {
                Winner = WrestlingMatch.WrestlerInRed.ID;
            }
            else if (!isRed && WrestlingMatch.WrestlerInBlue != null)
            {
                Winner = WrestlingMatch.WrestlerInBlue.ID;
            }
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

                NavigateToReturnTarget();
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
                    _setWinTypeCommand = new AsyncRelayCommand(async param => await SetWinTypeAsync(), param => true);
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
                OnPropertyChanged("IsWinnerNotSelected");
            }
        }

        // Validation flag for the «Не выбран победитель» message under the
        // match panel. Mutual outcomes don't need a winner, so they suppress
        // the warning. Completed matches don't need it either — by then a
        // winner is locked in.
        public bool IsWinnerNotSelected =>
            WrestlingMatch != null
            && WrestlingMatch.Status == MatchStatusEnum.Pending
            && !Winner.HasValue
            && WinType != MatchWinTypeEnum.MutualDisqualify
            && WinType != MatchWinTypeEnum.MutualNoShow
            && WinType != MatchWinTypeEnum.MutualInjury;

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

                // Mutual outcomes have no winner — drop any previously-picked
                // side so the yellow «ПОБЕДИТЕЛЬ» badge disappears and
                // ApproveAsync's (!Winner.HasValue && !isMutual) gate sees a
                // clean state.
                if ((value == MatchWinTypeEnum.MutualDisqualify
                     || value == MatchWinTypeEnum.MutualNoShow
                     || value == MatchWinTypeEnum.MutualInjury) && Winner != null)
                {
                    Winner = null;
                }

                OnPropertyChanged("WinType");
                OnPropertyChanged("IsWinTypeSet");
                OnPropertyChanged("IsWinnerNotSelected");
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

        // Shared between the picker dialog and the Up/Down keyboard cycle.
        // WarningsLimit (3 предупреждения) is NOT in the list — it's applied
        // automatically when a wrestler accumulates 3 warnings.
        private List<MatchWinTypeEnum> BuildAvailableWinTypes()
        {
            var result = new List<MatchWinTypeEnum>();

            if (IsTusheWinEnabled) result.Add(MatchWinTypeEnum.Tushe);

            result.Add(MatchWinTypeEnum.Injury);
            result.Add(MatchWinTypeEnum.MutualInjury);

            if (IsNoShowWinEnabled) result.Add(MatchWinTypeEnum.NoShow);
            if (IsNoShowWinEnabled) result.Add(MatchWinTypeEnum.MutualNoShow);
            if (IsDisqualifyWinEnabled) result.Add(MatchWinTypeEnum.DisqualifyWin);
            if (IsDisqualifyWinEnabled) result.Add(MatchWinTypeEnum.MutualDisqualify);

            if (IsDominationWinEnabled)
            {
                if (WrestlingMatch.PointsBlue > 0 && WrestlingMatch.PointsRed > 0)
                {
                    result.Add(MatchWinTypeEnum.DominationWinWithPoints);
                }
                else
                {
                    result.Add(MatchWinTypeEnum.DominationWin);
                }
            }

            if (IsPointsWinEnabled)
            {
                if (WrestlingMatch.PointsBlue > 0 && WrestlingMatch.PointsRed > 0)
                {
                    result.Add(MatchWinTypeEnum.PointsWinWithPoints);
                }
                else
                {
                    result.Add(MatchWinTypeEnum.PointsWin);
                }
            }

            if (IsActionWinEnabled) result.Add(MatchWinTypeEnum.ActionWin);

            return result;
        }

        private async Task SetWinTypeAsync()
        {
            var availableWinTypes = BuildAvailableWinTypes();
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
            // Mutual outcomes: neither side can be marked the winner —
            // finalize via ApproveAsync only.
            if (WinType == MatchWinTypeEnum.MutualDisqualify
                || WinType == MatchWinTypeEnum.MutualNoShow
                || WinType == MatchWinTypeEnum.MutualInjury) return;

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
                    T("MatchResults_RevertConfirm_Body", "Результат матча будет анулирован и сетка перестроена! Вы уверены?"),
                    T("MatchResults_ConfirmTitle", "Требуется подтверждение"), MessageBoxButton.OKCancel, MessageBoxImage.None) != MessageBoxResult.OK) return;

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
                ResultsService.Recalculate(DataContext.Tournament);

                await SaveIfAutosaveEnabledAsync();
            }

            NavigateToReturnTarget();
        }

        private async Task ApproveAsync()
        {
            // No-winner outcomes — finalize without picking a wrestler.
            var isMutual = WinType == MatchWinTypeEnum.MutualDisqualify
                           || WinType == MatchWinTypeEnum.MutualNoShow
                           || WinType == MatchWinTypeEnum.MutualInjury;
            if (WrestlingMatch == null || WrestlingMatch.Status != MatchStatusEnum.Pending || !WinType.HasValue
                || (!Winner.HasValue && !isMutual))
            {
                Dialog.ShowMessageBox(this,
                        T("MatchResults_CompleteError_Body", "Ошибка завершения мачта! Возможно матч уже завершен."),
                        T("MatchResults_ErrorTitle", "Ошибка"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (WrestlingMatch.StartDateTime == null)
            {
                // Fallback for matches completed without ever starting the
                // timer (NoShow, manual completion after revert, auto-VCA
                // before Start was clicked). Use DateTime.Now so the match
                // appears at the top of «Последние результаты» — falling back
                // to Tournament.StartDate would stamp it at the morning of
                // the competition day and push it past the top-10 cutoff.
                WrestlingMatch.StartDateTime = DateTime.Now;
            }

            WrestlingMatch.IsRedWon = isMutual ? (bool?)null : Winner == WrestlingMatch.WrestlerInRed.ID;
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
                Type = MatchActionType.MatchCompleted,
                DateTime = DateTime.Now,
                RoundNumber = WrestlingMatch.LastSecondInMatch > WrestlingMatch.MaxRoundSecond ? 2 : 1,
                SecondInRound = WrestlingMatch.LastSecondInMatch > WrestlingMatch.MaxRoundSecond ? WrestlingMatch.LastSecondInMatch - WrestlingMatch.MaxRoundSecond : WrestlingMatch.LastSecondInMatch,
            });

            CompleteMatch();

            // ResultsService and autosave both need a Tournament — gate them
            // on that. Always return the operator to their previous screen
            // (carpet/schedule/etc) via NavigateToMatches; there is no
            // legitimate path where completing a match should drop the user
            // back to the «open tournament» chooser.
            if (DataContext.Tournament != null)
            {
                ResultsService.Recalculate(DataContext.Tournament);
                await SaveIfAutosaveEnabledAsync();
            }

            NavigateToMatches();
        }

        private void CompleteMatch()
        {
            // Mutual DSQ / NoShow / Injury: WinType is set, IsRedWon is null —
            // that's the canonical «no winner» encoding (see GroupBracketProcessorBase).
            if (!WrestlingMatch.WinType.HasValue) throw new InvalidOperationException("Completed match must have WinType set.");
            var isMutual = WrestlingMatch.WinType == MatchWinTypeEnum.MutualDisqualify
                           || WrestlingMatch.WinType == MatchWinTypeEnum.MutualNoShow
                           || WrestlingMatch.WinType == MatchWinTypeEnum.MutualInjury;
            if (!WrestlingMatch.IsRedWon.HasValue && !isMutual)
                throw new InvalidOperationException("Completed match must have IsRedWon set (or be a Mutual* outcome).");

            if (DataContext.Tournament != null)
            {
                _processor.CompleteMatch(WrestlingMatch, WrestlingMatch.IsRedWon, WrestlingMatch.WinType.Value);

                if (WrestlingMatch.WinType == MatchWinTypeEnum.MutualDisqualify)
                {
                    var msg = GetMutualDsqAdvisoryMessage(WrestlingMatch);
                    if (!string.IsNullOrEmpty(msg)) ShowSnackMessage(msg);
                }
                else
                {
                    _scoreScreenVm.ShowWinner(WrestlingMatch);
                }
            }
        }

        // Returns a Russian advisory message for a mutual-DSQ outcome, tailored
        // to the round and bracket type. Returns null/empty when no message is
        // needed (e.g. mutual DSQ in an early round of an Olympic bracket — the
        // cascade in GroupBracketProcessorBase handles it without operator
        // intervention).
        private string GetMutualDsqAdvisoryMessage(WrestlingMatch match)
        {
            if (DataContext.Group?.Bracket == null || _processor == null) return null;
            var sf = _processor.GetSemiFinalRound(DataContext.Group);
            var f = _processor.GetFinalRound(DataContext.Group);
            var code = DataContext.Group.Bracket.BracketTypeCode;
            var isElim = code != BracketTypeEnum.RoundRobin.ToString();
            if (!isElim) return null;

            var isFinal = f != null && f.RoundMatches.Contains(match);
            var isSemiFinal = sf != null && sf.RoundMatches.Contains(match);
            if (!isFinal && !isSemiFinal) return null;

            // Final-mutual-DSQ in the consolation bracket type is auto-handled:
            // bronze winners are promoted into the final once both bronzes have
            // completed (any order). The alert tells the operator what to expect.
            if (isFinal && code == BracketTypeEnum.OlympicConsilationFinalists.ToString())
            {
                // After CompleteMatch returns, the rebuild has either already
                // fired (bronzes were done first) or it'll fire when bronzes
                // complete. Detect by checking whether the final is now Pending —
                // the rebuild resets it.
                if (match.Status == MatchStatusEnum.Pending)
                {
                    return T("MatchResults_MutualDsq_FinalRebuilt",
                        "Обоюдная DSQ в финале: в финал переведены победители схваток за 3-е место. Финал сыгран заново.");
                }
                return T("MatchResults_MutualDsq_FinalPending",
                    "Обоюдная DSQ в финале: после завершения схваток за 3-е место их победители будут переведены в финал.");
            }

            return T("MatchResults_MutualDsq_ManualRebuild",
                "Обоюдная DSQ в полуфинале/финале — требуется ручная перестройка сетки (правила УВВ).");
        }

        private void NavigateToMatches()
        {
            NavigateToReturnTarget();
        }

        // Restores the screen the operator was on before MatchControl took
        // over (Phase 5 wrapper for the carpet/schedule path, Phase 6 for the
        // CompletedMatches path, etc.). Captured by MainWindowViewModel on
        // the transition into the overlay.
        private void NavigateToReturnTarget()
        {
            var target = Navigation.ShellVm?.GetReturnVmType();
            if (target != null) Navigation.NavigateToView(target);
            else Navigation.NavigateToView<Tournament.Conducting.ConductingViewModel>();
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
                if (!WrestlingMatch.WinType.HasValue) throw new InvalidOperationException("Completed match must have WinType set.");
                // Mutual DSQ / NoShow / Injury: completed without winner. Other completion types must have IsRedWon.
                var initIsMutual = WrestlingMatch.WinType == MatchWinTypeEnum.MutualDisqualify
                                   || WrestlingMatch.WinType == MatchWinTypeEnum.MutualNoShow
                                   || WrestlingMatch.WinType == MatchWinTypeEnum.MutualInjury;
                if (!WrestlingMatch.IsRedWon.HasValue && !initIsMutual)
                    throw new InvalidOperationException("Completed match must have IsRedWon set (or be a Mutual* outcome).");

                WinType = WrestlingMatch.WinType.Value;

                if (WrestlingMatch.IsRedWon.HasValue)
                {
                    if (WrestlingMatch.IsRedWon.Value && WrestlingMatch.WrestlerInRed != null)
                    {
                        Winner = WrestlingMatch.WrestlerInRed.ID;
                    }
                    else if (!WrestlingMatch.IsRedWon.Value && WrestlingMatch.WrestlerInBlue != null)
                    {
                        Winner = WrestlingMatch.WrestlerInBlue.ID;
                    }
                }

                Note = WrestlingMatch.Note;

                // Completed-view advantage indicator: when scores are tied
                // (action-win), light the yellow underline on whichever side
                // won the tiebreaker (better-quality action, or last action).
                // Without this the operator sees a 5:5 score on a finished
                // action-win match with no visual cue why one side won.
                if (WrestlingMatch.PointsRed == WrestlingMatch.PointsBlue && WrestlingMatch.PointsRed > 0)
                {
                    if (WrestlingMatch.BestActionRed != WrestlingMatch.BestActionBlue)
                    {
                        IsPlayer1WithAdvantage = WrestlingMatch.BestActionRed > WrestlingMatch.BestActionBlue;
                        IsPlayer2WithAdvantage = !IsPlayer1WithAdvantage;
                    }
                    else
                    {
                        IsPlayer1WithAdvantage = WrestlingMatch.IsLastActionRed;
                        IsPlayer2WithAdvantage = !WrestlingMatch.IsLastActionRed;
                    }
                }

                return;
            }

            // 3 warnings (VCA 5:0) takes precedence over the «match not
            // started → Tushe» default — the auto-trigger from MatchControl
            // can fire before the timer was ever started, so checking
            // IsMatchStarted first would mask it. Use >= so an over-count
            // (e.g. 4) still resolves correctly.
            if (WrestlingMatch.WarningsNumberRed >= 3 && WrestlingMatch.WrestlerInBlue != null)
            {
                Winner = WrestlingMatch.WrestlerInBlue.ID;
                WinType = MatchWinTypeEnum.WarningsLimit;
                return;
            }

            if (WrestlingMatch.WarningsNumberBlue >= 3 && WrestlingMatch.WrestlerInRed != null)
            {
                Winner = WrestlingMatch.WrestlerInRed.ID;
                WinType = MatchWinTypeEnum.WarningsLimit;
                return;
            }

            // If match not started select Tushe by default
            if (!IsMatchStarted)
            {
                WinType = MatchWinTypeEnum.Tushe;
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
                        Note = T("MatchResults_AutoNote_ActionQuality", "Победа присуждена по качеству результативного действия.");
                    }
                    else
                    {
                        IsPlayer1WithAdvantage = WrestlingMatch.IsLastActionRed;
                        IsPlayer2WithAdvantage = !WrestlingMatch.IsLastActionRed;
                        Winner = WrestlingMatch.IsLastActionRed ? WrestlingMatch.WrestlerInRed.ID : WrestlingMatch.WrestlerInBlue.ID;
                        Note = T("MatchResults_AutoNote_LastAction", "При равном счете и равном качестве результативных действий победа присуждена по последнему действию.");
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