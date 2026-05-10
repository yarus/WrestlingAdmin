using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CsvHelper;
using MaterialDesignThemes.Wpf;
using MvvmDialogs.FrameworkDialogs.FolderBrowser;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Print;
using Wrestling.UI.Material.Tournament.Print.PrintApplications;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Applications
{
    public class ApplicationsViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        #region Fields

        private ObservableCollection<TeamApplicationViewModel> _items;

        private ICommand _addAppCommand;
        private ICommand _approveAllCommand;
        private ICommand _editAppCommand;
        private ICommand _deleteAppCommand;
        private ICommand _addWrestlerCommand;
        private ICommand _editWrestlerCommand;
        private ICommand _deleteWrestlerCommand;

        private string _filterString;
        private bool _isOnlyUnapprovedVisible;
        private IList<CommandButtonItem> _quickButtons;

        #endregion

        public ApplicationsViewModel(IDiContainer container) : base(container)
        {
        }

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                if (_quickButtons == null)
                {
                    CommandButtonItem weighingBtn = null;
                    var weighingCmd = new AsyncRelayCommand(
                        execute: async _ =>
                        {
                            weighingBtn.IsBusy = true;
                            try { await ExportWeighingProtocolsAsync(); }
                            finally { weighingBtn.IsBusy = false; }
                        },
                        canExecute: _ => true);
                    weighingBtn = new CommandButtonItem(
                        "Сохранить протоколы взвешивания",
                        PackIconKind.PrinterOutline,
                        weighingCmd);

                    CommandButtonItem wrestlersCsvBtn = null;
                    var wrestlersCsvCmd = new RelayCommand(
                        execute: _ =>
                        {
                            wrestlersCsvBtn.IsBusy = true;
                            try { ExportWrestlersCsv(); }
                            finally { wrestlersCsvBtn.IsBusy = false; }
                        },
                        canExecute: _ => true);
                    wrestlersCsvBtn = new CommandButtonItem(
                        "Экспортировать список участников",
                        PackIconKind.DatabaseExport,
                        wrestlersCsvCmd);

                    _quickButtons = new List<CommandButtonItem>
                    {
                        weighingBtn,
                        wrestlersCsvBtn
                    };
                }
                return _quickButtons;
            }
        }

        #region Binding Properties

        public string PageName => "Заявки";
        public override string PageTitle => "Заявки на участие";

        public int AppsCount => DataContext.Tournament?.TeamApplications.Count ?? 0;
        public int WrestlersCount => DataContext.Tournament?.Wrestlers.Count ?? 0;

        public ObservableCollection<TeamApplicationViewModel> Items
        {
            get { return _items; }
            set
            {
                _items = value;

                OnPropertyChanged("Items");
                OnPropertyChanged("ShouldAutoExpand");
            }
        }

        public bool ShouldAutoExpand
        {
            get
            {
                if (!IsFilterEnabled || _items == null || _items.Count == 0) return false;
                if (_items.Count == 1) return true;

                return _items.Sum(i => i.Wrestlers.Count) <= 5;
            }
        }

        public bool IsOnlyUnapprovedVisible
        {
            get { return _isOnlyUnapprovedVisible; }
            set
            {
                _isOnlyUnapprovedVisible = value;
                OnPropertyChanged("IsOnlyUnapprovedVisible");

                Filter(_filterString, IsOnlyUnapprovedVisible);
                OnPropertyChanged("IsFilterEnabled");
                OnPropertyChanged("ShouldAutoExpand");
            }
        }

        public string FilterString
        {
            get { return _filterString; }
            set
            {
                var prevValue = _filterString;
                if (_filterString != value)
                {
                    _filterString = value;
                    OnPropertyChanged("FilterString");

                    if (prevValue != null && prevValue.Length > 2 && _filterString.Length == 0 || _filterString.Length > 2)
                    {
                        Filter(_filterString, IsOnlyUnapprovedVisible);
                    }
                }

                OnPropertyChanged("IsFilterEnabled");
                OnPropertyChanged("ShouldAutoExpand");
            }
        }

        public bool IsFilterEnabled => !string.IsNullOrEmpty(FilterString) || IsOnlyUnapprovedVisible;

        #endregion

        public override void InitData()
        {
            base.InitData();

            if (DataContext.Tournament == null)
            {
                throw new InvalidOperationException("Tournament is not set on the data context. Navigate to a tournament before opening this view.");
            }

            // VM is a singleton (registered once in App.xaml.cs and reused across
            // navigations). Reset cached QuickButtons so a returning visit
            // re-evaluates conditional buttons (e.g. tournament-state checks).
            _quickButtons = null;

            Filter(_filterString, _isOnlyUnapprovedVisible);
        }

        #region Command Properties

        public ICommand ApproveAllCommand
        {
            get
            {
                if (_approveAllCommand == null)
                {
                    _approveAllCommand = new RelayCommand(param => ApproveAllWrestlers(), param => true);
                }
                return _approveAllCommand;
            }
        }

        public ICommand AddAppCommand
        {
            get
            {
                if (_addAppCommand == null)
                {
                    _addAppCommand = new AsyncRelayCommand(_ => AddApplication(), _ => true);
                }
                return _addAppCommand;
            }
        }

        public ICommand EditAppCommand
        {
            get
            {
                if (_editAppCommand == null)
                {
                    _editAppCommand = new AsyncRelayCommand(param => EditApplication(param as TeamApplicationViewModel), param => param != null);
                }
                return _editAppCommand;
            }
        }

        public ICommand DeleteAppCommand
        {
            get
            {
                if (_deleteAppCommand == null)
                {
                    _deleteAppCommand = new RelayCommand(param => DeleteApplication(param as TeamApplicationViewModel), param => param != null);
                }
                return _deleteAppCommand;
            }
        }

        public ICommand AddWrestlerCommand
        {
            get
            {
                if (_addWrestlerCommand == null)
                {
                    _addWrestlerCommand = new AsyncRelayCommand(param => AddWrestler(param as TeamApplicationViewModel), param => param != null);
                }
                return _addWrestlerCommand;
            }
        }

        public ICommand EditWrestlerCommand
        {
            get
            {
                if (_editWrestlerCommand == null)
                {
                    _editWrestlerCommand = new AsyncRelayCommand(param => EditWrestler(param as Wrestler), param => param != null);
                }
                return _editWrestlerCommand;
            }
        }

        public ICommand DeleteWrestlerCommand
        {
            get
            {
                if (_deleteWrestlerCommand == null)
                {
                    _deleteWrestlerCommand = new RelayCommand(param => DeleteWrestler(param as Wrestler), param => param != null);
                }
                return _deleteWrestlerCommand;
            }
        }

        #endregion

        #region Private Methods

        private void Filter(string filter, bool isOnlyUnapprovedVisible)
        {
            if (!IsFilterEnabled)
            {
                Items = new ObservableCollection<TeamApplicationViewModel>(DataContext.Tournament.TeamApplications.Select(x => new TeamApplicationViewModel(x, DataContext.Tournament)));
                return;
            }

            var hasTextFilter = !string.IsNullOrEmpty(filter) && filter.Length > 2;

            var matchingWrestlerTeamIds = new HashSet<Guid>(DataContext.Tournament.Wrestlers
                .Where(w => (!isOnlyUnapprovedVisible || !w.IsRegistrationApproved)
                            && (!hasTextFilter || ContainsCi(w.FullName, filter)))
                .Where(w => w.TeamID.HasValue)
                .Select(w => w.TeamID.Value));

            var matched = DataContext.Tournament.TeamApplications.Where(app =>
                matchingWrestlerTeamIds.Contains(app.ID)
                || (hasTextFilter
                    && (ContainsCi(app.ShortName, filter) || ContainsCi(app.FullName, filter) || ContainsCi(app.City, filter))
                    && (!isOnlyUnapprovedVisible
                        || DataContext.Tournament.Wrestlers.Any(w => w.TeamID == app.ID && !w.IsRegistrationApproved))));

            var filtered = new ObservableCollection<TeamApplicationViewModel>(matched.Select(a => new TeamApplicationViewModel(a, DataContext.Tournament)));
            foreach (var teamApplication in filtered)
            {
                teamApplication.SetFilter(filter, isOnlyUnapprovedVisible);
            }

            Items = filtered;
        }

        private static bool ContainsCi(string source, string value) =>
            !string.IsNullOrEmpty(source)
            && !string.IsNullOrEmpty(value)
            && source.IndexOf(value, StringComparison.InvariantCultureIgnoreCase) >= 0;

        private void ApproveAllWrestlers()
        {
            if (Dialog.ShowMessageBox(this, "Вы уверены, что хотите допустить всех спортсменов?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.None) != MessageBoxResult.OK) return;

            foreach (var wrestler in Tournament.Wrestlers)
            {
                wrestler.IsEntryFeePaid = true;
                wrestler.IsWeightApproved = true;
            }

            OnPropertyChanged("Items");
        }

        private async Task AddApplication()
        {
            var addAppVm = new AddAppViewModel(DiContainer, new TeamApplication
            {
                ID = Guid.NewGuid(),
                EmblemPath = $"{AppDomain.CurrentDomain.BaseDirectory}Images\\DefaultLogo.png"
            });
            addAppVm.InitData();

            var view = new AddAppDialog
            {
                DataContext = addAppVm
            };

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                DataContext.Tournament.TeamApplications.Add(addAppVm.Item);

                OnPropertyChanged("AppsCount");

                Items = new ObservableCollection<TeamApplicationViewModel>(DataContext.Tournament.TeamApplications.Select(x => new TeamApplicationViewModel(x, DataContext.Tournament)));

                OnPropertyChanged("Items");

                if (!DataContext.TeamsCache.Any(x => !string.IsNullOrEmpty(x.HashTag) && x.HashTag == addAppVm.Item.HashTag))
                {
                    DataContext.TeamsCache.Add(addAppVm.Item);
                    
                    var cache = DiContainer.Resolve<ICacheManager>();
                    cache.SaveTeams(DataContext.TeamsCache);
                }
            }
        }

        private async Task EditApplication(TeamApplicationViewModel app)
        {
            var tmpApp = app.Team.Clone() as TeamApplication;

            var addAppVm = new AddAppViewModel(DiContainer, tmpApp);
            addAppVm.InitData();

            var view = new AddAppDialog
            {
                DataContext = addAppVm
            };

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                app.Team.Sync(tmpApp);

                foreach (var itemWrestler in app.Wrestlers)
                {
                    itemWrestler.TeamName = tmpApp.ShortName;
                    itemWrestler.TeamCity = tmpApp.City;
                }

                OnPropertyChanged("AppsCount");

                OnPropertyChanged("Items");

                var cache = DiContainer.Resolve<ICacheManager>();
                cache.SaveTeams(DataContext.TeamsCache);
            }
        }

        private void DeleteApplication(TeamApplicationViewModel app)
        {
            if (Dialog.ShowMessageBox(this, "Вы уверены, что хотите удалить заявку?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.None) != MessageBoxResult.OK) return;

            foreach (var wrestler in app.Wrestlers)
            {
                DataContext.Tournament.Wrestlers.Remove(wrestler);

                if (wrestler.GroupID.HasValue && wrestler.IsRegistrationApproved)
                {
                    var group = DataContext.Tournament.Groups.FirstOrDefault(g => g.ID == wrestler.GroupID);
                    if (group != null)
                    {
                        group.Wrestlers.Remove(wrestler);
                        group.Bracket = null;
                        group.RefreshState();
                    }
                }
            }

            DataContext.Tournament.TeamApplications.Remove(app.Team);
            Items.Remove(app);

            OnPropertyChanged("AppsCount");
            OnPropertyChanged("WrestlersCount");
        }

        private async Task AddWrestler(TeamApplicationViewModel app)
        {
            var tmpWresler = new Wrestler
            {
                ID = Guid.NewGuid(),
                TeamID = app.Team.ID,
                TeamName = app.Team.ShortName,
                TeamCity = app.Team.City,
                Timestamp = DateTime.Now
            };

            if (!DataContext.Tournament.EntryFee.HasValue || DataContext.Tournament.EntryFee.Value == 0)
            {
                tmpWresler.IsEntryFeePaid = true;
                tmpWresler.PaidAmount = 0;
            }
            else
            {
                tmpWresler.PaidAmount = DataContext.Tournament.EntryFee;
                tmpWresler.IsEntryFeePaid = false;
            }

            var vm = new AddWrestlerViewModel(DiContainer, tmpWresler);
            vm.InitData();

            var view = new AddWrestlerDialog
            {
                DataContext = vm
            };

            var result = await DialogHost.Show(view, "RootDialog");
            if (result != null && (bool)result)
            {
                tmpWresler.TeamID = app.Team.ID;
                tmpWresler.TeamName = app.Team.ShortName;
                tmpWresler.TeamCity = app.Team.City;
                tmpWresler.Timestamp = DateTime.Now;

                DataContext.Tournament.Wrestlers.Add(tmpWresler);

                AddWrestlerToHisGroup(tmpWresler);

                var team = Items.First(x => x.Team.ID == tmpWresler.TeamID);
                team.SetFilter(FilterString, IsOnlyUnapprovedVisible);

                OnPropertyChanged("Items");
                OnPropertyChanged("WrestlersCount");
                
                if (!DataContext.WrestlersCache.Any(x => !string.IsNullOrEmpty(x.HashTag) && x.HashTag == tmpWresler.HashTag))
                {
                    DataContext.WrestlersCache.Add(tmpWresler);
                    
                    var cache = DiContainer.Resolve<ICacheManager>();
                    cache.SaveWrestlers(DataContext.WrestlersCache);
                }
            }
        }

        private bool _editWrestlerDialogOpened = false;

        private void RemoveWrestlerFromGroup(Wrestler wrestler)
        {
            if (!wrestler.GroupID.HasValue)
            {
                return;
            }

            var oldGroup = DataContext.Tournament.Groups.FirstOrDefault(gr => gr.ID == wrestler.GroupID.Value);
            if (oldGroup != null)
            {
                var groupEntry = oldGroup.Wrestlers.FirstOrDefault(wr => wr.ID == wrestler.ID);
                if (groupEntry != null)
                {
                    oldGroup.Wrestlers.Remove(groupEntry);
                    oldGroup.Bracket = null;
                    oldGroup.RefreshState();

                    wrestler.GroupID = null;
                    wrestler.GroupName = string.Empty;
                }
            }
            else
            {
                wrestler.GroupID = null;
                wrestler.GroupName = string.Empty;
            }
        }

        private void AddWrestlerToHisGroup(Wrestler wrestler)
        {
            if (wrestler.GroupID.HasValue && wrestler.IsRegistrationApproved)
            {
                var group = DataContext.Tournament.Groups.FirstOrDefault(g => g.ID == wrestler.GroupID.Value);
                if (group != null)
                {
                    var groupWrestler = group.Wrestlers.FirstOrDefault(wr => wr.ID == wrestler.ID);
                    if (groupWrestler == null)
                    {
                        group.Wrestlers.Add(wrestler);
                    }
                    
                    group.Bracket = null;
                    group.RefreshState();
                        
                    wrestler.GroupName = group.Name;
                }
            }
        }

        private async Task EditWrestler(Wrestler wrestler)
        {
            if (_editWrestlerDialogOpened) return;

            var tmpWrestler = wrestler.Clone() as Wrestler;
            tmpWrestler.Timestamp = DateTime.Now;

            var vm = new AddWrestlerViewModel(DiContainer, tmpWrestler);
            vm.InitData();

            var view = new AddWrestlerDialog
            {
                DataContext = vm
            };

            _editWrestlerDialogOpened = true;

            var result = await DialogHost.Show(view, "RootDialog");
            if (result != null && (bool)result)
            {
                var teamApp = Items.FirstOrDefault(a => a.Team.ID == wrestler.TeamID);
                if (teamApp != null && tmpWrestler != null)
                {
                    var isGroupChanged = tmpWrestler.GroupID != wrestler.GroupID || (!tmpWrestler.GroupID.HasValue && wrestler.GroupID.HasValue);
                    
                    if (isGroupChanged)
                    {
                        // Remove wrestler from old group
                        RemoveWrestlerFromGroup(wrestler);

                        // Remove wrestler from new group if it was already added
                        RemoveWrestlerFromGroup(tmpWrestler);
                    }
                    
                    wrestler.Sync(tmpWrestler);

                    if (isGroupChanged)
                    {
                        // Add wrestler to Group if all data is valid
                        AddWrestlerToHisGroup(tmpWrestler);
                    }
                    
                    var cache = DiContainer.Resolve<ICacheManager>();
                    cache.SaveWrestlers(DataContext.WrestlersCache);
                }

                OnPropertyChanged("Items");
            }

            _editWrestlerDialogOpened = false;
        }

        private void DeleteWrestler(Wrestler wrestler)
        {
            if (Dialog.ShowMessageBox(this, "Вы уверены, что хотите удалить спортсмена из заявки?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.None) != MessageBoxResult.OK) return;

            RemoveWrestlerFromGroup(wrestler);

            DataContext.Tournament.Wrestlers.Remove(wrestler);

            var team = Items.First(x => x.Team.ID == wrestler.TeamID);
            team.SetFilter(FilterString, IsOnlyUnapprovedVisible);

            OnPropertyChanged("Items");
            OnPropertyChanged("WrestlersCount");            
        }

        // Bulk-PDF export of weighing protocols, one PDF per group with wrestlers.
        // Identical pipeline to the «Данные» card it replaces.
        private async Task ExportWeighingProtocolsAsync()
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

            var tournamentDir = !string.IsNullOrWhiteSpace(tournament.FileName)
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

            try
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
        }

        private void ExportWrestlersCsv()
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

        #endregion

        // CsvHelper writes by reflection on this POCO's properties.
        private sealed class ExportedWrestler
        {
            public string GroupName { get; set; }
            public string FullName { get; set; }
            public string TeamName { get; set; }
            public string TeamCity { get; set; }
            public string BirthDate { get; set; }
        }
    }
}