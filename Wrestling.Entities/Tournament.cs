using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Wrestling.Entities
{
    public class Tournament : INotifyPropertyChanged
    {
        private string _hashTag;
        private string _country;
        private string _city;
        private string _address;
        private string _name;
        private DateTime? _startDate;
        private TournamentStatus _status;
        private string _fileName;
        private string _mainJudge;
        private string _mainJudgeEmail;
        private string _mainJudgePhone;
        private string _mainSecretary;
        private string _mainSecretaryEmail;
        private string _mainSecretaryPhone;
        private decimal? _entryFee;

        private ObservableCollection<ScreenSlide> _slides;

        private ObservableCollection<AgeWeightGroup> _groups;

        private ObservableCollection<TeamApplication> _applications;

        private ObservableCollection<Wrestler> _wrestlers;

        private ObservableCollection<Carpet> _carpets;

        private ObservableCollection<string> _importSources;

        public Tournament(GlobalSettings settings)
        {
            _groups = new ObservableCollection<AgeWeightGroup>();
            _applications = new ObservableCollection<TeamApplication>();
            _wrestlers = new ObservableCollection<Wrestler>();
            _carpets = new ObservableCollection<Carpet>();
            _slides = new ObservableCollection<ScreenSlide>();
            _importSources = new ObservableCollection<string>();

            Settings = settings ?? new GlobalSettings();
        }

        public int AppliedWrestlersCount => Wrestlers.Count;
        public int ApprovedWrestlersCount => Groups.Sum(g => g.WrestlersApprovedCount);
        public int GroupsCount => Groups.Count;
        public int CarpetsCount => Carpets.Count;
        public int MatchesCount => Groups.Where(g => g.Bracket != null).Sum(g => g.Bracket.MatchesCount);
        public int CompletedMatchesCount => Groups.Where(g => g.Bracket != null).Sum(g => g.Bracket.CompletedMatchesCount);
        public int PendingMatchesCount => MatchesCount - CompletedMatchesCount;
        public int ApplicationsCount => TeamApplications.Count;
        public int ProgressPercent => MatchesCount == 0 ? 0 : CompletedMatchesCount * 100 / MatchesCount;

        public int ExpectedDurationInSeconds { 
            get
            {
                if (PendingMatchesCount == 0)
                {
                    return 0;
                }

                int maxDurationInSeconds = 0;

                foreach (var carpet in _carpets)
                {
                    var carpetMaxDurationInSeconds = 0;
                    var groupsWithBrackets = carpet.Groups.Where(g => g.IsBracketGenerated).ToList();

                    foreach (var group in groupsWithBrackets)
                    {
                        var roundLength = group.MaxRoundSecond;
                        var timeoutLength = group.MaxTimeoutSecond;

                        var uncompleted = group.Bracket.MatchesCount - group.Bracket.CompletedMatchesCount;

                        var groupDuration = uncompleted * (roundLength * 2 + timeoutLength);

                        carpetMaxDurationInSeconds += groupDuration;
                    }

                    if (carpetMaxDurationInSeconds > maxDurationInSeconds)
                    {
                        maxDurationInSeconds = carpetMaxDurationInSeconds;
                    }
                }

                return maxDurationInSeconds;
            }
        }

        public decimal? FeesCollectedAmount => Wrestlers.Where(w => w.IsEntryFeePaid).Sum(w => w.PaidAmount);

        public bool IsStandingCompleted => Status == TournamentStatus.InProgress;

        public GlobalSettings Settings { get; set; }

        public ObservableCollection<string> ImportSources
        {
            get { return _importSources; }
            set
            {
                _importSources = value;
                OnPropertyChanged();
            }
        }

        public decimal? EntryFee
        {
            get { return _entryFee; }
            set
            {
                _entryFee = value;
                OnPropertyChanged();
            }
        }

        public DateTime? StartDate
        {
            get { return _startDate; }
            set
            {
                _startDate = value;
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

        public string MainJudgeEmail
        {
            get { return _mainJudgeEmail; }
            set
            {
                _mainJudgeEmail = value;
                OnPropertyChanged();
            }
        }

        public string MainJudgePhone
        {
            get { return _mainJudgePhone; }
            set
            {
                _mainJudgePhone = value;
                OnPropertyChanged();
            }
        }

        public string MainSecretaryEmail
        {
            get { return _mainSecretaryEmail; }
            set
            {
                _mainSecretaryEmail = value;
                OnPropertyChanged();
            }
        }

        public string MainSecretaryPhone
        {
            get { return _mainSecretaryPhone; }
            set
            {
                _mainSecretaryPhone = value;
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

        public string Address
        {
            get { return _address; }
            set
            {
                _address = value;
                OnPropertyChanged();
            }
        }

        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string MainJudge
        {
            get { return _mainJudge; }
            set
            {
                _mainJudge = value;
                OnPropertyChanged();
            }
        }

        public string MainSecretary
        {
            get { return _mainSecretary; }
            set
            {
                _mainSecretary = value;
                OnPropertyChanged();
            }
        }

        public string FileName
        {
            get { return _fileName; }
            set
            {
                _fileName = value;
                OnPropertyChanged();
            }
        }

        public TournamentStatus Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ScreenSlide> Slides
        {
            get { return _slides; }
            set
            {
                _slides = value;
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
                OnPropertyChanged("IsStandingCompleted");
            }
        }

        public ObservableCollection<AgeWeightGroup> Groups
        {
            get { return _groups; }
            set
            {
                _groups = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<TeamApplication> TeamApplications
        {
            get { return _applications; }
            set
            {
                _applications = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Carpet> Carpets
        {
            get { return _carpets; }
            set
            {
                _carpets = value;
                OnPropertyChanged();
            }
        }

        public Guid? ID { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}