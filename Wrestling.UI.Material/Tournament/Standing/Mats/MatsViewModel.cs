using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.UI.Material.Utils;
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
        private IMatRedistributionService _redistribution;

        private ObservableCollection<AgeWeightGroup> _groups;
        private ObservableCollection<Mat> _items;

        private ICommand _addMatCommand;
        private ICommand _deleteMatCommand;
        private ICommand _editMatCommand;
        private ICommand _bindGroupCommand;
        private ICommand _unbindGroupCommand;
        private ICommand _upGroupCommand;
        private ICommand _downGroupCommand;
        private ICommand _addPartCommand;
        private ICommand _renamePartCommand;
        private ICommand _deletePartCommand;
        private ICommand _moveGroupToPartCommand;

        private IList<CommandButtonItem> _quickButtons;

        public string PageName => T("Nav_Schedule", "Расписание");
        public override string PageTitle => T("Mats_PageTitle", "Расписание");
        public int UnbindedGroups => _groups != null && _items != null ? _groups.Count - _items.SelectMany(c => c.Groups).Count() : 0;

        // Parts surface — bound to the tournament's first-class Parts list.
        // HasMultipleParts is the gate the XAML uses to show / hide the parts
        // toolbar and per-group MoveToPart popups; single-part tournaments
        // look exactly like the pre-Parts UI.
        public ObservableCollection<TournamentPart> Parts => DataContext?.Tournament?.Parts;
        public bool HasMultipleParts => (Parts?.Count ?? 0) > 1;

        private TournamentPart _selectedPart;

        // The part currently selected in the distribution tab strip.
        // Drives CurrentPartMatPanels and is two-way bound to the ListBox
        // SelectedItem in the XAML.
        public TournamentPart SelectedPart
        {
            get => _selectedPart;
            set
            {
                if (_selectedPart == value) return;
                _selectedPart = value;
                OnPropertyChanged(nameof(SelectedPart));
                OnPropertyChanged(nameof(CurrentPartMatPanels));
            }
        }

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
                        T("Mats_ExportSchedules_Tooltip", "Сохранить протоколы расписания"),
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
            _redistribution = Resolve<IMatRedistributionService>();

            Items = DataContext.Tournament.Mats;

            _groups = DataContext.Tournament.Groups;

            VerifyMats();
            EnsureSelectedPart();
        }

        // Pick a sensible default for SelectedPart on entry, and re-validate
        // if the operator adds/deletes parts during the session. Without a
        // valid SelectedPart the bottom distribution section would be empty.
        private void EnsureSelectedPart()
        {
            var parts = Parts;
            if (parts == null || parts.Count == 0)
            {
                if (_selectedPart != null)
                {
                    _selectedPart = null;
                    OnPropertyChanged(nameof(SelectedPart));
                    OnPropertyChanged(nameof(CurrentPartMatPanels));
                }
                return;
            }

            if (_selectedPart == null || !parts.Contains(_selectedPart))
            {
                // Prefer the first part that has groups so a freshly-added
                // (still-empty) trailing part doesn't blank the screen.
                var preferred = parts.FirstOrDefault(p => _groups != null && _groups.Any(g => g.PartID == p.ID))
                                ?? parts[0];
                _selectedPart = preferred;
                OnPropertyChanged(nameof(SelectedPart));
                OnPropertyChanged(nameof(CurrentPartMatPanels));
            }
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

        public ICommand AddPartCommand =>
            _addPartCommand ?? (_addPartCommand = new RelayCommand(_ => AddPart(), _ => true));

        public ICommand RenamePartCommand =>
            _renamePartCommand ?? (_renamePartCommand = new AsyncRelayCommand(
                param => RenamePartAsync(param as TournamentPart),
                p => p is TournamentPart));

        public ICommand DeletePartCommand =>
            _deletePartCommand ?? (_deletePartCommand = new RelayCommand(param => DeletePart(param as TournamentPart), p => p is TournamentPart));

        // The XAML binds this to per-group popup items. CommandParameter is
        // the (Group, TargetPart) tuple — we pack it via an anonymous Tuple<>
        // so a single command serves the whole Parts dropdown.
        public ICommand MoveGroupToPartCommand =>
            _moveGroupToPartCommand ?? (_moveGroupToPartCommand = new RelayCommand(
                param =>
                {
                    if (param is Tuple<AgeWeightGroup, TournamentPart> tuple)
                    {
                        MoveGroupToPart(tuple.Item1, tuple.Item2);
                    }
                },
                p => p is Tuple<AgeWeightGroup, TournamentPart>));

        private void AddPart()
        {
            // Auto-name as «Часть N» where N = current count + 1. Operator
            // can rename via the ✏ on the part chip.
            var parts = DataContext?.Tournament?.Parts;
            if (parts == null) return;

            var name = string.Format(T("Mats_Part_AutoName_Format", "Часть {0}"), parts.Count + 1);
            var newPart = new TournamentPart { ID = Guid.NewGuid(), Name = name };
            parts.Add(newPart);
            DataContext.Tournament.MetaVersion++;

            OnPropertyChanged(nameof(Parts));
            OnPropertyChanged(nameof(HasMultipleParts));
            EnsureSelectedPart();
        }

        private async Task RenamePartAsync(TournamentPart part)
        {
            if (part == null) return;

            var dlg = new RenamePartDialogViewModel { NewName = part.Name ?? string.Empty };
            var view = new RenamePartDialog { DataContext = dlg };
            var result = await DialogHost.Show(view, "RootDialog");
            if (!(result is bool ok) || !ok) return;

            var newName = dlg.NewName;
            if (string.IsNullOrWhiteSpace(newName) || newName == part.Name) return;

            part.Name = newName;
            if (DataContext?.Tournament != null) DataContext.Tournament.MetaVersion++;
        }

        private void DeletePart(TournamentPart part)
        {
            if (part == null || _groups == null || DataContext?.Tournament == null) return;

            if (DataContext.Tournament.Parts.Count <= 1)
            {
                Dialog.ShowMessageBox(this,
                    T("Mats_Part_DeleteBlocked_LastPart", "Нельзя удалить единственную часть."),
                    T("Mats_Part_Delete_Title", "Удалить часть"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_groups.Any(g => g.PartID == part.ID))
            {
                Dialog.ShowMessageBox(this,
                    T("Mats_Part_DeleteBlocked_NonEmpty", "В части есть группы. Сначала перенесите их в другую часть."),
                    T("Mats_Part_Delete_Title", "Удалить часть"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (Items.Any(m => m.ActivePartID == part.ID))
            {
                Dialog.ShowMessageBox(this,
                    T("Mats_Part_DeleteBlocked_Active", "Часть активна на одном из ковров."),
                    T("Mats_Part_Delete_Title", "Удалить часть"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (Dialog.ShowMessageBox(this,
                T("Mats_Part_Delete_Body", "Удалить эту часть? В ней не должно быть групп."),
                T("Mats_Part_Delete_Title", "Удалить часть"),
                MessageBoxButton.OKCancel, MessageBoxImage.None) != MessageBoxResult.OK) return;

            DataContext.Tournament.Parts.Remove(part);
            DataContext.Tournament.MetaVersion++;
            OnPropertyChanged(nameof(Parts));
            OnPropertyChanged(nameof(HasMultipleParts));
            EnsureSelectedPart();
        }

        private void MoveGroupToPart(AgeWeightGroup group, TournamentPart targetPart)
        {
            if (group == null || targetPart == null || _redistribution == null) return;

            var outcome = _redistribution.MoveGroupToPart(DataContext.Tournament, group, targetPart.ID);
            switch (outcome.Outcome)
            {
                case MoveOutcome.Moved:
                    DataContext.Tournament.MetaVersion++;
                    OnPropertyChanged(nameof(CurrentPartMatPanels));
                    ShowSnackMessage(string.Format(
                        T("Mats_MoveToPart_Snack", "Группа «{0}» перенесена в часть «{1}»"),
                        group.Name, targetPart.Name));
                    break;

                case MoveOutcome.BlockedByCompletedMatches:
                    // Silent no-op: the popup button is already disabled in
                    // this state and explains the constraint via its tooltip.
                    // Surfacing a dialog here would duplicate the message.
                    break;

                case MoveOutcome.BlockedByLiveMatch:
                    Dialog.ShowMessageBox(this,
                        T("MatBoard_LiveMatchBlock_Body",
                            "На Ковре сейчас идёт схватка. Дождитесь Approve или нажмите Revert."),
                        T("MatBoard_LiveMatchBlock_Title", "Группа в работе"),
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
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
            OnPropertyChanged(nameof(UnbindedGroups));
            OnPropertyChanged(nameof(CurrentPartMatPanels));
        }

        private async Task BindGroupAsync(Mat mat)
        {
            await BindGroupAsyncCore(mat, partFilter: null);
        }

        // (Part, Mat) variant called from the nested multi-part layout. Pre-
        // filters the picker to groups in this part (or unpartitioned) so the
        // operator doesn't see groups belonging to another part. After the
        // mat assignment, stamps PartID so a freshly orphan group lands in
        // the right part automatically.
        private async Task BindGroupForPartAsync(Mat mat, TournamentPart part)
        {
            await BindGroupAsyncCore(mat, partFilter: part?.ID);
            if (part != null && _redistribution != null)
            {
                // Pick the last-bound group on this mat — the dialog's
                // SelectedGroup isn't stored on the VM after dialog close,
                // so the redistribution-service path's structural change is
                // already done. The PartID stamp happens inside core.
            }
        }

        private async Task BindGroupAsyncCore(Mat mat, System.Nullable<System.Guid> partFilter)
        {
            var vm = new BindGroupViewModel(DiContainer);
            vm.PartFilter = partFilter;
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
                    _redistribution.MoveGroupToMat(DataContext.Tournament, vm.SelectedGroup, mat.ID);
                    // Stamp PartID for newly-bound groups picked from the
                    // unpartitioned pool. Existing PartID is preserved if
                    // already set (a partitioned group was picked).
                    if (partFilter.HasValue && !vm.SelectedGroup.PartID.HasValue)
                    {
                        vm.SelectedGroup.PartID = partFilter.Value;
                        vm.SelectedGroup.FieldsVersion++;
                    }
                    OnPropertyChanged("UnbindedGroups");
                    OnPropertyChanged(nameof(CurrentPartMatPanels));
                }
            }
        }

        public System.Windows.Input.ICommand BindGroupForPartCommand =>
            _bindGroupForPartCommand ?? (_bindGroupForPartCommand = new AsyncRelayCommand(
                param =>
                {
                    if (param is System.Tuple<Mat, TournamentPart> tuple)
                    {
                        return BindGroupForPartAsync(tuple.Item1, tuple.Item2);
                    }
                    return System.Threading.Tasks.Task.CompletedTask;
                },
                p => p is System.Tuple<Mat, TournamentPart>));

        private System.Windows.Input.ICommand _bindGroupForPartCommand;

        // Mat panels for the currently selected part. Recomputed on access;
        // cheap (3-5 mats × small group lists). The bottom section of the
        // view binds here and refreshes whenever SelectedPart changes.
        public System.Collections.Generic.IList<MatsPartMatPanelVm> CurrentPartMatPanels
        {
            get
            {
                if (_selectedPart == null || _items == null)
                {
                    return new System.Collections.Generic.List<MatsPartMatPanelVm>();
                }

                var panels = new System.Collections.Generic.List<MatsPartMatPanelVm>();
                foreach (var mat in _items)
                {
                    var groups = mat.Groups.Where(g => g.PartID == _selectedPart.ID).ToList();
                    panels.Add(new MatsPartMatPanelVm { Mat = mat, Part = _selectedPart, Groups = groups });
                }
                return panels;
            }
        }

        private void UnbindGroup(AgeWeightGroup group)
        {
            if (Dialog.ShowMessageBox(this, T("Mats_UnbindGroup_Body", "Вы уверены, что убрать группу с ковра?"), T("MatchResults_ConfirmTitle", "Требуется подтверждение"), MessageBoxButton.OKCancel, MessageBoxImage.None) != MessageBoxResult.OK) return;

            _redistribution.MoveGroupToMat(DataContext.Tournament, group, null);
            Items = new ObservableCollection<Mat>(DataContext.Tournament.Mats);
            OnPropertyChanged("UnbindedGroups");
            OnPropertyChanged(nameof(CurrentPartMatPanels));
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

            var selectedFolder = FolderPicker.PickFolder(
                T("MatsExport_FolderPicker_Title", "Выберите папку для сохранения расписаний ковров"),
                defaultPath);
            if (string.IsNullOrEmpty(selectedFolder)) return;

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
                var result = await exporter.ExportAsync(jobs, selectedFolder);

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