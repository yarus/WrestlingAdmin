using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
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
        private ObservableCollection<WrestlerLevelEnum> _levels;
        private string _searchText = string.Empty;
        private bool _isAthleteSearchOpen;
        private ICommand _selectAthleteCommand;

        public AddWrestlerViewModel(IDiContainer container, Wrestler item) : base(container)
        {
            _item = item;
        }

        public override void InitData()
        {
            base.InitData();

            if (DataContext.Tournament == null)
            {
                throw new InvalidOperationException("Tournament is not set on the data context. Navigate to a tournament before opening this view.");
            }

            // Order in the picker (highest rank first) mirrors LevelNormalizer's
            // ordering. "None" is omitted intentionally — the textbox stays
            // unselected for "no rank" rather than offering an explicit empty
            // option, matching the legacy free-form-string behavior.
            _levels = new ObservableCollection<WrestlerLevelEnum>()
            {
                WrestlerLevelEnum.MSMK,
                WrestlerLevelEnum.MS,
                WrestlerLevelEnum.KMS,
                WrestlerLevelEnum.Adult1,
                WrestlerLevelEnum.Adult2,
                WrestlerLevelEnum.Adult3,
                WrestlerLevelEnum.Junior1,
                WrestlerLevelEnum.Junior2,
                WrestlerLevelEnum.Junior3,
            };
            
            _groups = DataContext.Tournament.Groups;

            _selectedGroup = _item.GroupID.HasValue ? _groups.FirstOrDefault(g => g.ID == _item.GroupID) : null;

            CachedAthletes = new ObservableCollection<Wrestler>(DataContext.WrestlersCache);
            SearchText = string.Empty;
            SelectedItem = null;
            IsAthleteSearchOpen = false;
        }

        public ObservableCollection<WrestlerLevelEnum> Levels => _levels;

        public string SearchText
        {
            get { return _searchText; }
            set
            {
                _searchText = value ?? string.Empty;
                SelectedItem = null;
                OnPropertyChanged(nameof(SearchText));
                OnPropertyChanged(nameof(FilteredAthletes));
                IsAthleteSearchOpen = FilteredAthletes.Count > 0;
            }
        }

        public List<Wrestler> FilteredAthletes
        {
            get
            {
                var text = SearchText.Trim();
                if (text.Length < 3 || CachedAthletes == null) return new List<Wrestler>();
                return CachedAthletes.Where(w =>
                    w.FullName.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrEmpty(w.HashTag) && w.HashTag.Contains(text, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
        }

        public bool IsAthleteSearchOpen
        {
            get { return _isAthleteSearchOpen; }
            set
            {
                _isAthleteSearchOpen = value;
                OnPropertyChanged(nameof(IsAthleteSearchOpen));
            }
        }

        public Wrestler SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                _selectedItem = value;
                OnPropertyChanged(nameof(SelectedItem));
            }
        }

        public ICommand SelectAthleteCommand => _selectAthleteCommand ??= new RelayCommand(
            param => SelectAthlete(param as Wrestler ?? SelectedItem),
            param => (param as Wrestler ?? SelectedItem) != null);

        private void SelectAthlete(Wrestler athlete)
        {
            if (athlete == null) return;

            _item.Sync(athlete);

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

            SearchText = string.Empty;
            IsAthleteSearchOpen = false;
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
