using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using MvvmDialogs.FrameworkDialogs.FolderBrowser;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Entities.Bracket.Seeding;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Print;
using Wrestling.UI.Material.Tournament.Print.PrintBracket;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Draw
{
    public class DrawViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        #region Fields

        private IMatchNumbersGenerator _matchNumbersGenerator;
        private ISeedingStrategy _seedingStrategy;

        private ICommand _generateBracketCommand;
        private ICommand _regenerateAllBrackets;
        private ICommand _unfixAllSeedsCommand;

        private List<IGroupBracketProcessor> _drawTypes;
        private ObservableCollection<AgeWeightGroup> _groups;

        private IList<CommandButtonItem> _quickButtons;

        private bool IsTeamTournament => true;

        #endregion

        public DrawViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            if (DataContext.Tournament == null)
            {
                throw new ApplicationException("Tournament property is not set!");
            }

            _matchNumbersGenerator = Resolve<IMatchNumbersGenerator>();

            _drawTypes = Resolve<List<IGroupBracketProcessor>>();
            _seedingStrategy = Resolve<ISeedingStrategy>();

            var groups = DataContext.Tournament.Groups.OrderBy(g => g.IsFemale).ThenByDescending(g => g.BirthYearMin).ThenBy(g => g.WeightMax).ToList();
            foreach (var group in groups)
            {
                SeedWrestlers(group);
            }

            Groups = new ObservableCollection<AgeWeightGroup>(groups);

            // Check groups
            foreach (var wrestler in DataContext.Tournament.Wrestlers)
            {
                var group = Groups.FirstOrDefault(gr => gr.ID == wrestler.GroupID);
                if (group != null)
                {                    
                    if (group.Wrestlers.FirstOrDefault(wr => wr.ID == wrestler.ID) == null)
                    {
                        group.Wrestlers.Add(wrestler);
                    }
                }
                else
                {
                    wrestler.GroupID = null;
                    wrestler.GroupName = string.Empty;
                }
            }
        }

        #region Binding Properties

        public int GroupsCount => DataContext.Tournament.GroupsCount;
        public int WrestlersCount => Groups?.SelectMany(gr => gr.Wrestlers).Count() ?? 0;

        // Counts only "real" matches operators must run on the carpet —
        // bye/walkover slots that the bracket processor auto-completes with
        // WinType=FreeWin during generation are excluded.
        public int MatchesCount => DataContext.Tournament.Groups
            .Where(g => g.Bracket != null)
            .SelectMany(g => g.Bracket.Rounds)
            .SelectMany(r => r.RoundMatches)
            .Count(m => m.WinType != MatchWinTypeEnum.FreeWin);

        public string PageName => "Жеребьевка";
        public override string PageTitle => "Жеребьевка Участников";

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                if (_quickButtons == null)
                {
                    CommandButtonItem printBtn = null;
                    var printCmd = new AsyncRelayCommand(
                        execute: async _ =>
                        {
                            printBtn.IsBusy = true;
                            try { await ExportDrawProtocolsAsync(); }
                            finally { printBtn.IsBusy = false; }
                        },
                        canExecute: _ => true);
                    printBtn = new CommandButtonItem(
                        "Сохранить протоколы жеребьевки",
                        PackIconKind.PrinterOutline,
                        printCmd);

                    _quickButtons = new List<CommandButtonItem> { printBtn };
                }
                return _quickButtons;
            }
        }
        
        public ObservableCollection<AgeWeightGroup> Groups
        {
            get => _groups;
            set
            {
                _groups = value;

                OnPropertyChanged("Groups");
            }
        }

        #endregion

        #region Command Properties

        public ICommand RegenerateAllBrackets
        {
            get
            {
                if (_regenerateAllBrackets == null)
                {
                    _regenerateAllBrackets = new RelayCommand(param => RegenerateBrackets(), param => param != null);
                }
                return _regenerateAllBrackets;
            }
        }

        public ICommand UnfixAllSeedsCommand
        {
            get
            {
                if (_unfixAllSeedsCommand == null)
                {
                    _unfixAllSeedsCommand = new RelayCommand(param => UnfixAllSeeds(), param => true);
                }
                return _unfixAllSeedsCommand;
            }
        }
        
        public ICommand GenerateBracketCommand
        {
            get
            {
                if (_generateBracketCommand == null)
                {
                    _generateBracketCommand = new RelayCommand(param => GenerateBracket(param as AgeWeightGroup), param => param != null);
                }
                return _generateBracketCommand;
            }
        }

        #endregion

        #region Private Methods

        private void RegenerateBrackets()
        {
            if (Dialog.ShowMessageBox(this, "Вы уверены, что хотите перегенерировать все сетки! Это приведет к потере текущих результатов турнира!", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

            foreach (var ageWeightGroup in Groups)
            {
                SeedWrestlers(ageWeightGroup);

                var drawType = GetDrawTypeForGroup(ageWeightGroup);

                if (drawType == null)
                {
                    continue;
                }
                
                drawType.Generate(DataContext.Tournament, ageWeightGroup);
                
                foreach (var wr in ageWeightGroup.Wrestlers)
                {
                    wr.FinalPlace = null;
                    wr.IsSeedFixed = true;
                }
                
                if (ageWeightGroup.Bracket != null)
                {
                    if (DataContext.Tournament.Carpets.FirstOrDefault(c => c.Groups.Contains(ageWeightGroup)) != null)
                    {
                        _matchNumbersGenerator.Generate(DataContext.Tournament, _drawTypes);
                    }

                    // We need to refresh Rounds collection to redraw it on UI
                    ageWeightGroup.Bracket.Rounds = new List<GroupRound>(ageWeightGroup.Bracket.Rounds);

                    OnPropertyChanged("MatchesCount");
                }
            }
        }

        private void UnfixAllSeeds()
        {
            if (Dialog.ShowMessageBox(this, "Снять отметку «Фикс.» у всех участников во всех группах?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

            foreach (var wrestler in DataContext.Tournament.Wrestlers)
            {
                wrestler.IsSeedFixed = false;
            }
        }

        private IGroupBracketProcessor GetDrawTypeForGroup(AgeWeightGroup group)
        {
            IGroupBracketProcessor drawType;
                
            if (group.Wrestlers.Count <= 5)
            {
                drawType = _drawTypes.First(x => x.Code == BracketTypeEnum.RoundRobin.ToString());
            } 
            else if (group.Wrestlers.Count > 5 && group.Wrestlers.Count < 8)
            {
                drawType = _drawTypes.First(x => x.Code == BracketTypeEnum.SubGroupsIntoOlympic.ToString());
            }
            else
            {
                drawType = _drawTypes.First(x => x.Code == BracketTypeEnum.OlympicConsilationFinalists.ToString());
            }

            return drawType;
        }

        private async void GenerateBracket(AgeWeightGroup group)
        {
            if (group == null) return;

            var vm = new AddBracketViewModel(DiContainer, group);
            vm.InitData();

            var view = new AddBracketDialog
            {
                DataContext = vm
            };

            var result = await DialogHost.Show(view, "RootDialog");
            if (result == null || !(bool)result) return;

            var drawType = _drawTypes.FirstOrDefault(d => d.Title == vm.SelectedDrawType.Title);
            if (drawType == null) throw new ApplicationException("Wrong Bracket type!");

            SeedWrestlers(group);

            drawType.Generate(DataContext.Tournament, group);

            foreach (var wr in group.Wrestlers)
            {
                wr.FinalPlace = null;
                wr.IsSeedFixed = true;
            }

            if (group.Bracket != null)
            {
                if (DataContext.Tournament.Carpets.FirstOrDefault(c => c.Groups.Contains(group)) != null)
                {
                    _matchNumbersGenerator.Generate(DataContext.Tournament, _drawTypes);
                }

                group.Bracket.Rounds = new List<GroupRound>(group.Bracket.Rounds);

                OnPropertyChanged("MatchesCount");
            }
        }
        
        // Delegates to the injected ISeedingStrategy (see App.xaml.cs). The
        // strategy is responsible for honoring IsSeedFixed locks, rewriting
        // SeedNumber to a contiguous 1..N range, and sorting group.Wrestlers
        // by the new SeedNumber.
        private void SeedWrestlers(AgeWeightGroup group)
        {
            // InitData is the first call site — _seedingStrategy may not be
            // resolved yet when unit tests bypass InitData. Guard defensively.
            if (_seedingStrategy == null)
            {
                _seedingStrategy = Resolve<ISeedingStrategy>();
            }
            _seedingStrategy.Seed(group);
        }

        // Bulk-PDF export of the draw protocol — one bracket PDF per group
        // that has a bracket. Same renderer as the «Скачать сетки и итоги»
        // pipeline; before any matches are played, PrintBracketView shows
        // the seeded participants without scores, which is exactly what the
        // draw protocol needs.
        private async Task ExportDrawProtocolsAsync()
        {
            var tournament = DataContext.Tournament;
            var groupsWithBrackets = tournament?.Groups?
                .Where(g => g?.Bracket != null).ToList() ?? new List<AgeWeightGroup>();
            if (groupsWithBrackets.Count == 0)
            {
                Dialog.ShowMessageBox(this,
                    "Нет групп со сгенерированными сетками. Сначала проведите жеребьёвку.",
                    "Экспорт протоколов жеребьёвки", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var tournamentDir = !string.IsNullOrWhiteSpace(tournament.FileName)
                ? Path.GetDirectoryName(tournament.FileName)
                : null;
            var defaultPath = !string.IsNullOrWhiteSpace(tournamentDir) && Directory.Exists(tournamentDir)
                ? tournamentDir
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            var settings = new FolderBrowserDialogSettings
            {
                Description = "Выберите папку для сохранения протоколов жеребьёвки",
                ShowNewFolderButton = true,
                SelectedPath = defaultPath
            };

            if (Dialog.ShowFolderBrowserDialog(this, settings) != true) return;

            try
            {
                var jobs = new List<BulkPdfExportJob>();
                foreach (var group in groupsWithBrackets)
                {
                    var capturedGroup = group;
                    var mainRounds = capturedGroup.Bracket.Rounds.Count(r => r.RoundType == GroupRoundTypeEnum.Main);
                    jobs.Add(new BulkPdfExportJob
                    {
                        FileName = "Жеребьевка_" + BulkBracketPdfExporter.MakeSafeFileName(capturedGroup.Name) + ".pdf",
                        Landscape = mainRounds >= 5,
                        ViewFactory = () =>
                        {
                            DataContext.Group = capturedGroup;
                            var vm = new PrintBracketViewModel(DiContainer) { IsDrawProtocol = true };
                            vm.InitData();
                            return new PrintBracketView { DataContext = vm };
                        }
                    });
                }

                ShowSnackMessage($"Идет создание протоколов жеребьёвки: {jobs.Count} файлов...");

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
                        "Экспорт протоколов жеребьёвки", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Dialog.ShowMessageBox(this,
                    "Ошибка экспорта: " + ex.Message,
                    "Экспорт протоколов жеребьёвки", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}