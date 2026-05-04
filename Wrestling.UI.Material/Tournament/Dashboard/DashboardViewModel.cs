using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CsvHelper;
using MaterialDesignThemes.Wpf;
using MvvmDialogs.FrameworkDialogs.FolderBrowser;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Entities.Results;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Settings;
using Wrestling.UI.Material.Slider;
using Wrestling.UI.Material.Tournament.Print;
using Wrestling.UI.Material.Tournament.Print.PrintApplications;
using Wrestling.UI.Material.Tournament.Print.PrintBracket;
using Wrestling.UI.Material.Tournament.Print.PrintResults;
using Wrestling.UI.Material.Tournament.Print.PrintSchedule;
using Wrestling.UI.Material.Tournament.Progress.Brackets;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Wrestling.UI.Material.Tournament.Results.Achievements;
using Wrestling.UI.Material.Tournament.Results.PersonalResults;
using Wrestling.UI.Material.Tournament.Results.TeamResults;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Material.Tournament.Standing.Applications;
using Wrestling.UI.Material.Tournament.Standing.Carpets;
using Wrestling.UI.Material.Tournament.Standing.Details;
using Wrestling.UI.Material.Tournament.Standing.Draw;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Dashboard
{
    public class DashboardViewModel : TournamentViewModelBase
    {
        #region Fields

        private ICommand _openBracketsCommand;
        private ICommand _openCarpetScheduleCommand;
        private ICommand _openStandingCommand;
        private ICommand _openStandingDetailsCommand;
        private ICommand _openStandingApplicationsCommand;
        private ICommand _openStandingDrawCommand;
        private ICommand _openStandingScheduleCommand;
        private ICommand _openPersonalResultsCommand;
        private ICommand _openTeamResultsCommand;
        private ICommand _openAchievementsCommand;
        private ICommand _openSliderControlCommand;
        private ICommand _exportBracketsPdfCommand;
        private ICommand _exportApplicationsPdfCommand;
        private ICommand _printScheduleCommand;

        private IList<CommandButtonItem> _quickButtons;
        private IList<CommandButtonItem> _drawerItems;

        private readonly CommandButtonItem _saveQuickCommand;
        private readonly CommandButtonItem _openLogsQuickCommand;
        private readonly CommandButtonItem _exportResultsQuickCommand;
        private bool _isExportingPdfs;

        private PeerSyncStatusTracker _peerSyncTracker;

        #endregion

        public System.Collections.ObjectModel.ObservableCollection<PeerStatusViewModel> PeerStatuses
            => _peerSyncTracker?.Peers;


        public DashboardViewModel(IDiContainer container) : base(container)
        {
            _saveQuickCommand = new CommandButtonItem("Сохранить турнир", PackIconKind.ContentSave,
                new AsyncRelayCommand(execute: async _ => await SaveDataAsync()));
            _openLogsQuickCommand = new CommandButtonItem("Открыть журнал", PackIconKind.FileDocumentOutline,
                new RelayCommand(param => OpenLatestLogFile(), param => true));
            _exportResultsQuickCommand = new CommandButtonItem("Экспорт результатов в Excel", PackIconKind.DatabaseExport,
                new RelayCommand(param => ExportResults(), param => true));
        }

        public override void InitData()
        {
            base.InitData();

            // Wire the peer-sync read model lazily on first dashboard nav.
            // Singleton in DI; binding survives across re-navigations to Home.
            if (_peerSyncTracker == null)
            {
                _peerSyncTracker = Resolve<PeerSyncStatusTracker>();
                OnPropertyChanged(nameof(PeerStatuses));
            }

            _ = SetupAutoSaveAsync();
        }

        public override string PageTitle => "Вольная борьба - Администратор турниров версия 20260421";

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
                    new CommandButtonItem("Настройки", new RelayCommand(param => OpenSettings(), param => true)),
                    _exportResultsQuickCommand,
                    _openLogsQuickCommand,
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

        public ICommand OpenStandingDetailsCommand
            => _openStandingDetailsCommand ?? (_openStandingDetailsCommand =
                new RelayCommand(_ => OpenStandingPage<DetailsViewModel>(), _ => true));

        public ICommand OpenStandingApplicationsCommand
            => _openStandingApplicationsCommand ?? (_openStandingApplicationsCommand =
                new RelayCommand(_ => OpenStandingPage<ApplicationsViewModel>(), _ => true));

        public ICommand OpenStandingDrawCommand
            => _openStandingDrawCommand ?? (_openStandingDrawCommand =
                new RelayCommand(_ => OpenStandingPage<DrawViewModel>(), _ => true));

        public ICommand OpenStandingScheduleCommand
            => _openStandingScheduleCommand ?? (_openStandingScheduleCommand =
                new RelayCommand(_ => OpenStandingPage<CarpetsViewModel>(), _ => true));

        public ICommand OpenPersonalResultsCommand
            => _openPersonalResultsCommand ?? (_openPersonalResultsCommand =
                new RelayCommand(param => NavigateToView<PersonalResultsViewModel>(), param => true));

        public ICommand OpenTeamResultsCommand
            => _openTeamResultsCommand ?? (_openTeamResultsCommand =
                new RelayCommand(param => NavigateToView<TeamResultsViewModel>(), param => true));

        public ICommand OpenAchievementsCommand
            => _openAchievementsCommand ?? (_openAchievementsCommand =
                new RelayCommand(param => NavigateToView<AchievementsViewModel>(), param => true));

        public ICommand OpenBracketsCommand
            => _openBracketsCommand ?? (_openBracketsCommand =
                new RelayCommand(_ => OpenBracketsView(), _ => true));

        public ICommand OpenCarpetScheduleCommand
            => _openCarpetScheduleCommand ?? (_openCarpetScheduleCommand =
                new RelayCommand(_ => OpenCarpetSchedule(), _ => true));

        public ICommand ExportBracketsPdfCommand
            => _exportBracketsPdfCommand ?? (_exportBracketsPdfCommand =
                new AsyncRelayCommand(execute: _ => ExportAllBracketPdfsAsync(), canExecute: _ => !_isExportingPdfs));

        public ICommand ExportApplicationsPdfCommand
            => _exportApplicationsPdfCommand ?? (_exportApplicationsPdfCommand =
                new AsyncRelayCommand(execute: _ => ExportAllApplicationsPdfsAsync(), canExecute: _ => !_isExportingPdfs));

        public ICommand PrintScheduleCommand
            => _printScheduleCommand ?? (_printScheduleCommand =
                new AsyncRelayCommand(execute: _ => PrintScheduleAsync()));

        #endregion

        #region Private Methods


        private void OpenStanding()
        {
            NavigateToView<StandingViewModel>();
        }

        private void OpenStandingPage<TPage>() where TPage : class, IStandingPageViewModel
        {
            var standing = Resolve<INavigationService>().GetViewModel<StandingViewModel>();
            standing?.SetInitialPage<TPage>();
            NavigateToView<StandingViewModel>();
        }

        private void OpenSliderControl()
        {
            NavigateToView<SliderControlViewModel>();
        }

        private void OpenBracketsView()
        {
            DataContext.IsBracketView = true;
            NavigateToView<BracketsViewModel>();
        }

        private void OpenCarpetSchedule()
        {
            DataContext.IsBracketView = false;
            NavigateToView<ScheduleViewModel>();
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

        private async Task ExportAllBracketPdfsAsync()
        {
            var tournament = DataContext.Tournament;
            var groupsWithBrackets = tournament?.Groups?
                .Where(g => g?.Bracket != null).ToList() ?? new List<AgeWeightGroup>();
            if (groupsWithBrackets.Count == 0)
            {
                Dialog.ShowMessageBox(this,
                    "Нет групп со сгенерированными сетками. Сначала проведите жеребьёвку.",
                    "Экспорт пакета протоколов", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var tournamentDir = !string.IsNullOrWhiteSpace(tournament?.FileName)
                ? Path.GetDirectoryName(tournament.FileName)
                : null;
            var defaultPath = !string.IsNullOrWhiteSpace(tournamentDir) && Directory.Exists(tournamentDir)
                ? tournamentDir
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            var settings = new FolderBrowserDialogSettings
            {
                Description = "Выберите папку для сохранения пакета протоколов",
                ShowNewFolderButton = true,
                SelectedPath = defaultPath
            };

            if (Dialog.ShowFolderBrowserDialog(this, settings) != true) return;

            _isExportingPdfs = true;
            try
            {
                var jobs = BuildExportJobs(tournament, groupsWithBrackets);
                ShowSnackMessage($"Идет создание пакета протоколов: {jobs.Count} файлов...");

                var exporter = new BulkBracketPdfExporter();
                var result = await exporter.ExportAsync(jobs, settings.SelectedPath);

                var msg = $"Готово. Сохранено PDF: {result.Succeeded}";
                if (result.Skipped > 0) msg += $", пропущено: {result.Skipped}";
                if (result.Failures.Count > 0) msg += $", ошибок: {result.Failures.Count}";
                ShowSnackMessage(msg);

                if (result.Failures.Count > 0)
                {
                    Dialog.ShowMessageBox(this,
                        "Не удалось сохранить часть протоколов:\n\n" + string.Join("\n", result.Failures),
                        "Экспорт пакета протоколов", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Dialog.ShowMessageBox(this,
                    "Ошибка экспорта: " + ex.Message,
                    "Экспорт пакета протоколов", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isExportingPdfs = false;
            }
        }

        private List<BulkPdfExportJob> BuildExportJobs(
            Wrestling.Entities.Tournament tournament,
            List<AgeWeightGroup> groupsWithBrackets)
        {
            var jobs = new List<BulkPdfExportJob>();

            var (personalResults, olympicTeamResults) = ComputeTournamentResults(tournament);

            if (olympicTeamResults != null && olympicTeamResults.Count > 0)
            {
                jobs.Add(new BulkPdfExportJob
                {
                    FileName = "_Командный зачет (олимпийский).pdf",
                    Landscape = false,
                    ViewFactory = () =>
                    {
                        var vm = new PrintOlympicTeamResultsViewModel(DiContainer) { TeamResults = olympicTeamResults };
                        vm.InitData();
                        return new PrintOlympicTeamResultsView { DataContext = vm };
                    }
                });
            }

            if (personalResults != null && personalResults.Count > 0)
            {
                jobs.Add(new BulkPdfExportJob
                {
                    FileName = "_Личные результаты.pdf",
                    Landscape = false,
                    ViewFactory = () =>
                    {
                        var vm = new PrintPersonalResultsViewModel(DiContainer) { Results = personalResults };
                        vm.InitData();
                        return new PrintPersonalResultsView { DataContext = vm };
                    }
                });
            }

            foreach (var group in groupsWithBrackets)
            {
                var capturedGroup = group;
                var mainRounds = capturedGroup.Bracket.Rounds.Count(r => r.RoundType == GroupRoundTypeEnum.Main);
                jobs.Add(new BulkPdfExportJob
                {
                    FileName = BulkBracketPdfExporter.MakeSafeFileName(capturedGroup.Name) + ".pdf",
                    Landscape = mainRounds >= 5,
                    ViewFactory = () =>
                    {
                        DataContext.Group = capturedGroup;
                        var vm = new PrintBracketViewModel(DiContainer);
                        vm.InitData();
                        return new PrintBracketView { DataContext = vm };
                    }
                });
            }

            return jobs;
        }

        private async Task ExportAllApplicationsPdfsAsync()
        {
            var tournament = DataContext.Tournament;
            var groupsWithWrestlers = tournament?.Groups?
                .Where(g => g?.Wrestlers != null && g.Wrestlers.Count > 0).ToList() ?? new List<AgeWeightGroup>();
            if (groupsWithWrestlers.Count == 0)
            {
                Dialog.ShowMessageBox(this,
                    "Нет групп с зарегистрированными участниками.",
                    "Экспорт протоколов взвешивания", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var tournamentDir = !string.IsNullOrWhiteSpace(tournament?.FileName)
                ? Path.GetDirectoryName(tournament.FileName)
                : null;
            var defaultPath = !string.IsNullOrWhiteSpace(tournamentDir) && Directory.Exists(tournamentDir)
                ? tournamentDir
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            var settings = new FolderBrowserDialogSettings
            {
                Description = "Выберите папку для сохранения протоколов взвешивания",
                ShowNewFolderButton = true,
                SelectedPath = defaultPath
            };

            if (Dialog.ShowFolderBrowserDialog(this, settings) != true) return;

            _isExportingPdfs = true;
            try
            {
                var jobs = BuildApplicationsExportJobs(groupsWithWrestlers);
                ShowSnackMessage($"Идет создание протоколов взвешивания: {jobs.Count} файлов...");

                var exporter = new BulkBracketPdfExporter();
                var result = await exporter.ExportAsync(jobs, settings.SelectedPath);

                var msg = $"Готово. Сохранено PDF: {result.Succeeded}";
                if (result.Skipped > 0) msg += $", пропущено: {result.Skipped}";
                if (result.Failures.Count > 0) msg += $", ошибок: {result.Failures.Count}";
                ShowSnackMessage(msg);

                if (result.Failures.Count > 0)
                {
                    Dialog.ShowMessageBox(this,
                        "Не удалось сохранить часть протоколов:\n\n" + string.Join("\n", result.Failures),
                        "Экспорт протоколов взвешивания", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Dialog.ShowMessageBox(this,
                    "Ошибка экспорта: " + ex.Message,
                    "Экспорт протоколов взвешивания", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isExportingPdfs = false;
            }
        }

        private List<BulkPdfExportJob> BuildApplicationsExportJobs(List<AgeWeightGroup> groupsWithWrestlers)
        {
            var jobs = new List<BulkPdfExportJob>();

            foreach (var group in groupsWithWrestlers)
            {
                var capturedGroup = group;
                jobs.Add(new BulkPdfExportJob
                {
                    FileName = "Взвешивание_" + BulkBracketPdfExporter.MakeSafeFileName(capturedGroup.Name) + ".pdf",
                    Landscape = false,
                    ViewFactory = () =>
                    {
                        DataContext.Group = capturedGroup;
                        var vm = new PrintApplicationsViewModel(DiContainer);
                        vm.InitData();
                        return new PrintApplicationsView { DataContext = vm };
                    }
                });
            }

            return jobs;
        }

        private (List<TournamentResult> personal, List<TournamentTeamResult> olympicTeam) ComputeTournamentResults(
            Wrestling.Entities.Tournament tournament)
        {
            var processors = Resolve<List<IGroupBracketProcessor>>();
            var teamCalculator = Resolve<ITeamResultsCalculator>();
            var olympicOrderer = Resolve<ITeamResultsOrderer>("OlympicOrderer");

            var allResults = new List<TournamentResult>();
            foreach (var group in tournament.Groups)
            {
                if (group.Bracket == null) continue;

                var processor = processors.FirstOrDefault(p => p.Code == group.Bracket.BracketTypeCode);
                if (processor == null) continue;

                processor.Load(tournament, group);
                var groupResults = processor.GetResults();
                if (groupResults != null) allResults.AddRange(groupResults);
            }

            var ordered = allResults
                .OrderBy(x => x.Group.Name)
                .ThenBy(p => p.Wrestler.FinalPlace)
                .ToList();

            var teamResults = teamCalculator.GetTeamResults(ordered, null);
            var olympicTeam = olympicOrderer.GetOrderedResults(teamResults);

            return (ordered, olympicTeam);
        }

        private async Task SetupAutoSaveAsync()
        {
            // A freshly-created tournament has no FileName until the operator
            // picks a path. Prompt once when the dashboard opens so subsequent
            // event-driven autosaves have a target to write to.
            if (DataContext.Tournament != null
                && string.IsNullOrEmpty(DataContext.Tournament.FileName))
            {
                await SaveDataAsync();
            }
        }

        private void ExportResults()
        {
            var tournament = DataContext.Tournament;
            if (tournament == null)
            {
                ShowSnackMessage("Турнир не открыт.");
                return;
            }

            var resultsService = Resolve<IResultsService>();
            var results = resultsService?.AllResults;
            if (results == null || results.Count == 0)
            {
                ShowSnackMessage("Нет данных для экспорта.");
                return;
            }

            var settings = new SaveFileDialogSettings
            {
                Title = "Экспортировать результаты в файл",
                CheckFileExists = false,
                OverwritePrompt = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "CSV (*.csv)|*.csv|All Files (*.*)|*.*"
            };

            if (Dialog.ShowSaveFileDialog(this, settings) != true) return;

            try
            {
                using (var writer = new StreamWriter(settings.FileName))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    var exportData = results.Select(item =>
                    {
                        var team = tournament.TeamApplications.FirstOrDefault(x => x.ID == item.Wrestler.TeamID);
                        return new ExportedResult
                        {
                            FullName = item.Wrestler.FullName,
                            BirthDate = item.Wrestler.BirthDate.HasValue
                                ? item.Wrestler.BirthDate.Value.ToString("dd/MM/yyyy")
                                : string.Empty,
                            FinalPlace = item.Wrestler.FinalPlace,
                            GroupName = item.GroupName,
                            TeamCity = item.Wrestler.TeamCity,
                            TeamName = item.Wrestler.TeamName,
                            TeamCoach = team?.MainCoach,
                            WinsCount = item.Wins,
                            LoseCount = item.Loses,
                            PointsEarned = item.AllGainedPoints,
                            PointsLost = item.AllLostPoints,
                            WinsByTushe = item.WinsByTushe,
                            WinsByDomination = item.WinsByDomination,
                            WinsByPoints = item.WinsByPointsTotal,
                            LoseByTushe = item.LoseByTushe,
                            LoseByDomination = item.LoseByDomination,
                            LoseByPoints = item.LoseByPoints
                        };
                    }).OrderBy(x => x.GroupName).ThenBy(x => x.FinalPlace);

                    csv.WriteRecords(exportData);
                }

                ShowSnackMessage("Результаты турнира экспортированы!");
            }
            catch (Exception ex)
            {
                ShowSnackMessage($"Произошла ошибка экспорта: {ex.Message}");
            }
        }

        private async Task PrintScheduleAsync()
        {
            var tournament = DataContext.Tournament;
            var carpets = tournament?.Carpets;
            if (carpets == null || carpets.Count == 0)
            {
                ShowSnackMessage("Нет ковров для печати.");
                return;
            }

            var carpet = await CarpetPicker.PickAsync(carpets);
            if (carpet == null) return;

            var hasPending = tournament.Groups
                .Where(g => g.Bracket != null && g.CarpetID == carpet.ID)
                .SelectMany(g => g.Bracket.Rounds)
                .SelectMany(r => r.RoundMatches)
                .Any(rm => !rm.IsMatchCompleted);

            if (!hasPending)
            {
                ShowSnackMessage($"На ковре «{carpet.Name}» нет непройденных схваток.");
                return;
            }

            var vm = new PrintScheduleViewModel(DiContainer, carpet);
            vm.InitData();
            var view = new PrintScheduleView { DataContext = vm };

            var dlg = new PrintDialog();
            if (dlg.ShowDialog() != true) return;

            if (!VisualPrinter.PrintAcrossPages(dlg, view, "Печать"))
            {
                Dialog.ShowMessageBox(this,
                    "Ошибка печати. Попробуйте еще раз.",
                    "Печать расписания", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }

    public class ExportedResult
    {
        public string GroupName { get; set; }
        public string FullName { get; set; }
        public string TeamName { get; set; }
        public string TeamCity { get; set; }
        public string TeamCoach { get; set; }
        public string BirthDate { get; set; }
        public int? FinalPlace { get; set; }
        public int PointsEarned { get; set; }
        public int PointsLost { get; set; }
        public int WinsCount { get; set; }
        public int LoseCount { get; set; }
        public int WinsByTushe { get; set; }
        public int WinsByDomination { get; set; }
        public int WinsByPoints { get; set; }
        public int LoseByTushe { get; set; }
        public int LoseByDomination { get; set; }
        public int LoseByPoints { get; set; }
    }
}