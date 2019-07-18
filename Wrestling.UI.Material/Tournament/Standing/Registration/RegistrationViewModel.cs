using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Registration
{
    public class RegistrationViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        #region Fields

        private ObservableCollection<AgeWeightGroup> _items;

        private ICommand _showRegCommand;
        private ICommand _approveRegCommand;

        private string _filterString;
        private bool _isOnlyUnapprovedVisible;

        #endregion

        public RegistrationViewModel(IDiContainer container) : base(container)
        {
        }

        #region Binding Properties

        public string PageName => "Регистрация";
        public override string PageTitle => "Регистрация спортсменов";

        public int GroupsCount => DataContext.Tournament.GroupsCount;
        public int WrestlersCount => DataContext.Tournament.AppliedWrestlersCount;
        public int ApprovedCount => DataContext.Tournament.ApprovedWrestlersCount;

        public ObservableCollection<AgeWeightGroup> Items
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

                Filter();
                OnPropertyChanged("IsFilterEnabled");
            }
        }

        public string FilterString
        {
            get { return _filterString; }
            set
            {
                _filterString = value;
                OnPropertyChanged("FilterString");

                Filter();
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

            Items = GetRegistrationList();
        }

        #region Command Properties

        public ICommand ApproveRegistrationCommand
        {
            get
            {
                if (_approveRegCommand == null)
                {
                    _approveRegCommand = new RelayCommand(param => ApproveRegistration(param as AgeWeightGroup), param => param != null);
                }
                return _approveRegCommand;
            }
        }

        public ICommand ShowRegCommand
        {
            get
            {
                if (_showRegCommand == null)
                {
                    _showRegCommand = new RelayCommand(param => ShowRegistration(param as Wrestler), param => param != null);
                }
                return _showRegCommand;
            }
        }
        
        #endregion

        #region Private Methods

        private void RefreshState()
        {
            OnPropertyChanged("GroupsCount");
            OnPropertyChanged("WrestlersCount");
            OnPropertyChanged("ApprovedCount");
        }

        private ObservableCollection<AgeWeightGroup> GetRegistrationList()
        {
            var result = new List<AgeWeightGroup>();

            foreach (var group in DataContext.Tournament.Groups)
            {
                var groupTmp = group.Clone() as AgeWeightGroup;
                var wrestlers = new List<Wrestler>(group.Wrestlers);

                wrestlers.AddRange(DataContext.Tournament.Wrestlers.Where(w => w.GroupID.HasValue && w.GroupID.Value == groupTmp.ID && !group.Wrestlers.Contains(w)));

                groupTmp.Wrestlers = new List<Wrestler>(wrestlers.OrderBy(w => w.LastFirstName));

                result.Add(groupTmp);
            }

            if (IsFilterEnabled)
            {
                var filteredWrestlers = DataContext.Tournament.Wrestlers.Where(w => (!IsOnlyUnapprovedVisible || !w.IsRegistrationApproved) 
                    && (FilterString == null || FilterString.Length <= 2 || w.LastName.StartsWith(FilterString, true, CultureInfo.InvariantCulture))).ToList();
                var filtered = new List<AgeWeightGroup>(result.Where(g => filteredWrestlers.Select(w => w.GroupID).Contains(g.ID)).Select(a => a.Clone() as AgeWeightGroup));
                foreach (var group in filtered)
                {
                    group.Wrestlers = new List<Wrestler>(filteredWrestlers.Where(w => w.GroupID == group.ID).OrderBy(w => w.LastFirstName));
                }
                result = filtered;
            }

            return new ObservableCollection<AgeWeightGroup>(result.OrderBy(g => g.IsFemale).ThenByDescending(g => g.BirthYearMin).ThenBy(g => g.WeightMax));
        }

        private void Filter()
        {
            Items = GetRegistrationList();
        }

        private void ApproveRegistration(AgeWeightGroup group)
        {
            if (Dialog.ShowMessageBox(this, "Вы уверены, что хотите подтвердить регистрацию всех спортсменов группы?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

            var originalGroup = DataContext.Tournament.Groups.FirstOrDefault(g => g.ID == group.ID);

            foreach (var wr in group.Wrestlers)
            {
                wr.IsEntryFeePaid = true;
                wr.IsWeightApproved = true;
                
                if (originalGroup != null && !originalGroup.Wrestlers.Contains(wr))
                {
                    originalGroup.Wrestlers.Add(wr);
                }
            }

            group.RefreshState();
            RefreshState();
        }

        private async void ShowRegistration(Wrestler item)
        {
            var tmpItem = item.Clone() as Wrestler;

            var targetGroup = DataContext.Tournament.Groups.FirstOrDefault(g => g.ID == item.GroupID);

            var vm = new SetWeightViewModel(DiContainer, targetGroup, tmpItem);

            var view = new SetWeightDialog
            {
                DataContext = vm
            };
            
            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                item.Weight = tmpItem.Weight;
                item.IsEntryFeePaid = tmpItem.IsEntryFeePaid;
                item.PaidAmount = tmpItem.PaidAmount;
                item.IsWeightApproved = tmpItem.IsWeightApproved;

                var originalGroup = DataContext.Tournament.Groups.FirstOrDefault(g => g.ID == item.GroupID);
                if (originalGroup != null)
                {
                    if (item.IsRegistrationApproved)
                    {
                        if (!originalGroup.Wrestlers.Contains(item))
                        {
                            originalGroup.Wrestlers.Add(item);
                            originalGroup.Wrestlers = new List<Wrestler>(originalGroup.Wrestlers.OrderBy(w => w.LastFirstName));
                        }
                    }
                    else
                    {
                        if (originalGroup.Wrestlers.Contains(item))
                        {
                            originalGroup.Wrestlers.Remove(item);
                        }
                    }
                }

                var group = Items.FirstOrDefault(g => g.ID == item.GroupID);
                if (group != null)
                {
                    if (item.Weight.HasValue && item.Weight.Value > group.WeightMax)
                    {
                        item.GroupID = null;
                        item.GroupName = string.Empty;
                        item.Weight = null;

                        group.Wrestlers.Remove(item);
                        originalGroup?.Wrestlers.Remove(item);

                        Filter();

                        Dialog.ShowMessageBox(this, $"Вес спортсмена превышает максимально допустимый для весовой категории {group.Name}. Необходимо указать другую весовую категорию в заявке.", "Требуется подтверждение", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    group.RefreshState();
                }

                RefreshState();
            }
        }

        #endregion
    }
}