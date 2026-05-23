using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CsvHelper;
using MaterialDesignThemes.Wpf;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.UI.Material.Utils;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Entities.Results;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Print;
using Wrestling.UI.Material.Tournament.Print.PrintBracket;
using Wrestling.UI.Material.Tournament.Print.PrintResults;
using Wrestling.UI.Material.Tournament.Results.Achievements;
using Wrestling.UI.Material.Tournament.Results.PersonalResults;
using Wrestling.UI.Material.Tournament.Results.TeamResults;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Tournament.Results
{
    // «Результаты» wrapper. Hosts three results sub-tabs:
    // Личные / Командные / Достижения.
    public class ResultsViewModel : TournamentViewModelBase
    {
        private IStandingPageViewModel _currentPage;
        private List<IStandingPageViewModel> _pageViewModels;
        private IList<CommandButtonItem> _quickButtons;

        // One-shot deep-link target — set by external callers before
        // NavigateToView<ResultsViewModel>(). Consumed and cleared in InitData
        // so the next plain navigation falls back to the first page.
        private IStandingPageViewModel _pendingInitialPage;

        public ResultsViewModel(IDiContainer container) : base(container)
        {
        }

        public override bool IsBackButtonAvailable => false;

        // T inherited from TournamentViewModelBase.
        public override string PageTitle => CurrentPage?.PageTitle ?? T("Results_PageTitle", "Результаты");

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                if (_quickButtons == null)
                {
                    CommandButtonItem exportBtn = null;
                    var exportCmd = new AsyncRelayCommand(
                        execute: async _ =>
                        {
                            exportBtn.IsBusy = true;
                            try { await ExportAllBracketPdfsAsync(); }
                            finally { exportBtn.IsBusy = false; }
                        },
                        canExecute: _ => true);
                    exportBtn = new CommandButtonItem(
                        T("Results_ExportPdf", "Скачать сетки и итоги PDF"),
                        PackIconKind.PrinterOutline,
                        exportCmd);

                    CommandButtonItem resultsCsvBtn = null;
                    var resultsCsvCmd = new RelayCommand(
                        execute: _ =>
                        {
                            resultsCsvBtn.IsBusy = true;
                            try { ExportResultsCsv(); }
                            finally { resultsCsvBtn.IsBusy = false; }
                        },
                        canExecute: _ => true);
                    resultsCsvBtn = new CommandButtonItem(
                        T("Results_ExportCsv", "Экспорт результатов в CSV"),
                        PackIconKind.DatabaseExport,
                        resultsCsvCmd);

                    _quickButtons = new List<CommandButtonItem> { exportBtn, resultsCsvBtn };
                }
                return _quickButtons;
            }
        }

        public IStandingPageViewModel CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage == value) return;
                _currentPage = value;
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(PageTitle));
                _currentPage?.InitData();
            }
        }

        public List<IStandingPageViewModel> PageViewModels
        {
            get => _pageViewModels;
            set
            {
                _pageViewModels = value;
                OnPropertyChanged(nameof(PageViewModels));
            }
        }

        public override void InitData()
        {
            base.InitData();

            _quickButtons = null;

            InitPages();
            SetupCurrentPage();
        }

        public void SetInitialPage<TPage>() where TPage : class, IStandingPageViewModel
        {
            if (_pageViewModels == null) InitPages();
            _pendingInitialPage = _pageViewModels.OfType<TPage>().FirstOrDefault();
        }

        private void InitPages()
        {
            if (PageViewModels != null) return;

            var nav = Resolve<INavigationService>();

            PageViewModels = new List<IStandingPageViewModel>
            {
                nav?.GetViewModel<PersonalResultsViewModel>(),
                nav?.GetViewModel<TeamResultsViewModel>(),
                nav?.GetViewModel<AchievementsViewModel>()
            }.Where(vm => vm != null).ToList();
        }

        private void SetupCurrentPage()
        {
            // Preserve CurrentPage across re-entries — when returning from a
            // full-screen MatchResults overlay we want to land on the same
            // sub-tab they left from rather than snap back to Личные.
            var target = _pendingInitialPage ?? CurrentPage ?? PageViewModels.FirstOrDefault();
            _pendingInitialPage = null;
            CurrentPage = target;
        }

        private async Task ExportAllBracketPdfsAsync()
        {
            var tournament = DataContext.Tournament;
            var groupsWithBrackets = tournament?.Groups?
                .Where(g => g?.Bracket != null).ToList() ?? new List<AgeWeightGroup>();
            if (groupsWithBrackets.Count == 0)
            {
                Dialog.ShowMessageBox(this,
                    T("Export_NoBrackets_Body", "Нет групп со сгенерированными сетками. Сначала проведите жеребьёвку."),
                    T("Export_DialogTitle", "Экспорт пакета протоколов"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var defaultPath = ResolveDefaultExportFolder(tournament);

            var selectedFolder = FolderPicker.PickFolder(
                T("Export_FolderPicker_Title", "Выберите папку для сохранения пакета протоколов"),
                defaultPath);
            if (string.IsNullOrEmpty(selectedFolder)) return;

            try
            {
                var jobs = BuildExportJobs(tournament, groupsWithBrackets);
                ShowSnackMessage(string.Format(T("Export_Snack_Building", "Идет создание пакета протоколов: {0} файлов..."), jobs.Count));

                var exporter = new BulkBracketPdfExporter();
                var result = await exporter.ExportAsync(jobs, selectedFolder);

                var msg = string.Format(T("Export_Snack_Done", "Готово. Сохранено PDF: {0}"), result.Succeeded);
                if (result.Skipped > 0) msg += string.Format(T("Export_Snack_Skipped", ", пропущено: {0}"), result.Skipped);
                if (result.Failures.Count > 0) msg += string.Format(T("Export_Snack_Failed", ", ошибок: {0}"), result.Failures.Count);
                ShowSnackMessage(msg);

                if (result.Failures.Count > 0)
                {
                    Dialog.ShowMessageBox(this,
                        T("Export_PartialFailure", "Не удалось сохранить часть протоколов:") + "\n\n" + string.Join("\n", result.Failures),
                        T("Export_DialogTitle", "Экспорт пакета протоколов"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Dialog.ShowMessageBox(this,
                    T("Export_ErrorPrefix", "Ошибка экспорта: ") + ex.Message,
                    T("Export_DialogTitle", "Экспорт пакета протоколов"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string ResolveDefaultExportFolder(Wrestling.Entities.Tournament tournament)
        {
            var tournamentDir = !string.IsNullOrWhiteSpace(tournament?.FileName)
                ? Path.GetDirectoryName(tournament.FileName)
                : null;
            return !string.IsNullOrWhiteSpace(tournamentDir) && Directory.Exists(tournamentDir)
                ? tournamentDir
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private List<BulkPdfExportJob> BuildExportJobs(
            Wrestling.Entities.Tournament tournament,
            List<AgeWeightGroup> groupsWithBrackets)
        {
            var jobs = new List<BulkPdfExportJob>();

            // Read straight from the cached ResultsService — Recalculate fires
            // on tournament open, match approve, and peer-sync merge, so the
            // cache is current whenever the user can click Export.
            var resultsService = Resolve<IResultsService>();
            var olympicOrderer = Resolve<ITeamResultsOrderer>("OlympicOrderer");
            var pointsOrderer = Resolve<ITeamResultsOrderer>("PointsOrderer");
            var personalResults = resultsService.AllResults.ToList();
            var olympicTeamResults = resultsService.GetOrderedTeamResults(olympicOrderer).ToList();
            var pointsTeamResults = pointsOrderer == null
                ? new List<TournamentTeamResult>()
                : resultsService.GetOrderedTeamResults(pointsOrderer).ToList();

            if (olympicTeamResults != null && olympicTeamResults.Count > 0)
            {
                jobs.Add(new BulkPdfExportJob
                {
                    FileName = T("Export_FileName_TeamResults", "_Командный зачет (олимпийский).pdf"),
                    Landscape = false,
                    ViewFactory = () =>
                    {
                        var vm = new PrintOlympicTeamResultsViewModel(DiContainer) { TeamResults = olympicTeamResults };
                        vm.InitData();
                        return new PrintOlympicTeamResultsView { DataContext = vm };
                    }
                });
            }

            if (pointsTeamResults != null && pointsTeamResults.Count > 0)
            {
                jobs.Add(new BulkPdfExportJob
                {
                    FileName = T("Export_FileName_TeamResultsPoints", "_Командный зачет (квалификационные баллы).pdf"),
                    Landscape = false,
                    ViewFactory = () =>
                    {
                        var vm = new PrintPointsTeamResultsViewModel(DiContainer) { TeamResults = pointsTeamResults };
                        vm.InitData();
                        return new PrintPointsTeamResultsView { DataContext = vm };
                    }
                });
            }

            if (personalResults != null && personalResults.Count > 0)
            {
                jobs.Add(new BulkPdfExportJob
                {
                    FileName = T("Export_FileName_PersonalResults", "_Личные результаты.pdf"),
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
                var mainRounds = capturedGroup.Bracket.MainRounds().Count();
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

        private void ExportResultsCsv()
        {
            var tournament = DataContext.Tournament;
            if (tournament == null)
            {
                ShowSnackMessage(T("Snack_TournamentNotOpen", "Турнир не открыт."));
                return;
            }

            var resultsService = Resolve<IResultsService>();
            var results = resultsService?.AllResults;
            if (results == null || results.Count == 0)
            {
                ShowSnackMessage(T("Snack_NoExportData", "Нет данных для экспорта."));
                return;
            }

            var settings = new SaveFileDialogSettings
            {
                Title = T("Export_Csv_DialogTitle", "Экспортировать результаты в файл"),
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

                ShowSnackMessage(T("Snack_ResultsExported", "Результаты турнира экспортированы!"));
            }
            catch (Exception ex)
            {
                ShowSnackMessage(string.Format(T("Snack_ExportError", "Произошла ошибка экспорта: {0}"), ex.Message));
            }
        }

        // CsvHelper writes by reflection on this POCO's properties.
        private sealed class ExportedResult
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
}
