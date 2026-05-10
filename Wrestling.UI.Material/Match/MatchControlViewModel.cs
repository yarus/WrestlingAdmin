using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Tournament.Progress.Brackets;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Match
{
    public class MatchControlViewModel : ViewModelBase
    {
        #region Fields
        
        private DateTime? _startDateTime;
        private List<MatchAction> _matchActions;

        private DispatcherTimer _timer;

        private bool _isRunning;

        private ScoreScreenViewModel _scoreScreen;
        private IKeyHandler _keyHandler;

        private BitmapImage _action1Image;
        private BitmapImage _action2Image;
        private Visibility _action1Visibility;
        private Visibility _action2Visibility;
        
        private ICommand _startCommand;
        private ICommand _stopCommand;
        private ICommand _adjustPointsCommand;
        private ICommand _adjustTimerCommand;
        private ICommand _actionStartStopCommand;
        private ICommand _changeWarningsCommand;
        private ICommand _completeMatch;

        private IList<CommandButtonItem> _quickButtons;

        #endregion

        public MatchControlViewModel(IDiContainer container) : base(container)
        {
            _matchActions = new List<MatchAction>();
            SetupTimer();
        }

        public bool IsRunning
        {
            get { return _isRunning; }
            set
            {
                _isRunning = value;
                OnPropertyChanged("IsStartButtonVisible");
                OnPropertyChanged("IsStopButtonVisible");
            }
        }

        public override bool IsBackButtonAvailable => true;
        public bool IsStartButtonVisible => IsMatchNotCompleted && !IsRunning;
        public bool IsStopButtonVisible => IsMatchNotCompleted && IsRunning;

        public override string PageTitle => "Управление Электронным Табло";

        // No QuickButtons on this screen — the «Сбросить поединок» action is
        // implicit: leaving an unfinished match via the back button resets
        // it (with a confirmation prompt, see OnBackCommand). The «Открыть
        // электронное табло» button moved to ScheduleViewModel.
        public override IList<CommandButtonItem> QuickButtons
        {
            get { return _quickButtons ?? (_quickButtons = new List<CommandButtonItem>()); }
        }

        public override void InitData()
        {
            base.InitData();

            _scoreScreen = Resolve<ScoreScreenViewModel>();
            _keyHandler = Resolve<IKeyHandler>();
           
            _quickButtons = null;

            if (DataContext.WrestlingMatch == null)
            {
                ShowSnackMessage("Матч не инициализирован!");
                OnBackCommand();
                return;
            }

            SetupTimer();

            // Sync local _startDateTime with the match's persisted value.
            // The VM is a singleton, so without this the previous match's
            // start time leaks into a freshly-opened (or reverted) match —
            // Start() guards on `!_startDateTime.HasValue` and would skip
            // updating, then CopyDataFromViewToMatch writes the stale value.
            _startDateTime = DataContext.WrestlingMatch.StartDateTime;

            if (DataContext.WrestlingMatch.LastSecondInMatch == 0)
            {
                _matchActions = new List<MatchAction>();
            }
            else
            {
                _matchActions = DataContext.WrestlingMatch.MatchActions;
            }

            _scoreScreen.InitData();

            CalculateAdvantage();

            SetActionTimers();

            if (_keyHandler != null)
            {
                _keyHandler.KeyPressed -= KeyHandler_KeyPressed;
                _keyHandler.KeyPressed += KeyHandler_KeyPressed;
            }

            ScoreScreenVm.IsTimeout = false;
        }

        #region Commands

        public ICommand ChangeWarningsCommand
        {
            get
            {
                if (_changeWarningsCommand == null)
                {
                    _changeWarningsCommand = new RelayCommand(
                        param => AdjustWarnings(param.ToString()),
                        param => true
                    );
                }
                return _changeWarningsCommand;
            }
        }


        public ICommand CompleteMatchCommand
        {
            get
            {
                if (_completeMatch == null)
                {
                    _completeMatch = new RelayCommand(
                        param => CompleteMatch(),
                        param => true);
                }
                return _completeMatch;
            }
        }

        public ICommand StartCommand
        {
            get
            {
                if (_startCommand == null)
                {
                    _startCommand = new RelayCommand(
                        param => Start(),
                        param => true
                    );
                }
                return _startCommand;
            }
        }

        public ICommand StopCommand
        {
            get
            {
                if (_stopCommand == null)
                {
                    _stopCommand = new RelayCommand(
                        param => Stop(),
                        param => true
                    );
                }
                return _stopCommand;
            }
        }

        public ICommand AdjustPointsCommand
        {
            get
            {
                if (_adjustPointsCommand == null)
                {
                    _adjustPointsCommand = new RelayCommand(
                        param => AdjustPoints(param.ToString()),
                        param => true
                    );
                }
                return _adjustPointsCommand;
            }
        }

        public ICommand AdjustTimerCommand
        {
            get
            {
                if (_adjustTimerCommand == null)
                {
                    _adjustTimerCommand = new RelayCommand(
                        param => AdjustTimer(Convert.ToInt32(param)),
                        param => DataContext.WrestlingMatch != null
                                 && DataContext.WrestlingMatch.Status != MatchStatusEnum.Completed
                    );
                }
                return _adjustTimerCommand;
            }
        }

        public ICommand ActionStartStopCommand
        {
            get
            {
                if (_actionStartStopCommand == null)
                {
                    _actionStartStopCommand = new RelayCommand(
                        param => ActionStartStop(param.ToString()),
                        param => true
                    );
                }
                return _actionStartStopCommand;
            }
        }

        #endregion

        #region Properties

        public bool IsMatchNotCompleted => DataContext.WrestlingMatch != null && DataContext.WrestlingMatch.LastSecondInMatch < DataContext.WrestlingMatch.MaxRoundSecond * 2 && !DataContext.WrestlingMatch.IsMatchCompleted;

        public bool IsTournamentEmpty => DataContext.Tournament == null;

        public ScoreScreenViewModel ScoreScreenVm
        {
            get { return _scoreScreen; }
            set
            {
                _scoreScreen = value;

                OnPropertyChanged("ScoreScreenVm");
            }
        }

        public BitmapImage Action1Image
        {
            get
            {
                return _action1Image;
            }
            set
            {
                _action1Image = value;

                OnPropertyChanged("Action1Image");
            }
        }

        public BitmapImage Action2Image
        {
            get
            {
                return _action2Image;
            }
            set
            {
                _action2Image = value;

                OnPropertyChanged("Action2Image");
            }
        }

        public Visibility Action1Visibility
        {
            get
            {
                return _action1Visibility;
            }
            set
            {
                _action1Visibility = value;

                OnPropertyChanged("Action1Visibility");
            }
        }

        public Visibility Action2Visibility
        {
            get
            {
                return _action2Visibility;
            }
            set
            {
                _action2Visibility = value;

                OnPropertyChanged("Action2Visibility");
            }
        }

        #endregion    

        #region Private Methods

        private void KeyHandler_KeyPressed(object sender, KeyEventArgs e)
        {
            if (DataContext.WrestlingMatch == null || DataContext.WrestlingMatch.Status == MatchStatusEnum.Completed)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Space)
            {
                if (IsRunning)
                {
                    Stop();
                }
                else
                {
                    Start();
                }
                
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                if (IsRunning)
                {
                    Stop();
                }

                CompleteMatch();

                e.Handled = true;
                return;
            }

            bool isShift = (e.KeyboardDevice.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            string color = isShift ? "Blue" : "Red";

            // Points: digit = red, Shift+digit = blue (top row + numpad).
            //   1→+1, 2→+2, 4→+4, 5→+5
            var points = MapKeyToPoints(e.Key);
            if (points.HasValue)
            {
                AdjustPoints($"+{points.Value},{color}");
                e.Handled = true;
                return;
            }

            // W = warning red, Shift+W = warning blue (W like "warning").
            if (e.Key == Key.W)
            {
                AdjustWarnings($"+1,{color}");
                e.Handled = true;
                return;
            }

            // Backspace = undo the most recent undoable action across both
            // wrestlers (SetPoints, SetWarning, ShowActionTimer). No Shift
            // variant — RevertLastAction walks the global history in order.
            if (e.Key == Key.Back)
            {
                RevertLastAction();
                e.Handled = true;
                return;
            }

            // A = toggle activity timer red, Shift+A = blue.
            if (e.Key == Key.A)
            {
                ActionStartStop(color);
                e.Handled = true;
                return;
            }
        }

        private static int? MapKeyToPoints(Key key)
        {
            switch (key)
            {
                case Key.D1:
                case Key.NumPad1:
                    return 1;
                case Key.D2:
                case Key.NumPad2:
                    return 2;
                case Key.D4:
                case Key.NumPad4:
                    return 4;
                case Key.D5:
                case Key.NumPad5:
                    return 5;
                default:
                    return null;
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

        // Single typed factory replacing the old text-based AddAction +
        // AddPointsAction + AddWarningAction trio. Display text is computed
        // by MatchActionDescriber (called inside the adapter / UI converter)
        // so callers only specify the discriminator + the relevant payload:
        //   SetPoints / RevertPoints  → isForRed + points
        //   SetWarning / RevertWarning / Show/Hide/Expired → isForRed
        //   RoundFinished / TimerAdjusted → points only
        //   timer-start/stop, timeout-start/stop, MatchCompleted → no payload
        private void AddAction(MatchActionType type, bool? isForRed = null, int points = 0)
        {
            _matchActions.Add(new MatchAction
            {
                Type = type,
                DateTime = DateTime.Now,
                RoundNumber = ScoreScreenVm.Round,
                SecondInRound = ScoreScreenVm.MainSeconds,
                IsForRed = isForRed,
                Points = points,
            });
        }

        private void Stop()
        {
            _timer?.Stop();
            
            AddAction(MatchActionType.StopMatchTimer);

            IsRunning = false;
        }

        private void Start()
        {
            if (!_startDateTime.HasValue)
            {
                _startDateTime = DateTime.Now;
            }

            // No need to continue if 2nd round finished
            if (ScoreScreenVm.MainSeconds == ScoreScreenVm.MaxRoundSecond && ScoreScreenVm.Round == 2) return;

            if (_timer == null)
            {
                SetupTimer();
            }

            _timer.Start();

            AddAction(MatchActionType.StartMatchTimer);

            if (ScoreScreenVm.MainSeconds == 0 && ScoreScreenVm.IsSoundEnabled)
            {
                PlaySingleGongSound();
            }

            IsRunning = true;
        }

        private void PlaySingleGongSound()
        {
            var singleGongPath = GlobalSettings.StartGongSoundPath;
            if (File.Exists(singleGongPath))
            {
                SoundPlayer sp = new SoundPlayer(singleGongPath);
                sp.Play();
            }
        }

        private void PlayTrippleGongSound()
        {
            var trippleGongPath = GlobalSettings.EndGongSoundPath;
            if (File.Exists(trippleGongPath))
            {
                SoundPlayer sp = new SoundPlayer(trippleGongPath);
                sp.Play();
            }
        }

        private void Reset()
        {
            Stop();

            _startDateTime = null;
            _matchActions = new List<MatchAction>();

            Action1Image = GetActionPathByEnabled(true);
            Action2Image = GetActionPathByEnabled(true);

            Action1Visibility = Visibility.Visible;
            Action2Visibility = Visibility.Visible;

            ScoreScreenVm.Reset();

            CopyDataFromViewToMatch();

            OnPropertyChanged("IsStartButtonVisible");
            OnPropertyChanged("IsStopButtonVisible");
        }

        private void AdjustWarnings(string param)
        {
            var items = param.Split(',');
            var value = Convert.ToInt32(items[0]);
            bool isRed = items[1] == "Red";

            if (isRed)
            {
                ScoreScreenVm.Wrestler1WarningsNumber += value;
            }
            else
            {
                ScoreScreenVm.Wrestler2WarningsNumber += value;
            }
            AddAction(MatchActionType.SetWarning, isForRed: isRed);

            // UWW: 3 warnings = automatic disqualification (VCA 5:0). Stop
            // the timer and jump to results immediately — operator cannot
            // change the win type, MatchResultsViewModel.SetWinnerAndWinType
            // forces WinType=WarningsLimit and assigns the opponent as winner.
            if (ScoreScreenVm.Wrestler1WarningsNumber >= 3 || ScoreScreenVm.Wrestler2WarningsNumber >= 3)
            {
                if (IsRunning) Stop();
                CompleteMatch();
            }
        }

        protected override void OnBackCommand()
        {
            if (Dialog.ShowMessageBox(this,
                    "Матч не звершен! Если вы вернетесь назад, то текущие результаты будут потеряны. Вы уверены, что хотите вернуться?",
                    "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.None) != MessageBoxResult.OK) return;

            if (!DataContext.WrestlingMatch.IsMatchCompleted)
            {
                Reset();

                DataContext.WrestlingMatch = null;
            }

            if (DataContext.Tournament != null)
            {
                NavigateToReturnTarget();
            }
            else
            {
                NavigateToView<HomeViewModel>();
            }
        }

        // Returns to whichever non-overlay screen the operator was on before
        // we took over full-screen for match control. Falls back to Phase 5
        // (Проведение) wrapper when no return target was captured (e.g. the
        // shell was reset between captures).
        private void NavigateToReturnTarget()
        {
            var target = Navigation.ShellVm?.GetReturnVmType();
            if (target != null) Navigation.NavigateToView(target);
            else Navigation.NavigateToView<Tournament.Conducting.ConductingViewModel>();
        }

        private void CompleteMatch()
        {
            _timer?.Stop();

            if (_isRunning)
            {
                IsRunning = false;
            }

            ScoreScreenVm.IsTimeout = false;

            CopyDataFromViewToMatch();

            NavigateToView<MatchResultsViewModel>();
        }

        private void CopyDataFromViewToMatch()
        {
            DataContext.WrestlingMatch.LastSecondInMatch = ScoreScreenVm.Round == 1
                ? ScoreScreenVm.MainSeconds
                : ScoreScreenVm.MaxRoundSecond + ScoreScreenVm.MainSeconds;

            DataContext.WrestlingMatch.PointsRed = ScoreScreenVm.Points1;
            DataContext.WrestlingMatch.PointsBlue = ScoreScreenVm.Points2;
            DataContext.WrestlingMatch.MatchActions = _matchActions;
            DataContext.WrestlingMatch.StartDateTime = _startDateTime;

            DataContext.WrestlingMatch.MaxRoundSecond = ScoreScreenVm.MaxRoundSecond;
            DataContext.WrestlingMatch.MaxActionSecond = ScoreScreenVm.MaxActionSecond;
            DataContext.WrestlingMatch.MaxTimeoutSecond = ScoreScreenVm.MaxTimeoutSecond;
            DataContext.WrestlingMatch.BestActionRed = ScoreScreenVm.BestActionRed;
            DataContext.WrestlingMatch.BestActionRedCount = ScoreScreenVm.BestActionRedCount;
            DataContext.WrestlingMatch.BestActionBlue = ScoreScreenVm.BestActionBlue;
            DataContext.WrestlingMatch.BestActionBlueCount = ScoreScreenVm.BestActionBlueCount;
            DataContext.WrestlingMatch.IsLastActionRed = ScoreScreenVm.IsLastActionRed;
            DataContext.WrestlingMatch.WarningsNumberRed = ScoreScreenVm.Wrestler1WarningsNumber;
            DataContext.WrestlingMatch.WarningsNumberBlue = ScoreScreenVm.Wrestler2WarningsNumber;

            if (DataContext.Tournament == null)
            {
                DataContext.WrestlingMatch.WrestlerInRed.TeamName = ScoreScreenVm.Wrestler1TeamName;
                DataContext.WrestlingMatch.WrestlerInBlue.TeamName = ScoreScreenVm.Wrestler2TeamName;
                DataContext.WrestlingMatch.WrestlerInRed.LastName = ScoreScreenVm.Wrestler1;
                DataContext.WrestlingMatch.WrestlerInBlue.LastName = ScoreScreenVm.Wrestler2;

                if (int.TryParse(ScoreScreenVm.MatchFullNumber, out var matchNumber))
                {
                    DataContext.WrestlingMatch.MatchNumber = matchNumber;
                }

                DataContext.WrestlingMatch.RoundName = ScoreScreenVm.RoundName;
                DataContext.WrestlingMatch.GroupName = ScoreScreenVm.GroupLabel;
            }
        }

        private void SetupTimer()
        {
            IsRunning = false;

            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= TimerTick;
            }

            _timer = new DispatcherTimer(DispatcherPriority.Send);
            _timer.Tick += TimerTick;
            _timer.Interval = new TimeSpan(0, 0, 0, 1);
        }

        private void TimerTick(object sender, EventArgs e)
        {
            ScoreScreenVm.MainSeconds++;

            if (ScoreScreenVm.IsTimeout)
            {
                HandleTimeoutTimer();
            }
            else
            {
                HandleRoundTimer();
            }
        }

        private BitmapImage GetActionPathByEnabled(bool isEnabled)
        {
            string cancelPath = "\\Resources\\30sec_cancel.png";
            string enablePath = "\\Resources\\30sec.png";

            return new BitmapImage(new Uri(isEnabled ? enablePath : cancelPath, UriKind.Relative));
        }

        private void ResetBestActions()
        {
            var redBestAction = _matchActions.Where(a => a.IsForRed.HasValue && a.IsForRed.Value).OrderByDescending(a => a.Points).FirstOrDefault();
            if (redBestAction != null)
            {
                ScoreScreenVm.BestActionRed = redBestAction.Points;

                var actionCount = _matchActions.Count(a => a.IsForRed.HasValue && a.IsForRed.Value && a.Points == redBestAction.Points);
                ScoreScreenVm.BestActionRedCount = actionCount;
            }

            var blueBestAction = _matchActions.Where(a => a.IsForRed.HasValue && !a.IsForRed.Value).OrderByDescending(a => a.Points).FirstOrDefault();
            if (blueBestAction != null)
            {
                ScoreScreenVm.BestActionBlue = blueBestAction.Points;

                var actionCount = _matchActions.Count(a => a.IsForRed.HasValue && !a.IsForRed.Value && a.Points == blueBestAction.Points);
                ScoreScreenVm.BestActionBlueCount = actionCount;
            }
        }

        // Last SetPoints action (any side, or filtered by side). Used to
        // restore IsLastActionRed after a points revert so the «advantage by
        // last action» tiebreaker stays consistent.
        private MatchAction GetLastWrestlerPointsAction(bool? isRed)
        {
            for (int i = _matchActions.Count - 1; i >= 0; i--)
            {
                var action = _matchActions[i];
                if (action.Type != MatchActionType.SetPoints) continue;
                if (isRed.HasValue && action.IsForRed != isRed.Value) continue;
                return action;
            }

            return null;
        }

        // Per-side undo (XAML «↩» button). Walks history scoped to the side
        // and emits the appropriate Revert action.
        private void AdjustLastPoint(bool isForRed)
        {
            TryRevertLastAction(isForRed);
        }

        // Global undo (Backspace). Walks history without a side filter and
        // undoes the most recent undoable action across both wrestlers.
        private void RevertLastAction()
        {
            TryRevertLastAction(null);
        }

        // Walks _matchActions in reverse, tracking how many SetPoints /
        // SetWarning / ShowActionTimer actions have already been cancelled by
        // a later Revert / Hide / Expire. The first undoable action that
        // hasn't been cancelled is reverted; if everything visible is already
        // cancelled, nothing happens.
        //
        // Pending counters are tracked per side so that, e.g., reverting Red
        // doesn't accidentally consume Blue's revert quota.
        private void TryRevertLastAction(bool? sideFilter)
        {
            int pendingRevertPointsRed = 0, pendingRevertPointsBlue = 0;
            int pendingRevertWarningRed = 0, pendingRevertWarningBlue = 0;
            int pendingHideTimerRed = 0, pendingHideTimerBlue = 0;

            for (int i = _matchActions.Count - 1; i >= 0; i--)
            {
                var a = _matchActions[i];
                if (sideFilter.HasValue && a.IsForRed != sideFilter.Value) continue;

                bool red = a.IsForRed == true;

                switch (a.Type)
                {
                    case MatchActionType.RevertPoints:
                        if (red) pendingRevertPointsRed++; else pendingRevertPointsBlue++;
                        continue;
                    case MatchActionType.RevertWarning:
                        if (red) pendingRevertWarningRed++; else pendingRevertWarningBlue++;
                        continue;
                    case MatchActionType.HideActionTimer:
                    case MatchActionType.ActionTimerExpired:
                        if (red) pendingHideTimerRed++; else pendingHideTimerBlue++;
                        continue;

                    case MatchActionType.SetPoints:
                        if (red)
                        {
                            if (pendingRevertPointsRed > 0) { pendingRevertPointsRed--; continue; }
                        }
                        else
                        {
                            if (pendingRevertPointsBlue > 0) { pendingRevertPointsBlue--; continue; }
                        }
                        UndoSetPoints(a);
                        return;

                    case MatchActionType.SetWarning:
                        if (red)
                        {
                            if (pendingRevertWarningRed > 0) { pendingRevertWarningRed--; continue; }
                        }
                        else
                        {
                            if (pendingRevertWarningBlue > 0) { pendingRevertWarningBlue--; continue; }
                        }
                        UndoSetWarning(a);
                        return;

                    case MatchActionType.ShowActionTimer:
                        if (red)
                        {
                            if (pendingHideTimerRed > 0) { pendingHideTimerRed--; continue; }
                        }
                        else
                        {
                            if (pendingHideTimerBlue > 0) { pendingHideTimerBlue--; continue; }
                        }
                        UndoShowActionTimer(a);
                        return;

                    default:
                        continue;
                }
            }
        }

        private void UndoSetPoints(MatchAction a)
        {
            bool isForRed = a.IsForRed == true;
            if (isForRed)
            {
                ScoreScreenVm.Points1 = Math.Max(0, ScoreScreenVm.Points1 - a.Points);
            }
            else
            {
                ScoreScreenVm.Points2 = Math.Max(0, ScoreScreenVm.Points2 - a.Points);
            }

            // Restore «last action by side» from the SetPoints just before
            // the one we're cancelling — needed by the tiebreaker logic in
            // MatchResultsViewModel.SetWinnerAndWinType.
            var prior = GetLastWrestlerPointsAction(null);
            if (prior != null && prior != a && prior.IsForRed.HasValue)
            {
                ScoreScreenVm.IsLastActionRed = prior.IsForRed.Value;
            }

            AddAction(MatchActionType.RevertPoints, isForRed: isForRed, points: a.Points);
            ResetBestActions();
            CalculateAdvantage();
        }

        private void UndoSetWarning(MatchAction a)
        {
            bool isForRed = a.IsForRed == true;
            if (isForRed)
            {
                ScoreScreenVm.Wrestler1WarningsNumber = Math.Max(0, ScoreScreenVm.Wrestler1WarningsNumber - 1);
            }
            else
            {
                ScoreScreenVm.Wrestler2WarningsNumber = Math.Max(0, ScoreScreenVm.Wrestler2WarningsNumber - 1);
            }
            AddAction(MatchActionType.RevertWarning, isForRed: isForRed);
        }

        private void UndoShowActionTimer(MatchAction a)
        {
            // Re-toggle the activity timer off via the existing path so the
            // visibility / image / SecondarySeconds bookkeeping stays in
            // sync. ActionStartStop logs HideActionTimer for us.
            bool isForRed = a.IsForRed == true;
            ActionStartStop(isForRed ? "Red" : "Blue");
        }

        private void AddPoints(bool isForRed, int value)
        {
            if (isForRed)
            {
                ScoreScreenVm.Points1 = ScoreScreenVm.Points1 + value;
            } 
            else
            {
                ScoreScreenVm.Points2 = ScoreScreenVm.Points2 + value;
            }            

            if (value > 0)
            {
                if (isForRed)
                {
                    if (ScoreScreenVm.BestActionRed < value)
                    {
                        ScoreScreenVm.BestActionRed = value;
                        ScoreScreenVm.BestActionRedCount = 1;
                    }
                    else if (ScoreScreenVm.BestActionRed == value)
                    {
                        ScoreScreenVm.BestActionRedCount++;
                    }
                }
                else
                {
                    if (ScoreScreenVm.BestActionBlue < value)
                    {
                        ScoreScreenVm.BestActionBlue = value;
                        ScoreScreenVm.BestActionBlueCount = 1;
                    }
                    else if (ScoreScreenVm.BestActionBlue == value)
                    {
                        ScoreScreenVm.BestActionBlueCount++;
                    }
                }

                AddAction(MatchActionType.SetPoints, isForRed: isForRed, points: value);

                ScoreScreenVm.IsLastActionRed = isForRed;
            }
            else
            {
                // Correction
                AdjustLastPoint(isForRed);
            }

            if (ScoreScreenVm.Points1 < 0)
            {
                ScoreScreenVm.Points1 = 0;
            }

            if (ScoreScreenVm.Points2 < 0)
            {
                ScoreScreenVm.Points2 = 0;
            }
        }

        private void AdjustPoints(string param)
        {
            var items = param.Split(',');
            var value = Convert.ToInt32(items[0]);
            bool isRed = items[1] == "Red";

            if ((ScoreScreenVm.IsAction1TimerEnabled || ScoreScreenVm.IsAction2TimerEnabled) && value > 0)
            {
                if (isRed && ScoreScreenVm.IsAction1TimerEnabled || !isRed && ScoreScreenVm.IsAction2TimerEnabled)
                {
                    ScoreScreenVm.SecondarySeconds = 0;
                    ScoreScreenVm.IsAction1TimerEnabled = false;
                    ScoreScreenVm.IsAction2TimerEnabled = false;

                    Action1Image = GetActionPathByEnabled(true);
                    Action2Image = GetActionPathByEnabled(true);

                    Action1Visibility = Visibility.Visible;
                    Action2Visibility = Visibility.Visible;
                }
            }

            AddPoints(isRed, value);

            CalculateAdvantage();
        }

        private void AdjustTimer(int deltaRemainingSeconds)
        {
            var mainDelta = ScoreScreenVm.IsTimerBackward ? -deltaRemainingSeconds : deltaRemainingSeconds;
            var max = ScoreScreenVm.IsTimeout ? ScoreScreenVm.MaxTimeoutSecond : ScoreScreenVm.MaxRoundSecond;

            var newSeconds = ScoreScreenVm.MainSeconds + mainDelta;
            if (newSeconds < 0) newSeconds = 0;
            if (newSeconds > max) newSeconds = max;

            if (newSeconds == ScoreScreenVm.MainSeconds) return;

            ScoreScreenVm.MainSeconds = newSeconds;

            AddAction(MatchActionType.TimerAdjusted, points: deltaRemainingSeconds);
        }

        private void CalculateAdvantage()
        {
            ScoreScreenVm.IsPlayer1WithAdvantage = false;
            ScoreScreenVm.IsPlayer2WithAdvantage = false;

            if (ScoreScreenVm.Points1 == ScoreScreenVm.Points2 && ScoreScreenVm.Points1 > 0)
            {
                // Show advantage for wrestler if any
                if (ScoreScreenVm.BestActionRed > ScoreScreenVm.BestActionBlue)
                {
                    ScoreScreenVm.IsPlayer1WithAdvantage = true;
                }
                else if (ScoreScreenVm.BestActionBlue > ScoreScreenVm.BestActionRed)
                {
                    ScoreScreenVm.IsPlayer2WithAdvantage = true;
                }
                else if (ScoreScreenVm.BestActionRed == ScoreScreenVm.BestActionBlue && ScoreScreenVm.BestActionRedCount > ScoreScreenVm.BestActionBlueCount)
                {
                    ScoreScreenVm.IsPlayer1WithAdvantage = true;
                }
                else if (ScoreScreenVm.BestActionRed == ScoreScreenVm.BestActionBlue && ScoreScreenVm.BestActionBlueCount > ScoreScreenVm.BestActionRedCount)
                {
                    ScoreScreenVm.IsPlayer2WithAdvantage = true;
                }
                else
                {
                    ScoreScreenVm.IsPlayer1WithAdvantage = ScoreScreenVm.IsLastActionRed;
                    ScoreScreenVm.IsPlayer2WithAdvantage = !ScoreScreenVm.IsLastActionRed;
                }
            }
        }

        private void SetActionTimers()
        {
            Action1Image = GetActionPathByEnabled(!ScoreScreenVm.IsAction1TimerEnabled);
            Action1Visibility = ScoreScreenVm.IsAction2TimerEnabled ? Visibility.Hidden : Visibility.Visible;
            
            Action2Image = GetActionPathByEnabled(!ScoreScreenVm.IsAction2TimerEnabled);
            Action2Visibility = ScoreScreenVm.IsAction1TimerEnabled ? Visibility.Hidden : Visibility.Visible;
        }

        private void ActionStartStop(string param)
        {
            bool isRed = param == "Red";

            // Mutual exclusion: only one wrestler's activity timer may run.
            // The on-screen UI hides the inactive button (so a mouse user
            // can't enable both), but hotkeys (A / Shift+A) bypass that —
            // pre-empt the opposite side here so the rule holds for every
            // entry point. Side-switch logs a Hide for the side being
            // turned off so Backspace history stays consistent.
            if (isRed && ScoreScreenVm.IsAction2TimerEnabled)
            {
                ScoreScreenVm.IsAction2TimerEnabled = false;
                Action2Image = GetActionPathByEnabled(true);
                Action1Visibility = Visibility.Visible;
                AddAction(MatchActionType.HideActionTimer, isForRed: false);
            }
            else if (!isRed && ScoreScreenVm.IsAction1TimerEnabled)
            {
                ScoreScreenVm.IsAction1TimerEnabled = false;
                Action1Image = GetActionPathByEnabled(true);
                Action2Visibility = Visibility.Visible;
                AddAction(MatchActionType.HideActionTimer, isForRed: true);
            }

            if (isRed)
            {
                ScoreScreenVm.IsAction1TimerEnabled = !ScoreScreenVm.IsAction1TimerEnabled;
                Action1Image = GetActionPathByEnabled(!ScoreScreenVm.IsAction1TimerEnabled);

                Action2Visibility = ScoreScreenVm.IsAction1TimerEnabled ? Visibility.Hidden : Visibility.Visible;
                AddAction(
                    ScoreScreenVm.IsAction1TimerEnabled ? MatchActionType.ShowActionTimer : MatchActionType.HideActionTimer,
                    isForRed: true,
                    points: ScoreScreenVm.IsAction1TimerEnabled ? ScoreScreenVm.MaxActionSecond : 0);
            }
            else
            {
                ScoreScreenVm.IsAction2TimerEnabled = !ScoreScreenVm.IsAction2TimerEnabled;
                Action2Image = GetActionPathByEnabled(!ScoreScreenVm.IsAction2TimerEnabled);
                Action1Visibility = ScoreScreenVm.IsAction2TimerEnabled ? Visibility.Hidden : Visibility.Visible;
                AddAction(
                    ScoreScreenVm.IsAction2TimerEnabled ? MatchActionType.ShowActionTimer : MatchActionType.HideActionTimer,
                    isForRed: false,
                    points: ScoreScreenVm.IsAction2TimerEnabled ? ScoreScreenVm.MaxActionSecond : 0);
            }

            ScoreScreenVm.SecondarySeconds = 0;
        }

        private void HandleActionTimer()
        {
            if (ScoreScreenVm.SecondarySeconds >= ScoreScreenVm.MaxActionSecond)
            {
                // Capture which side's activity timer expired *before* the
                // booleans below are reset — Backspace / undo logic uses the
                // side to know which Show is being cancelled.
                bool? expiredFor = ScoreScreenVm.IsAction1TimerEnabled
                    ? true
                    : ScoreScreenVm.IsAction2TimerEnabled ? false : (bool?)null;
                AddAction(MatchActionType.ActionTimerExpired, isForRed: expiredFor);

                if (ScoreScreenVm.IsAction1TimerEnabled)
                {
                    AdjustPoints("1,Blue");
                }
                else if (ScoreScreenVm.IsAction2TimerEnabled)
                {
                    AdjustPoints("1,Red");
                }

                ScoreScreenVm.IsAction1TimerEnabled = false;
                ScoreScreenVm.IsAction2TimerEnabled = false;
                ScoreScreenVm.SecondarySeconds = 0;

                Action1Image = GetActionPathByEnabled(true);
                Action2Image = GetActionPathByEnabled(true);

                Action1Visibility = Visibility.Visible;
                Action2Visibility = Visibility.Visible;
            }
        }

        private void HandleTimeoutTimer()
        {
            if (ScoreScreenVm.MainSeconds >= ScoreScreenVm.MaxTimeoutSecond)
            {
                if (ScoreScreenVm.IsSoundEnabled)
                {
                    PlayTrippleGongSound();
                    AddAction(MatchActionType.StopTimeout);
                }

                _timer?.Stop();
                IsRunning = false;

                ScoreScreenVm.IsTimeout = false;
                ScoreScreenVm.MainSeconds = 0;
                ScoreScreenVm.Round = 2;

                OnPropertyChanged("IsStartButtonVisible");
                OnPropertyChanged("IsStopButtonVisible");
            }
        }

        private void HandleRoundTimer()
        {
            DataContext.WrestlingMatch.LastSecondInMatch++;

            if (ScoreScreenVm.IsAction1TimerEnabled || ScoreScreenVm.IsAction2TimerEnabled)
            {
                ScoreScreenVm.SecondarySeconds++;
                HandleActionTimer();
            }
            else
            {
                ScoreScreenVm.SecondarySeconds = 0;
            }

            if (ScoreScreenVm.MainSeconds >= ScoreScreenVm.MaxRoundSecond)
            {
                if (ScoreScreenVm.IsSoundEnabled)
                {
                    PlaySingleGongSound();
                    AddAction(MatchActionType.RoundFinished, points: ScoreScreenVm.Round);
                }

                _timer?.Stop();
                IsRunning = false;

                if (ScoreScreenVm.Round == 1)
                {
                    ScoreScreenVm.IsTimeout = true;
                    ScoreScreenVm.MainSeconds = 0;                    

                    ScoreScreenVm.Round = 2;

                    if (_timer == null)
                    {
                        SetupTimer();
                    }

                    _timer.Start();                    

                    AddAction(MatchActionType.StartTimeout);

                    OnPropertyChanged("IsStartButtonVisible");
                    OnPropertyChanged("IsStopButtonVisible");
                }

                OnPropertyChanged("IsMatchNotCompleted");
            }
        }

        #endregion
    }
}