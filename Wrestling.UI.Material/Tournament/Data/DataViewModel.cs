using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CsvHelper;
using MaterialDesignThemes.Wpf;
using MvvmDialogs.FrameworkDialogs.FolderBrowser;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Entities.Results;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Print;
using Wrestling.UI.Material.Tournament.Print.PrintBracket;
using Wrestling.UI.Material.Tournament.Print.PrintResults;
using Wrestling.UI.Material.Tournament.Print.PrintSchedule;
using Wrestling.UI.Material.Utils;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Data
{
    // «Данные» — single home for bulk dumps. Step 7 lifts the export
    // pipeline 1:1 from DashboardViewModel; Dashboard keeps its own copy
    // until step 9 deletes the file. Logic intentionally duplicated for
    // the duration of the migration so the legacy Hub stays functional.
    public class DataViewModel : TournamentViewModelBase
    {
        private IList<BulkExportCardViewModel> _cards;
        private bool _isExporting;

        public DataViewModel(IDiContainer container) : base(container)
        {
        }

        public override string PageTitle => "Данные";

        public override bool IsBackButtonAvailable => false;

        public IList<BulkExportCardViewModel> Cards => _cards;

        public override void InitData()
        {
            base.InitData();

            if (_cards != null) return;

            _cards = new List<BulkExportCardViewModel>
            {
                MakeBulkBracketsCard(),
                MakeScheduleCard(),
                MakeWrestlersCsvCard(),
                MakeCsvExportCard()
            };

            OnPropertyChanged(nameof(Cards));
        }

        // === Cards ===

        private BulkExportCardViewModel MakeBulkBracketsCard()
        {
            BulkExportCardViewModel card = null;
            card = new BulkExportCardViewModel(
                "Скачать сетки и итоги PDF",
                "Турнирные сетки всех групп + командный и личный зачёты, по одному PDF на категорию.",
                PackIconKind.FilePdfBox,
                new AsyncRelayCommand(
                    execute: async _ => { card.IsBusy = true; try { await ExportAllBracketPdfsAsync(); } finally { card.IsBusy = false; } },
                    canExecute: _ => !_isExporting));
            return card;
        }

        private BulkExportCardViewModel MakeScheduleCard()
        {
            BulkExportCardViewModel card = null;
            card = new BulkExportCardViewModel(
                "Печать расписания ковра",
                "Расписание оставшихся схваток для выбранного ковра.",
                PackIconKind.ClipboardTextClock,
                new AsyncRelayCommand(
                    execute: async _ => { card.IsBusy = true; try { await PrintScheduleAsync(); } finally { card.IsBusy = false; } },
                    canExecute: _ => !_isExporting));
            return card;
        }

        private BulkExportCardViewModel MakeCsvExportCard()
        {
            BulkExportCardViewModel card = null;
            card = new BulkExportCardViewModel(
                "Экспорт результатов в CSV",
                "Список спортсменов с занятыми местами и статистикой схваток.",
                PackIconKind.FileDelimited,
                new RelayCommand(
                    execute: _ => { card.IsBusy = true; try { ExportResults(); } finally { card.IsBusy = false; } },
                    canExecute: _ => !_isExporting));
            return card;
        }

        private BulkExportCardViewModel MakeWrestlersCsvCard()
        {
            BulkExportCardViewModel card = null;
            card = new BulkExportCardViewModel(
                "Экспортировать список участников",
                "Список участников турнира с группами и командами в CSV/Excel.",
                PackIconKind.DatabaseExport,
                new RelayCommand(
                    execute: _ => { card.IsBusy = true; try { ExportWrestlers(); } finally { card.IsBusy = false; } },
                    canExecute: _ => !_isExporting));
            return card;
        }

        // === Export pipelines (lifted from DashboardViewModel) ===

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

            var defaultPath = ResolveDefaultExportFolder(tournament);

            var settings = new FolderBrowserDialogSettings
            {
                Description = "Выберите папку для сохранения пакета протоколов",
                ShowNewFolderButton = true,
                SelectedPath = defaultPath
            };

            if (Dialog.ShowFolderBrowserDialog(this, settings) != true) return;

            _isExporting = true;
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
                _isExporting = false;
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

            var dlg = new System.Windows.Controls.PrintDialog();
            if (dlg.ShowDialog() != true) return;

            if (!VisualPrinter.PrintAcrossPages(dlg, view, "Печать"))
            {
                Dialog.ShowMessageBox(this,
                    "Ошибка печати. Попробуйте еще раз.",
                    "Печать расписания", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void ExportWrestlers()
        {
            var tournament = DataContext.Tournament;
            if (tournament == null)
            {
                ShowSnackMessage("Турнир не открыт.");
                return;
            }

            var settings = new SaveFileDialogSettings
            {
                Title = "Экспортировать участников в файл",
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
                    var exportData = tournament.Wrestlers.Select(item => new ExportedWrestler
                    {
                        FullName = item.FullName,
                        BirthDate = item.BirthDate.HasValue ? item.BirthDate.Value.ToString("dd/MM/yyyy") : string.Empty,
                        GroupName = item.GroupName,
                        TeamCity = item.TeamCity,
                        TeamName = item.TeamName
                    }).OrderBy(x => x.GroupName).ThenBy(x => x.FullName);

                    csv.WriteRecords(exportData);
                }

                ShowSnackMessage("Список участников экспортирован!");
            }
            catch (Exception ex)
            {
                ShowSnackMessage($"Произошла ошибка экспорта: {ex.Message}");
            }
        }

        // === Helpers ===

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
    }

    // CsvHelper writes by reflection on this POCO's properties.
    public class ExportedWrestler
    {
        public string GroupName { get; set; }
        public string FullName { get; set; }
        public string TeamName { get; set; }
        public string TeamCity { get; set; }
        public string BirthDate { get; set; }
    }
}
