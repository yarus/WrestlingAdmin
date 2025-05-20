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
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Print.PrintTeamApplication;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Applications
{
    public class ApplicationsViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        #region Fields

        private ObservableCollection<TeamApplication> _items;

        private ICommand _addAppCommand;
        private ICommand _approveAllCommand;
        private ICommand _editAppCommand;
        private ICommand _deleteAppCommand;
        private ICommand _addWrestlerCommand;
        private ICommand _editWrestlerCommand;
        private ICommand _deleteWrestlerCommand;
        private ICommand _printTeamApplicationCommand;

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
                return _quickButtons ??
                       (
                           _quickButtons = new List<CommandButtonItem>
                           {
                               new CommandButtonItem("Экспортировать список в Excel", PackIconKind.DatabaseExport, new RelayCommand(param => ExportData(), param => true))
                           }
                       );
            }
        }

        #region Binding Properties

        public string PageName => "Заявки";
        public override string PageTitle => "Заявки на участие";

        public int AppsCount => DataContext.Tournament?.TeamApplications.Count ?? 0;
        public int WrestlersCount => DataContext.Tournament?.TeamApplications.SelectMany(a => a.Wrestlers).ToList().Count ?? 0;

        public ObservableCollection<TeamApplication> Items
        {
            get { return _items; }
            set
            {
                _items = value;

                OnPropertyChanged("Items");
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
            }
        }

        public bool IsFilterEnabled => !string.IsNullOrEmpty(FilterString) || IsOnlyUnapprovedVisible;

        #endregion

        public override void InitData()
        {
            base.InitData();

            if (DataContext.Tournament == null)
            {
                throw new ApplicationException("Tournament property is not set!");
            }

            Items = DataContext.Tournament.TeamApplications;
        }

        #region Command Properties

        public ICommand PrintTeamApplicationCommand
        {
            get
            {
                if (_printTeamApplicationCommand == null)
                {
                    _printTeamApplicationCommand = new RelayCommand(param => PrintTeamApplication(param as TeamApplication), param => param != null);
                }

                return _printTeamApplicationCommand;
            }
        }

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
                    _addAppCommand = new RelayCommand(async (param) => await AddApplication(), param => true);
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
                    _editAppCommand = new RelayCommand(async (param) => await EditApplication(param as TeamApplication), param => param != null);
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
                    _deleteAppCommand = new RelayCommand(param => DeleteApplication(param as TeamApplication), param => param != null);
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
                    _addWrestlerCommand = new RelayCommand(async (param) => await AddWrestler(param as TeamApplication), param => param != null);
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
                    _editWrestlerCommand = new RelayCommand(async (param) => await EditWrestler(param as Wrestler), param => param != null);
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
                Items = DataContext.Tournament.TeamApplications;
            }
            else
            {
                var filteredWrestlers = DataContext.Tournament.Wrestlers.Where(w => (!isOnlyUnapprovedVisible || !w.IsRegistrationApproved) 
                    && (filter == null || filter.Length <=2 || (filter.Length > 2 && w.LastName.StartsWith(filter, true, CultureInfo.InvariantCulture)))).ToList();
                var filtered = new ObservableCollection<TeamApplication>(DataContext.Tournament.TeamApplications.Where(a => filteredWrestlers.Select(w => w.TeamID).Contains(a.ID)).Select(a => a.Clone() as TeamApplication));
                foreach (var teamApplication in filtered)
                {
                    teamApplication.Wrestlers = new ObservableCollection<Wrestler>(filteredWrestlers.Where(w => w.TeamID == teamApplication.ID).OrderBy(w => w.LastName).ThenBy(w => w.FirstName));
                }
                Items = filtered;
            }
        }

        private void ApproveAllWrestlers()
        {
            if (Dialog.ShowMessageBox(this, "Вы уверены, что хотите допустить всех спортсменов?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

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
                Items.Add(addAppVm.Item);

                if (Items != DataContext.Tournament.TeamApplications)
                {
                    DataContext.Tournament.TeamApplications.Add(addAppVm.Item);
                }

                OnPropertyChanged("AppsCount");

                if (!DataContext.TeamsCache.Any(x => !string.IsNullOrEmpty(x.HashTag) && x.HashTag == addAppVm.Item.HashTag))
                {
                    DataContext.TeamsCache.Add(addAppVm.Item);
                    
                    var cache = DiContainer.Resolve<ICacheManager>();
                    cache.SaveTeams(DataContext.TeamsCache);
                }
            }
        }

        private void PrintTeamApplication(TeamApplication teamApplication)
        {
            if (teamApplication == null) return;

            DataContext.Team = teamApplication;

            ShowPrintPreview(new PrintTeamApplicationViewModel(DiContainer));            
        }
        
        private async Task EditApplication(TeamApplication app)
        {
            var tmpApp = app.Clone() as TeamApplication;

            var addAppVm = new AddAppViewModel(DiContainer, tmpApp);
            addAppVm.InitData();

            var view = new AddAppDialog
            {
                DataContext = addAppVm
            };

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                var item = DataContext.Tournament.TeamApplications.FirstOrDefault(t => t.ID == app.ID);
                if (item != null)
                {
                    item.Sync(tmpApp);
                    foreach (var itemWrestler in item.Wrestlers)
                    {
                        itemWrestler.TeamName = item.ShortName;
                        itemWrestler.TeamCity = item.City;
                    }
                }

                OnPropertyChanged("AppsCount");
                
                var cache = DiContainer.Resolve<ICacheManager>();
                cache.SaveTeams(DataContext.TeamsCache);
            }
        }

        private void DeleteApplication(TeamApplication app)
        {
            if (Dialog.ShowMessageBox(this, "Вы уверены, что хотите удалить заявку?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

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

            DataContext.Tournament.TeamApplications.Remove(app);

            Items = new ObservableCollection<TeamApplication>(DataContext.Tournament.TeamApplications);

            OnPropertyChanged("AppsCount");
            OnPropertyChanged("WrestlersCount");
        }

        private async Task AddWrestler(TeamApplication app)
        {
            var tmpWresler = new Wrestler
            {
                ID = Guid.NewGuid(),
                TeamID = app.ID,
                TeamName = app.ShortName,
                TeamCity = app.City,
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
                tmpWresler.TeamID = app.ID;
                tmpWresler.TeamName = app.ShortName;
                tmpWresler.TeamCity = app.City;
                tmpWresler.Timestamp = DateTime.Now;

                DataContext.Tournament.Wrestlers.Add(tmpWresler);

                app.Wrestlers.Add(tmpWresler);

                app.Wrestlers = new ObservableCollection<Wrestler>(app.Wrestlers.OrderBy(w => w.LastName));

                AddWrestlerToHisGroup(tmpWresler);

                app.RefreshStats();
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
                var teamApp = Items.FirstOrDefault(a => a.ID == wrestler.TeamID);
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

                    teamApp.RefreshStats();
                    
                    var cache = DiContainer.Resolve<ICacheManager>();
                    cache.SaveWrestlers(DataContext.WrestlersCache);
                }
            }

            _editWrestlerDialogOpened = false;
        }

        private void DeleteWrestler(Wrestler wrestler)
        {
            if (Dialog.ShowMessageBox(this, "Вы уверены, что хотите удалить спортсмена из заявки?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

            RemoveWrestlerFromGroup(wrestler);

            DataContext.Tournament.Wrestlers.Remove(wrestler);            

            if (wrestler.TeamID.HasValue)
            {
                var teamApp = DataContext.Tournament.TeamApplications.FirstOrDefault(a => a.ID == wrestler.TeamID);
                if (teamApp != null)
                {
                    teamApp.Wrestlers.Remove(wrestler);
                    teamApp.RefreshStats();

                    Filter(_filterString, IsOnlyUnapprovedVisible);
                }                
            }

            OnPropertyChanged("WrestlersCount");            
        }

        private void ExportData()
        {
            if (DataContext.Tournament == null)
            {
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

            bool? success = Dialog.ShowSaveFileDialog(this, settings);
            if (success == true)
            {
                try
                {
                    using (var writer = new StreamWriter(settings.FileName))

                    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        var exportData = DataContext.Tournament.Wrestlers.Select(item =>
                        {
                            return new ExportedWrestler()
                            {
                                FullName = item.FullName,
                                BirthDate = item.BirthDate.HasValue ? item.BirthDate.Value.ToString("dd/MM/yyyy") : string.Empty,
                                GroupName = item.GroupName,
                                TeamCity = item.TeamCity,
                                TeamName = item.TeamName
                            };
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
        }

        #endregion
    }
}

public class ExportedWrestler
{
    public string GroupName { get; set; }
    public string FullName { get; set; }
    public string TeamName { get; set; }
    public string TeamCity { get; set; }
    public string BirthDate { get; set; }
}