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
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Print;
using Wrestling.UI.Material.Tournament.Print.PrintSchedule;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Carpets
{
    public class CarpetsViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        private IMatchNumbersGenerator _matchNumbersGenerator;

        private ObservableCollection<AgeWeightGroup> _groups;
        private ObservableCollection<Carpet> _items;

        private ICommand _addCarpetCommand;
        private ICommand _deleteCarpetCommand;
        private ICommand _editCarpetCommand;
        private ICommand _bindGroupCommand;
        private ICommand _unbindGroupCommand;
        private ICommand _upGroupCommand;
        private ICommand _downGroupCommand;

        private IList<CommandButtonItem> _quickButtons;

        public string PageName => "Расписание";
        public override string PageTitle => "Очередность схваток по коврам и группам";
        public int UnbindedGroups => _groups != null && _items != null ? _groups.Count - _items.SelectMany(c => c.Groups).Count() : 0;

        public CarpetsViewModel(IDiContainer container) : base(container)
        {

        }

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
                            try { await ExportSchedulesAsync(); }
                            finally { printBtn.IsBusy = false; }
                        },
                        canExecute: _ => true);
                    printBtn = new CommandButtonItem(
                        "Скачать расписания ковров PDF",
                        PackIconKind.PrinterOutline,
                        printCmd);

                    _quickButtons = new List<CommandButtonItem> { printBtn };
                }
                return _quickButtons;
            }
        }

        public override void InitData()
        {
            base.InitData();

            _quickButtons = null;
            _matchNumbersGenerator = Resolve<IMatchNumbersGenerator>();

            Items = DataContext.Tournament.Carpets;

            _groups = DataContext.Tournament.Groups;

            VerifyCarpets();
        }

        private void VerifyCarpets()
        {
            var invalidCarpetGroups = _groups.Where(g => g.CarpetID.HasValue && Items.FirstOrDefault(c => c.ID == g.CarpetID.Value) == null).ToList();
            foreach (var invalidGroup in invalidCarpetGroups)
            {
                invalidGroup.CarpetID = null;
                invalidGroup.CarpetLabel = string.Empty;
            }
        }

        public ICommand UpGroupCommand
        {
            get
            {
                if (_upGroupCommand == null)
                {
                    _upGroupCommand = new RelayCommand(param => UpGroup(param as AgeWeightGroup), param => param != null);
                }
                return _upGroupCommand;
            }
        }

        public ICommand DownGroupCommand
        {
            get
            {
                if (_downGroupCommand == null)
                {
                    _downGroupCommand = new RelayCommand(param => DownGroup(param as AgeWeightGroup), param => param != null);
                }
                return _downGroupCommand;
            }
        }

        public ICommand AddCarpetCommand
        {
            get
            {
                if (_addCarpetCommand == null)
                {
                    _addCarpetCommand = new RelayCommand(param => AddCarpet(), param => true);
                }
                return _addCarpetCommand;
            }
        }

        public ICommand EditCarpetCommand
        {
            get
            {
                if (_editCarpetCommand == null)
                {
                    _editCarpetCommand = new RelayCommand(param => EditCarpet(param as Carpet), param => param != null);
                }
                return _editCarpetCommand;
            }
        }

        public ICommand DeleteCarpetCommand
        {
            get
            {
                if (_deleteCarpetCommand == null)
                {
                    _deleteCarpetCommand = new RelayCommand(param => DeleteCarpet(param as Carpet), param => param != null);
                }
                return _deleteCarpetCommand;
            }
        }

        public ICommand BindGroupCommand
        {
            get
            {
                if (_bindGroupCommand == null)
                {
                    _bindGroupCommand = new RelayCommand(param => BindGroup(param as Carpet), param => param != null);
                }
                return _bindGroupCommand;
            }
        }

        public ICommand UnbindGroupCommand
        {
            get
            {
                if (_unbindGroupCommand == null)
                {
                    _unbindGroupCommand = new RelayCommand(param => UnbindGroup(param as AgeWeightGroup), param => param != null);
                }
                return _unbindGroupCommand;
            }
        }

        public ObservableCollection<Carpet> Items
        {
            get { return _items; }
            set
            {
                _items = value;

                OnPropertyChanged("Items");
            }
        }

        private void UpGroup(AgeWeightGroup group)
        {
            var carpet = Items.FirstOrDefault(c => c.ID == group.CarpetID);
            if (carpet != null)
            {
                var i = carpet.Groups.IndexOf(group);
                var j = i - 1;
                if (j >= 0)
                {
                    carpet.Groups.Swap(i, j);
                }
            }
        }

        private void DownGroup(AgeWeightGroup group)
        {
            var carpet = Items.FirstOrDefault(c => c.ID == group.CarpetID);
            if (carpet != null)
            {
                var i = carpet.Groups.IndexOf(group);
                var j = i + 1;
                if (j < carpet.Groups.Count)
                {
                    carpet.Groups.Swap(i, j);
                }
            }
        }

        private async void AddCarpet()
        {
            var tmpCarpet = new Carpet
            {
                ID = Guid.NewGuid()
            };

            var view = new CarpetDialog
            {
                DataContext = tmpCarpet
            };

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool) result)
            {
                Items.Add(tmpCarpet);                
            }
        }

        private async void EditCarpet(Carpet carpet)
        {
            var tmpCarpet = carpet.Clone() as Carpet;

            var view = new CarpetDialog
            {
                DataContext = tmpCarpet
            };

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                carpet.Sync(tmpCarpet);
            }
        }

        private void DeleteCarpet(Carpet carpet)
        {
            if (Dialog.ShowMessageBox(this, "Вы уверены, что хотите удалить ковер?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.None) != MessageBoxResult.OK) return;

            var groups = _groups.Where(g => g.CarpetID.HasValue && g.CarpetID.Value == carpet.ID.Value).ToList();

            foreach (var group in groups)
            {
                group.CarpetID = null;
                group.CarpetLabel = string.Empty;
                group.FieldsVersion++;
            }

            Items.Remove(carpet);
        }

        private async void BindGroup(Carpet carpet)
        {
            var vm = new BindGroupViewModel(DiContainer);
            vm.InitData();

            var view = new BindGroupDialog
            {
                DataContext = vm
            };

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                if (vm.SelectedGroup != null)
                {
                    vm.SelectedGroup.CarpetLabel = carpet.Name;
                    vm.SelectedGroup.CarpetID = carpet.ID;
                    vm.SelectedGroup.FieldsVersion++;
                    carpet.Groups.Add(vm.SelectedGroup);
                    carpet.RefreshStats();
                    OnPropertyChanged("UnbindedGroups");

                    GenerateMatchNumbers();
                }
            }
        }

        private void UnbindGroup(AgeWeightGroup group)
        {
            if (Dialog.ShowMessageBox(this, "Вы уверены, что убрать группу с ковра?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.None) != MessageBoxResult.OK) return;

            var carpet = DataContext.Tournament.Carpets.FirstOrDefault(c => c.ID == group.CarpetID);
            if (carpet != null)
            {
                carpet.Groups.Remove(group);
                group.CarpetID = null;
                group.CarpetLabel = string.Empty;
                group.FieldsVersion++;
                Items = new ObservableCollection<Carpet>(DataContext.Tournament.Carpets);

                GenerateMatchNumbers();
            }
        }

        private void GenerateMatchNumbers()
        {
            _matchNumbersGenerator.Generate(DataContext.Tournament, Resolve<List<IGroupBracketProcessor>>());
        }

        private async Task ExportSchedulesAsync()
        {
            var tournament = DataContext.Tournament;
            var carpets = tournament?.Carpets;
            if (carpets == null || carpets.Count == 0)
            {
                Dialog.ShowMessageBox(this,
                    "Нет ковров для печати расписания.",
                    "Расписание ковров", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var carpetsWithPending = carpets
                .Where(c => tournament.Groups
                    .Where(g => g.Bracket != null && g.CarpetID == c.ID)
                    .SelectMany(g => g.Bracket.Rounds)
                    .SelectMany(r => r.RoundMatches)
                    .Any(rm => !rm.IsMatchCompleted))
                .ToList();

            if (carpetsWithPending.Count == 0)
            {
                Dialog.ShowMessageBox(this,
                    "На коврах нет непройденных схваток.",
                    "Расписание ковров", MessageBoxButton.OK, MessageBoxImage.Information);
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
                Description = "Выберите папку для сохранения расписаний ковров",
                ShowNewFolderButton = true,
                SelectedPath = defaultPath
            };

            if (Dialog.ShowFolderBrowserDialog(this, settings) != true) return;

            try
            {
                var jobs = new List<BulkPdfExportJob>();
                foreach (var carpet in carpetsWithPending)
                {
                    var capturedCarpet = carpet;
                    jobs.Add(new BulkPdfExportJob
                    {
                        FileName = "Расписание_" + BulkBracketPdfExporter.MakeSafeFileName(capturedCarpet.Name) + ".pdf",
                        Landscape = false,
                        ViewFactory = () =>
                        {
                            var vm = new PrintScheduleViewModel(DiContainer, capturedCarpet);
                            vm.InitData();
                            return new PrintScheduleView { DataContext = vm };
                        }
                    });
                }

                ShowSnackMessage($"Идет создание расписаний ковров: {jobs.Count} файлов...");

                var exporter = new BulkBracketPdfExporter();
                var result = await exporter.ExportAsync(jobs, settings.SelectedPath);

                var msg = $"Готово. Сохранено PDF: {result.Succeeded}";
                if (result.Skipped > 0) msg += $", пропущено: {result.Skipped}";
                if (result.Failures.Count > 0) msg += $", ошибок: {result.Failures.Count}";
                ShowSnackMessage(msg);

                if (result.Failures.Count > 0)
                {
                    Dialog.ShowMessageBox(this,
                        "Не удалось сохранить часть расписаний:\n\n" + string.Join("\n", result.Failures),
                        "Расписание ковров", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Dialog.ShowMessageBox(this,
                    "Ошибка экспорта: " + ex.Message,
                    "Расписание ковров", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}