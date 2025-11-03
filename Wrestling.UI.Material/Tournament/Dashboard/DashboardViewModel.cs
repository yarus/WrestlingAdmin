using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Settings;
using Wrestling.UI.Material.Slider;
using Wrestling.UI.Material.Tournament.Import;
using Wrestling.UI.Material.Tournament.Progress.Brackets;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Wrestling.UI.Material.Tournament.Results;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Dashboard
{
    public class DashboardViewModel : TournamentViewModelBase
    {
        #region Fields

        private ICommand _openBracketCommand;
        private ICommand _openStandingCommand;
        private ICommand _openResultsCommand;
        private ICommand _openSliderControlCommand;

        private DispatcherTimer _timer;
        private int _currentSecond;
        private bool _isSaving;

        private IList<CommandButtonItem> _quickButtons;
        private IList<CommandButtonItem> _drawerItems;

        private readonly CommandButtonItem _saveQuickCommand;

        private IPanelView _scoreScreenView;
        private ScoreScreenViewModel _scoreScreenVm;

        #endregion


        public DashboardViewModel(IDiContainer container) : base(container)
        {
            _saveQuickCommand = new CommandButtonItem("Сохранить турнир", PackIconKind.ContentSave, 
                new AsyncRelayCommand(execute: async _ => await SaveDataAsync()));
        }

        public override void InitData()
        {
            base.InitData();

            _scoreScreenView = Resolve<IPanelView>("ScoreScreen");
            _scoreScreenVm = Resolve<ScoreScreenViewModel>();

            SetupAutoSave();
        }

        public override string PageTitle => "Вольная борьба - Администратор турниров версия 20251101";

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                return _quickButtons ?? 
                (
                    _quickButtons = new List<CommandButtonItem>
                    {
                        _saveQuickCommand
                    }
                );
            }
        }

        public override IList<CommandButtonItem> DrawerItems
        {
            get
            {
                return _drawerItems ?? (_drawerItems = new List<CommandButtonItem>
                {
                    new CommandButtonItem("Импорт", new RelayCommand(param => OpenImport(), param => true)),
                    new CommandButtonItem("Табло", new RelayCommand(param => OpenMonitor(), param => true)),
                    new CommandButtonItem("Настройки", new RelayCommand(param => OpenSettings(), param => true)),
                    new CommandButtonItem("Закрыть", new AsyncRelayCommand(param => CloseTournament(), param => true))
                });
            }
        }

        protected override void OnBackCommand()
        {
            base.OnBackCommand();

            _timer.Stop();
        }

        #region Commands

        public ICommand OpenSliderControlCommand
        {
            get
            {
                if (_openSliderControlCommand == null)
                {
                    _openSliderControlCommand = new RelayCommand(param => OpenSliderControl(), param => true);
                }
                return _openSliderControlCommand;
            }
        }

        public ICommand OpenStandingCommand
        {
            get
            {
                if (_openStandingCommand == null)
                {
                    _openStandingCommand = new RelayCommand(param => OpenStanding(), param => true);
                }
                return _openStandingCommand;
            }
        }

        public ICommand OpenResultsCommand
        {
            get
            {
                if (_openResultsCommand == null)
                {
                    _openResultsCommand = new RelayCommand(param => OpenResults(), param => true);
                }
                return _openResultsCommand;
            }
        }

        public ICommand OpenBracketCommand
        {
            get
            {
                if (_openBracketCommand == null)
                {
                    _openBracketCommand = new RelayCommand(param => OpenDirector(), param => true);
                }
                return _openBracketCommand;
            }
        }
        
        #endregion

        #region Private Methods

        private void OpenImport()
        {
            NavigateToView<ImportViewModel>();
        }

        private void OpenStanding()
        {
            NavigateToView<StandingViewModel>();
        }

        private void OpenResults()
        {
            NavigateToView<ResultsViewModel>();
        }

        private void OpenMonitor()
        {
            _scoreScreenVm.IsSoundEnabled = Tournament.Settings.IsSoundEnabled;
            _scoreScreenVm.IsTimerBackward = Tournament.Settings.IsTimerBackward;
            _scoreScreenVm.MaxActionSecond = Tournament.Settings.MaxActionSecond;
            _scoreScreenVm.MaxRoundSecond = Tournament.Settings.MaxRoundSecond;
            _scoreScreenVm.MaxTimeoutSecond = Tournament.Settings.MaxTimeoutSecond;
            _scoreScreenVm.TournamentTitle = Tournament.Name;
            _scoreScreenVm.Round = 1;

            _scoreScreenView.ShowScreen(_scoreScreenVm);
        }

        private void OpenSliderControl()
        {
            NavigateToView<SliderControlViewModel>();
        }

        private void OpenDirector()
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
        
        private void OpenSettings()
        {
            NavigateToView<SettingsViewModel>();
        }

        private void SetupAutoSave()
        {
            _timer?.Stop();

            if (DataContext.Tournament != null && IsAutosaveEnabled)
            {
                if (string.IsNullOrEmpty(DataContext.Tournament.FileName)) SaveDataSync();

                if (QuickButtons.Contains(_saveQuickCommand)) QuickButtons.Remove(_saveQuickCommand);

                SetupTimer();
            }
            else
            {
                if (!QuickButtons.Contains(_saveQuickCommand)) QuickButtons.Add(_saveQuickCommand);
            }
        }

        private void SetupTimer()
        {
            _timer?.Stop();

            _timer = new DispatcherTimer();
            _timer.Tick += OnTimerTick;
            _timer.Interval = new TimeSpan(0, 0, 0, 1);

            _timer.Start();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (DataContext.Tournament == null)
            {
                _currentSecond = 0;
                _timer.Stop();
                return;
            }

            _currentSecond++;

            if (_currentSecond >= DataContext.Tournament.Settings.AutosaveMaxSecond && !_isSaving)
            {
                _isSaving = true;
                _currentSecond = 0;

                // Fire and forget the async operation, but capture the task to observe exceptions
                var _ = SaveDataAsyncWrapper();
            }
        }

        private async Task SaveDataAsyncWrapper()
        {
            try
            {
                await SaveDataAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Handle or log the exception appropriately
                Debug.WriteLine($"Autosave failed: {ex.Message}");
                // Consider rethrowing or showing a message to the user
            }
            finally
            {
                _isSaving = false;
            }
        }

        #endregion
    }
}