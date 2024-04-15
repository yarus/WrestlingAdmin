using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Details
{
    public class DetailsViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        private ICommand _addGroupCommand;
        private ICommand _editGroupCommand;
        private ICommand _deleteGroupCommand;
        private ICommand _generateGroupsCommand;

        private ObservableCollection<AgeWeightGroup> _groups;

        public string PageName => "Положение";
        public override string PageTitle => "Информация о Соревнованиях";
        
        public DetailsViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            if (DataContext.Tournament == null)
            {
                throw new ApplicationException("Tournament property is not set!");
            }

            Groups = new ObservableCollection<AgeWeightGroup>(DataContext.Tournament.Groups.OrderBy(g => g.IsFemale).ThenByDescending(g => g.BirthYearMin).ThenBy(g => g.WeightMax));
        }

        public ObservableCollection<AgeWeightGroup> Groups
        {
            get { return _groups; }
            set
            {
                _groups = value;

                OnPropertyChanged("Groups");
            }
        }

        #region Command Properties

        public ICommand GenerateGroupsCommand
        {
            get
            {
                if (_generateGroupsCommand == null)
                {
                    _generateGroupsCommand = new RelayCommand(async (param) => await GenerateGroups());
                }

                return _generateGroupsCommand;
            }
        }
        
        public ICommand AddGroupCommand
        {
            get
            {
                if (_addGroupCommand == null)
                {
                    _addGroupCommand = new RelayCommand(async (param) => await AddGroup());
                }
                return _addGroupCommand;
            }
        }

        public ICommand DeleteGroupCommand
        {
            get
            {
                if (_deleteGroupCommand == null)
                {
                    _deleteGroupCommand = new RelayCommand(
                        param => DeleteGroup(param as AgeWeightGroup),
                        param => param != null
                    );
                }
                return _deleteGroupCommand;
            }
        }

        public ICommand EditGroupCommand
        {
            get
            {
                if (_editGroupCommand == null)
                {
                    _editGroupCommand = new RelayCommand(
                        param => EditGroup(param as AgeWeightGroup),
                        param => param != null
                    );
                }
                return _editGroupCommand;
            }
        }
        
        #endregion

        #region Private Methods
        
        private async Task GenerateGroups()
        {
            var vm = new GenerateGroupsDialogViewModel(DiContainer);
        
            var view = new GenerateGroupsDialog
            {
                DataContext = vm
            };
        
            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                var response = Resolve<IGroupGenerator>().Generate(vm.GenerateText, Tournament.Settings);

                if (response.IsFailed)
                {
                    ShowSnackMessage($"Ошибка генерации групп: {string.Join(",", response.Errors.Select(x => x.Message).ToList())}");
                    return;
                }

                Tournament.Groups = new ObservableCollection<AgeWeightGroup>(response.Value);
                Groups = Tournament.Groups;
                OnPropertyChanged(nameof(Groups));
            }
        }
        
        private async Task AddGroup()
        {
            var tmp = new AgeWeightGroup
            {
                ID = Guid.NewGuid(),
                IsFemale = false,
                MaxActionSecond = DataContext.Tournament.Settings.MaxActionSecond,
                MaxRoundSecond = DataContext.Tournament.Settings.MaxRoundSecond,
                MaxTimeoutSecond = DataContext.Tournament.Settings.MaxTimeoutSecond
            };

            var vm = new AddGroupViewModel(DiContainer, tmp);

            var view = new AddGroupDialog
            {
                DataContext = vm
            };

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                DataContext.Tournament.Groups.Add(tmp);
                Groups.Add(tmp);
            }
        }

        private void DeleteGroup(AgeWeightGroup group)
        {
            if (Dialog.ShowMessageBox(this, "Вы уверены, что хотите удалить группу?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

            foreach (var wr in DataContext.Tournament.Wrestlers)
            {
                if (wr.GroupName == group.Name)
                {
                    wr.GroupName = string.Empty;
                    wr.GroupID = null;
                }
            }

            DataContext.Tournament.Groups.Remove(group);
            Groups.Remove(group);
        }

        private async void EditGroup(AgeWeightGroup group)
        {
            var tmp = group.Clone() as AgeWeightGroup;

            var vm = new AddGroupViewModel(DiContainer, tmp);

            var view = new AddGroupDialog
            {
                DataContext = vm
            };

            var result = await DialogHost.Show(view, "RootDialog", ClosingEventHandler);
            if (result != null && (bool)result)
            {
                var item = Groups.FirstOrDefault(a => a.ID == group.ID);
                if (item != null)
                {
                    item.Sync(tmp);

                    RemoveWrestlersWhichNotFeatToGroupLimits(item);
                    UpdateWrestlersGroupData(item);
                }
            }
        }

        private void UpdateWrestlersGroupData(AgeWeightGroup item)
        {
            var appointedWrestlers = DataContext.Tournament.Wrestlers.Where(w => w.GroupID == item.ID).ToList();
            foreach (var appointedWrestler in appointedWrestlers)
            {
                appointedWrestler.GroupName = item.Name;
            }
        }

        private void RemoveWrestlersWhichNotFeatToGroupLimits(AgeWeightGroup item)
        {
            foreach (var wr in DataContext.Tournament.Wrestlers)
            {
                if (wr.GroupName == item.Name &&
                    (item.IsFemale != wr.IsFemale
                    || wr.Weight.HasValue && item.WeightMax < wr.Weight.Value
                    || wr.BirthDate.HasValue && (wr.BirthDate.Value.Year < item.BirthYearMin)))
                {
                    wr.GroupName = string.Empty;
                    wr.GroupID = null;

                    if (item.Wrestlers.Contains(wr))
                    {
                        item.Wrestlers.Remove(wr);
                    }
                }
            }
        }

        private void ClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            var view = eventArgs.Session.Content as AddGroupDialog;

            var vm = view?.DataContext as AddGroupViewModel;
            if (vm == null) return;

            var originalItem = Groups.FirstOrDefault(a => a.ID == vm.Item.ID);
            if (originalItem == null || originalItem.Name == vm.Item.Name || originalItem.Wrestlers.Count == 0) return;

            if (Dialog.ShowMessageBox(this,
                    "Параметры группы были изменены, заявки на участие в данной группе будут удалены. Вы уверены?",
                    "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK)
            {
                eventArgs.Cancel();
            }
        }

        #endregion
    }
}