using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.Recorder;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.ReplayScreen;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Tournament.Progress.Brackets;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Wrestling.UI.Material.Utils.Recording;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Match
{
    public class MatchControlViewModel : ViewModelBase
    {
        #region Fields

        private IMatchRecorder _currentRecorder;
        
        private DateTime? _startDateTime;
        private List<MatchAction> _matchActions;

        private DispatcherTimer _timer;

        private bool _isRunning;
        private bool _isSettingsOpen;

        private IPanelView _scoreScreenView;

        private ScoreScreenViewModel _scoreScreen;

        private GlobalSettings _settings;
        private RecorderConfiguration _recConfig;

        private BitmapImage _action1Image;
        private BitmapImage _action2Image;
        private Visibility _action1Visibility;
        private Visibility _action2Visibility;
        
        private ICommand _startCommand;
        private ICommand _stopCommand;
        private ICommand _adjustPointsCommand;
        private ICommand _actionStartStopCommand;
        private ICommand _resetCommand;
        private ICommand _changeWarningsCommand;

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
                //_currentRecorder?.CreateOverlay(_isRunning);
                OnPropertyChanged("IsStartButtonVisible");
                OnPropertyChanged("IsStopButtonVisible");
            }
        }

        public bool IsVideoRecording => _currentRecorder?.IsRecording ?? false;

        public override bool IsBackButtonAvailable => true;
        public bool IsStartButtonVisible => IsMatchNotCompleted && !IsRunning;// && (ScoreScreenVm != null && !ScoreScreenVm.IsTimeout);
        public bool IsStopButtonVisible => IsMatchNotCompleted && IsRunning;// && (ScoreScreenVm != null && !ScoreScreenVm.IsTimeout);

        public override string PageTitle => "Управление Электронным Табло";

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                if (_quickButtons == null)
                {
                    _quickButtons = new List<CommandButtonItem>();

                    var settings = DataContext.Tournament != null ? DataContext.Tournament.Settings : GlobalSettings;

                    if (settings.IsVideoRecordingEnabled)
                    {
                        _quickButtons.Add(new CommandButtonItem("Открыть запись", PackIconKind.Camcorder, new RelayCommand(param => ShowReplayScreen(), param => true)));
                    }

                    _quickButtons.Add(new CommandButtonItem("Открыть электронное табло", PackIconKind.Monitor, new RelayCommand(param => ShowScreen(), param => true)));
                    _quickButtons.Add(new CommandButtonItem("Сбросить поединок", PackIconKind.BackupRestore, new RelayCommand(param => Reset(), param => true)));

                    if (DataContext.Tournament == null)
                    {
                        _quickButtons.Add(new CommandButtonItem("Настройки поединка", PackIconKind.Settings, new RelayCommand(param => ShowSettings(), param => true)));
                    }

                    _quickButtons.Add(new CommandButtonItem("Завершить поединок", PackIconKind.Check, new RelayCommand(param => CompleteMatch(), param => true)));
                }

                return _quickButtons;
            }
        }

        public override void InitData()
        {
            base.InitData();

            _scoreScreenView = Resolve<IPanelView>("ScoreScreen");
            _scoreScreen = Resolve<ScoreScreenViewModel>();
            _recConfig = Resolve<RecorderConfiguration>();

            _quickButtons = null;

            if (DataContext.WrestlingMatch == null)
            {
                ShowSnackMessage("Матч не инициализирован!");
                OnBackCommand();
                return;
            }

            SetupTimer();

            if (DataContext.WrestlingMatch.LastSecondInMatch == 0)
            {
                _matchActions = new List<MatchAction>();
            }
            else
            {
                _matchActions = DataContext.WrestlingMatch.MatchActions;
            }

            if (DataContext.Tournament == null)
            {
                _settings = Resolve<GlobalSettings>();
            }
            else
            {
                _settings = DataContext.Tournament.Settings;
            }

            _currentRecorder = Resolve<IMatchRecorder>();
            //_recorderGen = Resolve<App.IMatchRecorderGenerator>();

            _scoreScreen.InitData();

            CalculateAdvantage();

            SetActionTimers();
            
            if (_settings.IsVideoRecordingEnabled && IsMatchNotCompleted && !IsRunning && !IsVideoRecording)
            {
                StartRecording();
            }

            var keyHandler = Resolve<IKeyHandler>();
            if (keyHandler != null)
            {
                keyHandler.KeyPressed -= KeyHandler_KeyPressed;
                keyHandler.KeyPressed += KeyHandler_KeyPressed;
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


        public ICommand ResetCommand
        {
            get
            {
                if (_resetCommand == null)
                {
                    _resetCommand = new RelayCommand(
                        param => Reset(),
                        param => true
                    );
                }
                return _resetCommand;
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

        private ImageSource _currentFrame = null;
        public ImageSource CurrentFrame
        {
            get { return _currentFrame; }
            set
            {
                _currentFrame = value;
                OnPropertyChanged("CurrentFrame");
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

        #endregion    }

        #region Private Methods

        private void KeyHandler_KeyPressed(object sender, KeyEventArgs e)
        {
            if (DataContext.WrestlingMatch == null || DataContext.WrestlingMatch.Status == MatchStatusEnum.Completed)
            {
                e.Handled = true;
                return;
            }

            if (!_isSettingsOpen && (e.Key == Key.Space || e.Key == Key.Enter))
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
            }
        }

        private async void ShowSettings()
        {
            var tmp = new ScoreScreenViewModel(DiContainer);
            tmp.InitData();

            tmp.TournamentTitle = ScoreScreenVm.TournamentTitle;
            tmp.CarpetLabel = ScoreScreenVm.CarpetLabel;
            tmp.GroupLabel = ScoreScreenVm.GroupLabel;
            tmp.MatchFullNumber = ScoreScreenVm.MatchFullNumber;
            tmp.RoundName = ScoreScreenVm.RoundName;
            tmp.MaxRoundSecond = ScoreScreenVm.MaxRoundSecond;
            tmp.MaxActionSecond = ScoreScreenVm.MaxActionSecond;
            tmp.MaxTimeoutSecond = ScoreScreenVm.MaxTimeoutSecond;
            tmp.Wrestler1 = ScoreScreenVm.Wrestler1;
            tmp.Wrestler1TeamName = ScoreScreenVm.Wrestler1TeamName;
            tmp.Wrestler2 = ScoreScreenVm.Wrestler2;
            tmp.Wrestler2TeamName = ScoreScreenVm.Wrestler2TeamName;
            tmp.Wrestler1TeamEmblem = ScoreScreenVm.Wrestler1TeamEmblem;
            tmp.Wrestler2TeamEmblem = ScoreScreenVm.Wrestler2TeamEmblem;
            tmp.Points1 = ScoreScreenVm.Points1;
            tmp.Points2 = ScoreScreenVm.Points2;
            tmp.Wrestler1WarningsNumber = ScoreScreenVm.Wrestler1WarningsNumber;
            tmp.Wrestler2WarningsNumber = ScoreScreenVm.Wrestler2WarningsNumber;

            var vm = new MatchSettingsViewModel(DiContainer, tmp);
            vm.InitData();

            var view = new MatchSettingsDialog
            {
                DataContext = vm
            };

            _isSettingsOpen = true;

            var result = await DialogHost.Show(view, "RootDialog");
            
            if (result != null)
            {
                _isSettingsOpen = false;

                if ((bool) result)
                {
                    ScoreScreenVm.TournamentTitle = tmp.TournamentTitle;
                    ScoreScreenVm.CarpetLabel = tmp.CarpetLabel;
                    ScoreScreenVm.GroupLabel = tmp.GroupLabel;
                    ScoreScreenVm.MatchFullNumber = tmp.MatchFullNumber;
                    ScoreScreenVm.RoundName = tmp.RoundName;
                    ScoreScreenVm.MaxRoundSecond = tmp.MaxRoundSecond;
                    ScoreScreenVm.MaxActionSecond = tmp.MaxActionSecond;
                    ScoreScreenVm.MaxTimeoutSecond = tmp.MaxTimeoutSecond;
                    ScoreScreenVm.Wrestler1 = tmp.Wrestler1;
                    ScoreScreenVm.Wrestler1TeamName = tmp.Wrestler1TeamName;
                    ScoreScreenVm.Wrestler2 = tmp.Wrestler2;
                    ScoreScreenVm.Wrestler2TeamName = tmp.Wrestler2TeamName;
                    ScoreScreenVm.Wrestler1TeamEmblem = tmp.Wrestler1TeamEmblem;
                    ScoreScreenVm.Wrestler2TeamEmblem = tmp.Wrestler2TeamEmblem;

                    if (DataContext.Tournament == null)
                    {
                        CopyDataFromViewToMatch();
                    }
                }
            }
        }

        private void ShowReplayScreen()
        {
            StopRecording();

            _timer?.Stop();

            AddAction("Таймер остановлен", 0, null);

            CopyDataFromViewToMatch();

            NavigateToView<ReplayScreenViewModel>();
        }

        protected override void OnNavigatingOut()
        {
            base.OnNavigatingOut();

            var keyHandler = Resolve<IKeyHandler>();
            if (keyHandler != null)
            {
                keyHandler.KeyPressed -= KeyHandler_KeyPressed;
            }
        }

        private void ShowScreen()
        {
            _scoreScreenView.ShowScreen(_scoreScreen);
        }

        private void AddPointsAction(bool isRed, int value)
        {
            AddAction($"Действие борца в {(isRed ? "красном" : "синем")} трико оценено в {value}", value, isRed);
        }

        private void AddWarningAction(bool isRed, int value)
        {
            AddAction($"Борец в {(isRed ? "красном" : "синем")} трико получил {value} предупреждение", value, isRed);
        }

        private void AddAction(string text, int points, bool? isForRed)
        {
            _matchActions.Add(new MatchAction
            {
                DateTime = DateTime.Now,
                RoundNumber = ScoreScreenVm.Round,
                SecondInRound = ScoreScreenVm.MainSeconds,
                Text = text,
                IsForRed = isForRed,
                Points = points
            });
        }

        private void StartRecording()
        {
            try
            {
                _currentRecorder?.StartRecording(
                    _settings.VideoStoragePath,
                    _recConfig,
                    ScoreScreenVm,
                    DataContext.Tournament?.ID,
                    (sender, bs)=> 
                    {
                        CurrentFrame = bs;
                    });

                _currentRecorder.SetMaxSeconds(ScoreScreenVm.MaxRoundSecond);
                _currentRecorder.SetMainSecond(ScoreScreenVm.MainSeconds);
                _currentRecorder.SetTimerOffset(ScoreScreenVm.MainSeconds * 1000);
                _currentRecorder?.CreateOverlay(true);
            }
            catch(Exception ex)
            {
                ShowSnackMessage($"При попытке начать видеозапись произошла ошибка: {ex.Message}");

                try
                {
                    StopRecording();
                }
                catch
                {
                }
            }


            OnPropertyChanged("IsVideoRecording");
        }

        private void StopRecording()
        {
            _currentRecorder?.StopRecording();

            OnPropertyChanged("IsVideoRecording");
        }

        private void DeleteRecording()
        {
            _currentRecorder?.DeleteRecording(_settings.VideoStoragePath, DataContext.WrestlingMatch.MatchNumber, DataContext.Tournament?.ID);
        }

        private void Stop()
        {
            _timer?.Stop();
            //_currentRecorder.CreateOverlay(false);
            AddAction("Таймер остановлен", 0, null);

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

            AddAction("Таймер запущен", 0, null);

            _currentRecorder.SetMainSecond(ScoreScreenVm.MainSeconds);
            _currentRecorder.SetTimerOffset(ScoreScreenVm.MainSeconds * 1000);

            if (ScoreScreenVm.MainSeconds == 0 && ScoreScreenVm.IsSoundEnabled)
            {
                PlaySingleGongSound();
            }

            // If recording was stopped we need to start it again
            /*if (_settings.IsVideoRecordingEnabled && !_currentRecorder.IsRecording)
            {
                StartRecording();
            }*/

            //_currentRecorder?.CreateOverlay(true);

            IsRunning = true;
        }

        private void PlaySingleGongSound()
        {
            var singleGongPath = GlobalSettings.StartGongSoundPath; //AppDomain.CurrentDomain.BaseDirectory + "Sounds\\SingleGongBeep.wav";
            if (File.Exists(singleGongPath))
            {
                SoundPlayer sp = new SoundPlayer(singleGongPath);
                sp.Play();
            }
        }

        private void PlayTrippleGongSound()
        {
            var trippleGongPath = GlobalSettings.EndGongSoundPath; //AppDomain.CurrentDomain.BaseDirectory + "Sounds\\TripleGongBeep.wav";
            if (File.Exists(trippleGongPath))
            {
                SoundPlayer sp = new SoundPlayer(trippleGongPath);
                sp.Play();
            }
        }

        private void Reset()
        {
            Stop();

            if (_settings.IsVideoRecordingEnabled)
            {
                StopRecording();

                if (!DataContext.WrestlingMatch.IsMatchCompleted)
                {
                    DeleteRecording();
                }
            }

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
                AddWarningAction(true, ScoreScreenVm.Wrestler1WarningsNumber);
            }
            else
            {
                ScoreScreenVm.Wrestler2WarningsNumber += value;
                AddWarningAction(false, ScoreScreenVm.Wrestler2WarningsNumber);
            }
        }

        protected override void OnBackCommand()
        {
            if (Dialog.ShowMessageBox(this,
                    "Матч не звершен! Если вы вернетесь назад, то текущие результаты будут потеряны. Вы уверены, что хотите вернуться?",
                    "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

            if (!DataContext.WrestlingMatch.IsMatchCompleted)
            {
                Reset();

                DataContext.WrestlingMatch = null;
            }

            if (DataContext.Tournament != null)
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
            else
            {
                NavigateToView<HomeViewModel>();
            }
        }

        private void CompleteMatch()
        {
            _timer?.Stop();

            if (_isRunning)
            {
                IsRunning = false;
            }

            ScoreScreenVm.IsTimeout = false;

            //_currentRecorder.StopRecording();

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

        //private Stopwatch _timerSw = new Stopwatch();

        private void TimerTick(object sender, EventArgs e)
        {
            //ScoreScreenVm.MainSeconds = Convert.ToInt32(_timerSw.ElapsedMilliseconds / 1000L);
            ScoreScreenVm.MainSeconds++;

            _currentRecorder.SetMainSecond(ScoreScreenVm.MainSeconds);

            if (ScoreScreenVm.IsAction1TimerEnabled || ScoreScreenVm.IsAction2TimerEnabled)
            {
                ScoreScreenVm.SecondarySeconds++;
                HandleActionTimer();
            }
            else
            {
                ScoreScreenVm.SecondarySeconds = 0;
            }

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

        private MatchAction GetLastWrestlerPointsAction(bool? isRed)
        {
            for (int i = _matchActions.Count - 1; i >= 0; i--)
            {
                var action = _matchActions[i];
                if (action.Points > 0 && (!isRed.HasValue || action.IsForRed.Value == isRed.Value)) return action;
            }

            return null;
        }
        
        private void AdjustLastPoint(bool isForRed)
        {
            var lastPoint = GetLastWrestlerPointsAction(isForRed);
            if (lastPoint != null)
            {
                if (isForRed)
                {
                    ScoreScreenVm.Points1 -= lastPoint.Points;
                } 
                else
                {
                    ScoreScreenVm.Points2 -= lastPoint.Points;
                }

                _matchActions.Remove(lastPoint);

                var lastPointsAction = GetLastWrestlerPointsAction(null);
                if (lastPointsAction != null)
                {
                    ScoreScreenVm.IsLastActionRed = lastPointsAction.IsForRed.Value;
                }

                AddAction($"Коррекция баллов для борца в красном на {lastPoint.Points}", 0, lastPoint.IsForRed);

                ResetBestActions();
            }
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

                AddPointsAction(isForRed, value);

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
                ScoreScreenVm.SecondarySeconds = 0;
                ScoreScreenVm.IsAction1TimerEnabled = false;
                ScoreScreenVm.IsAction2TimerEnabled = false;

                Action1Image = GetActionPathByEnabled(true);
                Action2Image = GetActionPathByEnabled(true);

                Action1Visibility = Visibility.Visible;
                Action2Visibility = Visibility.Visible;
            }

            AddPoints(isRed, value);

            CalculateAdvantage();
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

            if (isRed)
            {
                ScoreScreenVm.IsAction1TimerEnabled = !ScoreScreenVm.IsAction1TimerEnabled;
                Action1Image = GetActionPathByEnabled(!ScoreScreenVm.IsAction1TimerEnabled);
                
                Action2Visibility = ScoreScreenVm.IsAction1TimerEnabled ? Visibility.Hidden : Visibility.Visible;
                AddAction($"{(ScoreScreenVm.IsAction1TimerEnabled ? "Запущен" : "Остановлен")} таймер действия для борца в красном трико", 0, null);
            }
            else
            {
                ScoreScreenVm.IsAction2TimerEnabled = !ScoreScreenVm.IsAction2TimerEnabled;
                Action2Image = GetActionPathByEnabled(!ScoreScreenVm.IsAction2TimerEnabled);
                Action1Visibility = ScoreScreenVm.IsAction2TimerEnabled ? Visibility.Hidden : Visibility.Visible;
                AddAction($"{(ScoreScreenVm.IsAction2TimerEnabled ? "Запущен" : "Остановлен")} таймер действия для борца в синем трико", 0, null);
            }

            ScoreScreenVm.SecondarySeconds = 0;
        }

        private void HandleActionTimer()
        {
            if (ScoreScreenVm.SecondarySeconds >= ScoreScreenVm.MaxActionSecond)
            {
                AddAction("Завершен таймер активности", 0, null);

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
                    AddAction("Таймаут завершен", 0, null);
                }

                _timer?.Stop();
                IsRunning = false;

                ScoreScreenVm.IsTimeout = false;
                ScoreScreenVm.MainSeconds = 0;
                ScoreScreenVm.Round = 2;

                _currentRecorder.SetMaxSeconds(ScoreScreenVm.MaxRoundSecond);
                _currentRecorder.SetMainSecond(ScoreScreenVm.MainSeconds);
                _currentRecorder.SetTimerOffset(ScoreScreenVm.MainSeconds * 1000);

                OnPropertyChanged("IsStartButtonVisible");
                OnPropertyChanged("IsStopButtonVisible");
            }
        }

        private void HandleRoundTimer()
        {
            DataContext.WrestlingMatch.LastSecondInMatch++;

            if (ScoreScreenVm.MainSeconds >= ScoreScreenVm.MaxRoundSecond)
            {
                if (ScoreScreenVm.IsSoundEnabled)
                {
                    PlaySingleGongSound();
                    AddAction($"Раунд {ScoreScreenVm.Round} завершен", 0, null);
                }

                _timer?.Stop();
                IsRunning = false;

                if (ScoreScreenVm.Round == 1)
                {
                    ScoreScreenVm.IsTimeout = true;
                    ScoreScreenVm.MainSeconds = 0;                    

                    _currentRecorder.SetMaxSeconds(ScoreScreenVm.MaxTimeoutSecond);
                    _currentRecorder.SetMainSecond(ScoreScreenVm.MainSeconds);

                    ScoreScreenVm.Round = 2;

                    if (_timer == null)
                    {
                        SetupTimer();
                    }

                    _timer.Start();                    

                    AddAction("Начался таймаут", 0, null);

                    OnPropertyChanged("IsStartButtonVisible");
                    OnPropertyChanged("IsStopButtonVisible");
                }

                OnPropertyChanged("IsMatchNotCompleted");
            }
        }

        #endregion
    }
}