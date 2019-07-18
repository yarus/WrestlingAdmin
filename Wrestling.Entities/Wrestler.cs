using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wrestling.Entities
{
    public class Wrestler : INotifyPropertyChanged, ICloneable
    {
        private Guid _id;
        private Guid? _teamId;
        private string _hashTag;
        private string _teamName;
        private Guid? _groupId;
        private string _groupName;
        private string _firstName;
        private string _lastName;
        private string _middleName;
        private DateTime? _birthDate;
        private double? _weight;
        private int? _finalPlace;
        private bool _isSeedFixed;
        private int? _seedNumber;
        private bool _isFemale;
        private bool _isEntryFeePaid;
        private decimal? _paidAmount;
        private bool _isWeightApproved;
        private string _level;
        public bool IsApplicationValid => !string.IsNullOrEmpty(LastName) && !string.IsNullOrEmpty(FirstName) && BirthDate.HasValue && GroupID.HasValue;
        public bool IsRegistrationApproved => IsApplicationValid && Weight.HasValue && IsEntryFeePaid && IsWeightApproved;

        public string IsFemaleLabel => IsFemale ? "Ж" : "М";

        public Guid ID
        {
            get { return _id; }
            set
            {
                _id = value;
                OnPropertyChanged("ID");
            }
        }

        public string Level
        {
            get { return _level; }
            set
            {
                _level = value;
                OnPropertyChanged("Level");
            }
        }

        public decimal? PaidAmount
        {
            get { return _paidAmount; }
            set
            {
                _paidAmount = value;
                OnPropertyChanged("PaidAmount");
            }
        }

        public string HashTag
        {
            get { return _hashTag; }
            set
            {
                _hashTag = value;
                OnPropertyChanged("HashTag");
            }
        }

        public string TeamName
        {
            get { return _teamName; }
            set
            {
                _teamName = value;
                OnPropertyChanged("TeamName");
            }
        }

        public string GroupName
        {
            get { return _groupName; }
            set
            {
                _groupName = value;
                OnPropertyChanged("GroupName");
            }
        }

        public bool IsEntryFeePaid
        {
            get { return _isEntryFeePaid; }
            set
            {
                _isEntryFeePaid = value;
                OnPropertyChanged("IsEntryFeePaid");
                OnPropertyChanged("IsRegistrationApproved");
            }
        }

        public bool IsWeightApproved
        {
            get { return _isWeightApproved; }
            set
            {
                _isWeightApproved = value;
                OnPropertyChanged("IsWeightApproved");
                OnPropertyChanged("IsRegistrationApproved");
            }
        }

        public Guid? TeamID
        {
            get { return _teamId; }
            set
            {
                _teamId = value;
                OnPropertyChanged("TeamID");
            }
        }

        public bool IsSeedFixed
        {
            get { return _isSeedFixed; }
            set
            {
                _isSeedFixed = value;
                OnPropertyChanged("IsSeedFixed");
            }
        }

        public bool IsFemale
        {
            get { return _isFemale; }
            set
            {
                _isFemale = value;
                OnPropertyChanged("IsFemale");
            }
        }


        public int? SeedNumber
        {
            get { return _seedNumber; }
            set
            {
                _seedNumber = value;
                OnPropertyChanged("SeedNumber");
            }
        }

        public DateTime? BirthDate
        {
            get { return _birthDate; }
            set
            {
                _birthDate = value;
                OnPropertyChanged("BirthDate");
                OnPropertyChanged("IsApplicationValid");
            }
        }

        public string MiddleName
        {
            get { return _middleName; }
            set
            {
                _middleName = value;
                OnPropertyChanged("MiddleName");
                RefreshStats();
            }
        }

        public string LastName
        {
            get { return _lastName; }
            set
            {
                _lastName = value;
                OnPropertyChanged("LastName");
                OnPropertyChanged("IsApplicationValid");
                RefreshStats();
            }
        }

        public string FirstName
        {
            get { return _firstName; }
            set
            {
                _firstName = value;
                OnPropertyChanged("FirstName");
                OnPropertyChanged("IsApplicationValid");
                RefreshStats();
            }
        }

        public Guid? GroupID
        {
            get { return _groupId; }
            set
            {
                _groupId = value;
                OnPropertyChanged("GroupID");
                OnPropertyChanged("IsApplicationValid");
                OnPropertyChanged("IsRegistrationApproved");
            }
        }
        
        public double? Weight
        {
            get { return _weight; }
            set
            {
                _weight = value;
                OnPropertyChanged("Weight");
                OnPropertyChanged("IsRegistrationApproved");
            }
        }

        public int? FinalPlace
        {
            get { return _finalPlace; }
            set
            {
                _finalPlace = value;
                OnPropertyChanged("FinalPlace");
            }
        }

        public string FullName => string.Format("{0}{1}{2}", !string.IsNullOrEmpty(LastName) ? LastName : string.Empty,
            !string.IsNullOrEmpty(FirstName) ? " " + FirstName : string.Empty,
            !string.IsNullOrEmpty(MiddleName) ? " " + MiddleName : string.Empty);

        public string FullNameShort => string.Format("{0}{1}{2}", !string.IsNullOrEmpty(LastName) ? LastName : string.Empty,
            !string.IsNullOrEmpty(FirstName) ? " " + FirstName[0] + "." : string.Empty,
            !string.IsNullOrEmpty(MiddleName) ? " " + MiddleName[0] + "." : string.Empty);

        public string LastFirstName => $"{LastName}{(!string.IsNullOrEmpty(FirstName) ? $" {FirstName}" : string.Empty)}";

        public string LastFirstNameShort => $"{LastName}{(!string.IsNullOrEmpty(FirstName) ? $" {FirstName[0]}." : string.Empty)}";

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Sync(Wrestler wr)
        {
            ID = wr.ID;
            PaidAmount = wr.PaidAmount;
            FinalPlace = wr.FinalPlace;
            LastName = wr.LastName;
            BirthDate = wr.BirthDate;
            FirstName = wr.FirstName;
            MiddleName = wr.MiddleName;
            Weight = wr.Weight;
            IsFemale = wr.IsFemale;
            IsSeedFixed = wr.IsSeedFixed;
            SeedNumber = wr.SeedNumber;
            GroupID = wr.GroupID;
            GroupName = wr.GroupName;
            IsEntryFeePaid = wr.IsEntryFeePaid;
            TeamID = wr.TeamID;
            TeamName = wr.TeamName;
            HashTag = wr.HashTag;
            Level = wr.Level;
            IsWeightApproved = wr.IsWeightApproved;
        }

        public object Clone()
        {
            var tmp = new Wrestler();
            tmp.Sync(this);
            return tmp;
        }

        public void RefreshStats()
        {
            OnPropertyChanged("FullName");
            OnPropertyChanged("FullNameShort");
            OnPropertyChanged("LastFirstName");
            OnPropertyChanged("LastFirstNameShort");
        }
    }
}