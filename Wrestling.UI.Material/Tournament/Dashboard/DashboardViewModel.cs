using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
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

        private IList<CommandButtonItem> _quickButtons;
        private IList<CommandButtonItem> _drawerItems;

        private readonly CommandButtonItem _saveQuickCommand;
        private readonly CommandButtonItem _openLogsQuickCommand;

        private IPanelView _scoreScreenView;
        private ScoreScreenViewModel _scoreScreenVm;

        #endregion


        public DashboardViewModel(IDiContainer container) : base(container)
        {
            _saveQuickCommand = new CommandButtonItem("Сохранить турнир", PackIconKind.ContentSave,
                new AsyncRelayCommand(execute: async _ => await SaveDataAsync()));
            _openLogsQuickCommand = new CommandButtonItem("Открыть журнал", PackIconKind.FileDocumentOutline,
                new RelayCommand(param => OpenLatestLogFile(), param => true));
        }

        public override void InitData()
        {
            base.InitData();

            _scoreScreenView = Resolve<IPanelView>("ScoreScreen");
            _scoreScreenVm = Resolve<ScoreScreenViewModel>();

            _ = SetupAutoSaveAsync();
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
                        _saveQuickCommand,
                        _openLogsQuickCommand
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

        private async void OpenMonitor()
        {
            if (!_scoreScreenView.WasShown)
            {
                var monitor = await MonitorPicker.PickAsync();
                if (monitor == null) return;

                if (_scoreScreenView is PanelViewBase panel)
                {
                    panel.TargetMonitor = monitor;
                }
            }

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

        // Event-driven autosave: no timer. When autosave is enabled and the
        // tournament has no FileName yet (fresh session), prompt once so the
        // first match-complete or import event can save without a dialog
        // interrupting mid-match. The manual "Сохранить турнир" quick button
        // is always visible regardless of the flag — autosave only covers
        // match/import events, so other mutations (team/wrestler registration,
        // bracket generation, schedule edits) still rely on manual save.
        private void OpenLatestLogFile()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appName = Assembly.GetEntryAssembly()?.GetName().Name ?? "WrestlingAdmin";
                var logDirectory = Path.Combine(appDataPath, appName, "Logs");

                if (!Directory.Exists(logDirectory))
                {
                    ShowSnackMessage("Журнал пуст.");
                    return;
                }

                string latest = null;
                var latestWrite = DateTime.MinValue;
                foreach (var file in Directory.EnumerateFiles(logDirectory, "*.txt"))
                {
                    var write = File.GetLastWriteTime(file);
                    if (write > latestWrite)
                    {
                        latestWrite = write;
                        latest = file;
                    }
                }

                if (latest == null)
                {
                    ShowSnackMessage("Журнал пуст.");
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = latest,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowSnackMessage("Не удалось открыть журнал: " + ex.Message);
            }
        }

        private async Task SetupAutoSaveAsync()
        {
            if (DataContext.Tournament != null
                && IsAutosaveEnabled
                && string.IsNullOrEmpty(DataContext.Tournament.FileName))
            {
                await SaveDataAsync();
            }
        }

        #endregion
    }
}