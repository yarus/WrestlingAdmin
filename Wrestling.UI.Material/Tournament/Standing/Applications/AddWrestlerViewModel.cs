using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Standing.Applications
{
    public class AddWrestlerViewModel : ViewModelBase
    {
        private Wrestler _item;
        private Wrestler _selectedItem;

        private AgeWeightGroup _selectedGroup;
        private ObservableCollection<AgeWeightGroup> _groups;
        private ObservableCollection<Wrestler> _cachedAthletes;
        private ObservableCollection<string> _levels;

        public AddWrestlerViewModel(IDiContainer container, Wrestler item) : base(container)
        {
            _item = item;
        }

        public override void InitData()
        {
            base.InitData();

            if (DataContext.Tournament == null)
            {
                throw new ApplicationException("Tournament property is not set!");
            }

            _levels = new ObservableCollection<string>()
            {
                "МСМК", "МС", "КМС", "I", "II", "III", "I юн", "II юн", "III юн"
            };
            
            _groups = DataContext.Tournament.Groups;

            _selectedGroup = _item.GroupID.HasValue ? _groups.FirstOrDefault(g => g.ID == _item.GroupID) : null;

            CachedAthletes = new ObservableCollection<Wrestler>(DataContext.WrestlersCache);
        }

        public ObservableCollection<string> Levels => _levels;

        public Func<string, object, bool> AthleteFilter
        {
            get
            {
                return (searchText, obj) =>
                {
                    var item = obj as Wrestler;

                    if (item == null || string.IsNullOrEmpty(searchText) || searchText.Length < 3) return false;

                    return (!string.IsNullOrEmpty(item.HashTag) && item.HashTag.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                           || (!string.IsNullOrEmpty(item.LastName) && item.LastName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                           || (!string.IsNullOrEmpty(item.FirstName) && item.FirstName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                           || (!string.IsNullOrEmpty(item.MiddleName) && item.MiddleName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
                };
            }
        }

        public Wrestler SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                _selectedItem = value;

                if (_selectedItem != null)
                {
                    _item.Sync(_selectedItem);

                    if (!DataContext.Tournament.EntryFee.HasValue)
                    {
                        _item.IsEntryFeePaid = true;
                        _item.PaidAmount = 0;
                    }
                    else
                    {
                        _item.PaidAmount = DataContext.Tournament.EntryFee;
                        _item.IsEntryFeePaid = false;
                    }

                    _item.IsWeightApproved = false;

                    OnPropertyChanged("Item");

                    OnPropertyChanged("IsFemaleT");
                    OnPropertyChanged("IsFemaleF");
                    OnPropertyChanged("WeightF");
                    OnPropertyChanged("BirthDateF");
                    OnPropertyChanged("Groups");

                    _selectedItem = null;
                }

                OnPropertyChanged("SelectedItem");
            }
        }

        public ObservableCollection<Wrestler> CachedAthletes
        {
            get { return _cachedAthletes; }
            set
            {
                _cachedAthletes = value;

                OnPropertyChanged("CachedAthletes");
            }
        }

        public Wrestler Item
        {
            get { return _item; }
            set
            {
                _item = value;
                OnPropertyChanged("Item");
            }
        }

        public double? WeightF
        {
            get { return _item.Weight; }
            set
            {
                _item.Weight = value;

                OnPropertyChanged("WeightF");
                OnPropertyChanged("Groups");
            }
        }

        public DateTime? BirthDateF
        {
            get { return _item.BirthDate; }
            set
            {
                _item.BirthDate = value;
                OnPropertyChanged("BirthDateF");
                OnPropertyChanged("Groups");
            }
        }

        public bool? IsFemaleF
        {
            get
            {
                return _item != null ? !_item.IsFemale : false;
            }
            set
            {
                if (_item != null && value.HasValue)
                {
                    _item.IsFemale = false;
                    OnPropertyChanged("IsFemaleT");
                }
            }
        }

        public bool? IsFemaleT
        {
            get
            {
                return _item != null ? _item.IsFemale : false;
            }
            set
            {
                if (_item != null && value.HasValue)
                {
                    _item.IsFemale = true;
                    OnPropertyChanged("IsFemaleF");
                }
            }
        }

        public AgeWeightGroup SelectedGroup
        {
            get { return _selectedGroup; }
            set
            {
                _selectedGroup = value;

                _item.GroupID = _selectedGroup?.ID;
                _item.GroupName = _selectedGroup?.Name;

                OnPropertyChanged("SelectedGroup");
            }
        }

        public List<AgeWeightGroup> Groups
        {
            get
            {
                var tmp = DataContext.Tournament.Groups.Where(
                        p => p.IsFemale == _item.IsFemale
                             && (!WeightF.HasValue || p.WeightMax >= WeightF.Value)
                             && (!BirthDateF.HasValue || p.BirthYearMin <= BirthDateF.Value.Year))
                    .OrderBy(g => g.IsFemale).ThenByDescending(p => p.BirthYearMax).ThenBy(x => x.WeightMax).ToList();

                if (SelectedGroup != null && !tmp.Contains(SelectedGroup))
                {
                    SelectedGroup = null;
                }

                if (SelectedGroup == null && tmp.Count > 0 && WeightF.HasValue && BirthDateF.HasValue)
                {
                    SelectedGroup = tmp[0];
                }

                return tmp;
            }
        }
    }
}