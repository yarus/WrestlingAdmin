using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Wrestling.Entities
{
    public class TeamApplication : INotifyPropertyChanged, ICloneable
    {
        private Guid _id;
        private string _fullName;
        private string _shortName;
        private string _mainCoach;
        private string _representative;
        private string _country;
        private string _city;
        private string _fullAddress;
        private string _phoneNumber;
        private string _email;
        private string _emblemPath;
        private string _hashTag;

        private ObservableCollection<Wrestler> _wrestlers;

        public TeamApplication()
        {
            _wrestlers = new ObservableCollection<Wrestler>();
        }

        public Guid ID
        {
            get { return _id; }
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        public string HashTag
        {
            get { return _hashTag; }
            set
            {
                _hashTag = value;
                OnPropertyChanged();
            }
        }

        public string FullName
        {
            get { return _fullName; }
            set
            {
                _fullName = value;
                OnPropertyChanged();
            }
        }

        public string ShortName
        {
            get { return _shortName; }
            set
            {
                _shortName = value;
                OnPropertyChanged();
            }
        }

        public string MainCoach
        {
            get { return _mainCoach; }
            set
            {
                _mainCoach = value;
                OnPropertyChanged();
            }
        }

        public string Representative
        {
            get { return _representative; }
            set
            {
                _representative = value;
                OnPropertyChanged();
            }
        }

        public string Country
        {
            get { return _country; }
            set
            {
                _country = value;
                OnPropertyChanged();
            }
        }

        public string City
        {
            get { return _city; }
            set
            {
                _city = value;
                OnPropertyChanged();
            }
        }

        public string FullAddress
        {
            get { return _fullAddress; }
            set
            {
                _fullAddress = value;
                OnPropertyChanged();
            }
        }

        public string PhoneNumber
        {
            get { return _phoneNumber; }
            set
            {
                _phoneNumber = value;
                OnPropertyChanged();
            }
        }

        public string Email
        {
            get { return _email; }
            set
            {
                _email = value;
                OnPropertyChanged();
            }
        }

        public string EmblemPath
        {
            get { return _emblemPath; }
            set
            {
                _emblemPath = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Wrestler> Wrestlers
        {
            get { return _wrestlers; }
            set
            {
                _wrestlers = value;
                OnPropertyChanged();
                OnPropertyChanged("IsApplicationValid");
            }
        }

        public bool IsApplicationValid => Wrestlers.Count > 0 && Wrestlers.FirstOrDefault(w => !w.IsApplicationValid) == null;

        public void RefreshStats()
        {
            OnPropertyChanged("IsApplicationValid");
            OnPropertyChanged("Wrestlers");
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion    }

        public void Sync(TeamApplication app)
        {
            ID = app.ID;
            City = app.City;
            Country = app.Country;
            Email = app.Email;
            EmblemPath = app.EmblemPath;
            FullAddress = app.FullAddress;
            FullName = app.FullName;
            HashTag = app.HashTag;
            MainCoach = app.MainCoach;
            PhoneNumber = app.PhoneNumber;
            Representative = app.Representative;
            ShortName = app.ShortName;
            Wrestlers = new ObservableCollection<Wrestler>(app.Wrestlers);
        }

        public object Clone()
        {
            var tmp = new TeamApplication();
            tmp.Sync(this);
            return tmp;
        }
    }
}
