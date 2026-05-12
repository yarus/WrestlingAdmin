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

namespace Wrestling.UI.Material.Tournament.Standing.Mats
{
    public class MatsViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        private IMatchNumbersGenerator _matchNumbersGenerator;

        private ObservableCollection<AgeWeightGroup> _groups;
        private ObservableCollection<Mat> _items;

        private ICommand _addMatCommand;
        private ICommand _deleteMatCommand;
        private ICommand _editMatCommand;
        private ICommand _bindGroupCommand;
        private ICommand _unbindGroupCommand;
        private ICommand _upGroupCommand;
        private ICommand _downGroupCommand;

        private IList<CommandButtonItem> _quickButtons;

        public string PageName => T("Nav_Schedule", "Расписание");
        public override string PageTitle => T("Mats_PageTitle", "Расписание");
        public int UnbindedGroups => _groups != null && _items != null ? _groups.Count - _items.SelectMany(c => c.Groups).Count() : 0;

        public MatsViewModel(IDiContainer container) : base(container)
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
                        T("Mats_ExportSchedules_Tooltip", "Скачать расписания ковров PDF"),
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

            Items = DataContext.Tournament.Mats;

            _groups = DataContext.Tournament.Groups;

            VerifyMats();
        }

        private void VerifyMats()
        {
            var invalidMatGroups = _groups.Where(g => g.MatID.HasValue && Items.FirstOrDefault(c => c.ID == g.MatID.Value) == null).ToList();
            foreach (var invalidGroup in invalidMatGroups)
            {
                invalidGroup.MatID = null;
                invalidGroup.MatLabel = string.Empty;
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

        public ICommand AddMatCommand
        {
            get
            {
                if (_addMatCommand == null)
                {
                    _addMatCommand = new AsyncRelayCommand(param => AddMatAsync(), param => true);
                }
                return _addMatCommand;
            }
        }

        public ICommand EditMatCommand
        {
            get
            {
                if (_editMatCommand == null)
                {
                    _editMatCommand = new AsyncRelayCommand(param => EditMatAsync(param as Mat), param => param != null);
                }
                return _editMatCommand;
            }
        }

        public ICommand DeleteMatCommand
        {
            get
            {
                if (_deleteMatCommand == null)
                {
                    _deleteMatCommand = new RelayCommand(param => DeleteMat(param as Mat), param => param != null);
                }
                return _deleteMatCommand;
            }
        }

        public ICommand BindGroupCommand
        {
            get
            {
                if (_bindGroupCommand == null)
                {
                    _bindGroupCommand = new AsyncRelayCommand(param => BindGroupAsync(param as Mat), param => param != null);
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

        public ObservableCollection<Mat> Items
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
            var mat = Items.FirstOrDefault(c => c.ID == group.MatID);
            if (mat != null)
            {
                var i = mat.Groups.IndexOf(group);
                var j = i - 1;
                if (j >= 0)
                {
                    mat.Groups.Swap(i, j);
                }
            }
        }

        private void DownGroup(AgeWeightGroup group)
        {
            var mat = Items.FirstOrDefault(c => c.ID == group.MatID);
            if (mat != null)
            {
                var i = mat.Groups.IndexOf(group);
                var j = i + 1;
                if (j < mat.Groups.Count)
                {
                    mat.Groups.Swap(i, j);
                }
            }
        }

        private async Task AddMatAsync()
        {
            var tmpMat = new Mat
            {
                ID = Guid.NewGuid()
            };

            var view = new MatDialog
            {
                DataContext = tmpMat
            };

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool) result)
            {
                Items.Add(tmpMat);                
            }
        }

        private async Task EditMatAsync(Mat mat)
        {
            var tmpMat = mat.Clone() as Mat;

            var view = new MatDialog
            {
                DataContext = tmpMat
            };

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                mat.Sync(tmpMat);
            }
        }

        private void DeleteMat(Mat mat)
        {
            if (Dialog.ShowMessageBox(this, T("Mats_Delete_Body", "Вы уверены, что хотите удалить ковер?"), T("MatchResults_ConfirmTitle", "Требуется подтверждение"), MessageBoxButton.OKCancel, MessageBoxImage.None) != MessageBoxResult.OK) return;

            var groups = _groups.Where(g => g.MatID.HasValue && g.MatID.Value == mat.ID.Value).ToList();

            foreach (var group in groups)
            {
                group.MatID = null;
                group.MatLabel = string.Empty;
                group.FieldsVersion++;
            }

            Items.Remove(mat);
        }

        private async Task BindGroupAsync(Mat mat)
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
                    vm.SelectedGroup.MatLabel = mat.Name;
                    vm.SelectedGroup.MatID = mat.ID;
                    vm.SelectedGroup.FieldsVersion++;
                    mat.Groups.Add(vm.SelectedGroup);
                    mat.RefreshStats();
                    OnPropertyChanged("UnbindedGroups");

                    GenerateMatchNumbers();
                }
            }
        }

        private void UnbindGroup(AgeWeightGroup group)
        {
            if (Dialog.ShowMessageBox(this, T("Mats_UnbindGroup_Body", "Вы уверены, что убрать группу с ковра?"), T("MatchResults_ConfirmTitle", "Требуется подтверждение"), MessageBoxButton.OKCancel, MessageBoxImage.None) != MessageBoxResult.OK) return;

            var mat = DataContext.Tournament.Mats.FirstOrDefault(c => c.ID == group.MatID);
            if (mat != null)
            {
                mat.Groups.Remove(group);
                group.MatID = null;
                group.MatLabel = string.Empty;
                group.FieldsVersion++;
                Items = new ObservableCollection<Mat>(DataContext.Tournament.Mats);

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
            var mats = tournament?.Mats;
            if (mats == null || mats.Count == 0)
            {
                Dialog.ShowMessageBox(this,
                    T("MatsExport_NoMats", "Нет ковров для печати расписания."),
                    T("MatsExport_DialogTitle", "Расписание ковров"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var matsWithPending = mats
                .Where(c => tournament.Groups
                    .Where(g => g.Bracket != null && g.MatID == c.ID)
                    .SelectMany(g => g.Bracket.Rounds)
                    .SelectMany(r => r.RoundMatches)
                    .Any(rm => !rm.IsMatchCompleted))
                .ToList();

            if (matsWithPending.Count == 0)
            {
                Dialog.ShowMessageBox(this,
                    T("MatsExport_NoPending", "На коврах нет непройденных схваток."),
                    T("MatsExport_DialogTitle", "Расписание ковров"), MessageBoxButton.OK, MessageBoxImage.Information);
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
                Description = T("MatsExport_FolderPicker_Title", "Выберите папку для сохранения расписаний ковров"),
                ShowNewFolderButton = true,
                SelectedPath = defaultPath
            };

            if (Dialog.ShowFolderBrowserDialog(this, settings) != true) return;

            try
            {
                var jobs = new List<BulkPdfExportJob>();
                foreach (var mat in matsWithPending)
                {
                    var capturedMat = mat;
                    jobs.Add(new BulkPdfExportJob
                    {
                        FileName = T("MatsExport_FilePrefix", "Расписание_") + BulkBracketPdfExporter.MakeSafeFileName(capturedMat.Name) + ".pdf",
                        Landscape = false,
                        ViewFactory = () =>
                        {
                            var vm = new PrintScheduleViewModel(DiContainer, capturedMat);
                            vm.InitData();
                            return new PrintScheduleView { DataContext = vm };
                        }
                    });
                }

                ShowSnackMessage(string.Format(T("MatsExport_Snack_Building", "Идет создание расписаний ковров: {0} файлов..."), jobs.Count));

                var exporter = new BulkBracketPdfExporter();
                var result = await exporter.ExportAsync(jobs, settings.SelectedPath);

                var msg = string.Format(T("Export_Snack_Done", "Готово. Сохранено PDF: {0}"), result.Succeeded);
                if (result.Skipped > 0) msg += string.Format(T("Export_Snack_Skipped", ", пропущено: {0}"), result.Skipped);
                if (result.Failures.Count > 0) msg += string.Format(T("Export_Snack_Failed", ", ошибок: {0}"), result.Failures.Count);
                ShowSnackMessage(msg);

                if (result.Failures.Count > 0)
                {
                    Dialog.ShowMessageBox(this,
                        T("Export_PartialFailure", "Не удалось сохранить часть протоколов:") + "\n\n" + string.Join("\n", result.Failures),
                        T("MatsExport_DialogTitle", "Расписание ковров"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Dialog.ShowMessageBox(this,
                    T("Export_ErrorPrefix", "Ошибка экспорта: ") + ex.Message,
                    T("MatsExport_DialogTitle", "Расписание ковров"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}