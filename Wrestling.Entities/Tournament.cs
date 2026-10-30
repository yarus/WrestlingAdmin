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

        private ObservableCollection<SlideChannel> _slideChannels;

        private ObservableCollection<AgeWeightGroup> _groups;

        private ObservableCollection<TeamApplication> _applications;

        private ObservableCollection<Wrestler> _wrestlers;

        private ObservableCollection<Mat> _mats;

        private ObservableCollection<TournamentPart> _parts;

        private int _metaVersion;

        public Tournament(GlobalSettings settings)
        {
            _groups = new ObservableCollection<AgeWeightGroup>();
            _applications = new ObservableCollection<TeamApplication>();
            _wrestlers = new ObservableCollection<Wrestler>();
            _mats = new ObservableCollection<Mat>();
            _slideChannels = new ObservableCollection<SlideChannel>();
            _parts = new ObservableCollection<TournamentPart>();

            Settings = settings ?? new GlobalSettings();
        }

        public int AppliedWrestlersCount => Wrestlers.Count;
        public int ApprovedWrestlersCount => Groups.Sum(g => g.WrestlersApprovedCount);
        public int GroupsCount => Groups.Count;
        public int MatsCount => Mats.Count;
        public int MatchesCount => Groups.Where(g => g.Bracket != null).Sum(g => g.Bracket.MatchesCount);
        public int CompletedMatchesCount => Groups.Where(g => g.Bracket != null).Sum(g => g.Bracket.CompletedMatchesCount);
        public int PendingMatchesCount => MatchesCount - CompletedMatchesCount;
        public int ApplicationsCount => TeamApplications.Count;
        public int ProgressPercent => MatchesCount == 0 ? 0 : CompletedMatchesCount * 100 / MatchesCount;

        // Tournament-wide ETA: the largest per-mat pending workload, sized
        // by theoretical per-match time (round × 2 + timeout). Conservative
        // upper bound that assumes mats run in parallel — the slowest mat
        // bounds the whole tournament. Operators read this as «when the
        // last running mat will finish».
        public int ExpectedDurationInSeconds
        {
            get
            {
                if (PendingMatchesCount == 0) return 0;

                int maxDurationInSeconds = 0;
                foreach (var mat in _mats)
                {
                    var matDurationInSeconds = 0;
                    foreach (var group in mat.Groups)
                    {
                        if (group?.Bracket == null) continue;
                        matDurationInSeconds += group.PendingMatchesCount
                            * (group.MaxRoundSecond * 2 + group.MaxTimeoutSecond);
                    }
                    if (matDurationInSeconds > maxDurationInSeconds)
                    {
                        maxDurationInSeconds = matDurationInSeconds;
                    }
                }
                return maxDurationInSeconds;
            }
        }

        public decimal? FeesCollectedAmount => Wrestlers.Where(w => w.IsEntryFeePaid).Sum(w => w.PaidAmount);

        public bool IsStandingCompleted => Status == TournamentStatus.InProgress;

        public GlobalSettings Settings { get; set; }

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

        public ObservableCollection<SlideChannel> SlideChannels
        {
            get { return _slideChannels; }
            set
            {
                _slideChannels = value;
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

        public ObservableCollection<Mat> Mats
        {
            get { return _mats; }
            set
            {
                _mats = value;
                OnPropertyChanged();
            }
        }

        // Operational sequence: groups belong to one part each; mats run
        // parts independently via Mat.ActivePartID. Always contains at least
        // one part — the adapter creates a default one on legacy load.
        public ObservableCollection<TournamentPart> Parts
        {
            get { return _parts; }
            set
            {
                _parts = value;
                OnPropertyChanged();
            }
        }

        // Bumped on any tournament-level meta change: parts list edits
        // (create / rename / delete). Peers apply via the existing
        // remote > local rule, mirroring AgeWeightGroup.FieldsVersion.
        public int MetaVersion
        {
            get { return _metaVersion; }
            set
            {
                _metaVersion = value;
                OnPropertyChanged();
            }
        }

        public Guid? ID { get; set; }

        public bool AddWrestlerRegistration(Wrestler wrestler)
        {
            Wrestlers.Add(wrestler);
            var group = Groups.FirstOrDefault(g => g.ID == wrestler.GroupID);
            wrestler.GroupName = group?.Name ?? string.Empty;
            if (group == null) return false;

            if (wrestler.IsRegistrationApproved)
                group.Wrestlers.Add(wrestler);

            // A new registration invalidates the draw even before approval.
            group.Bracket = null;
            group.BracketVersion++;
            wrestler.FinalPlace = null;
            foreach (var member in group.Wrestlers) member.FinalPlace = null;
            group.RefreshState();
            Mats.FirstOrDefault(m => m.ID == group.MatID)?.RefreshStats();
            RefreshAggregates();
            return true;
        }

        public bool UpdateWrestlerRegistration(Wrestler wrestler, Wrestler edited)
        {
            var groupChanged = wrestler.GroupID != edited.GroupID;
            if (!groupChanged)
            {
                wrestler.Sync(edited);
                return false;
            }

            var source = Groups.FirstOrDefault(g => g.ID == wrestler.GroupID);
            var destination = Groups.FirstOrDefault(g => g.ID == edited.GroupID);
            source?.Wrestlers.RemoveAll(w => w.ID == wrestler.ID);
            destination?.Wrestlers.RemoveAll(w => w.ID == wrestler.ID);

            wrestler.Sync(edited);
            wrestler.SeedNumber = null;
            wrestler.IsSeedFixed = false;
            wrestler.FinalPlace = null;
            wrestler.GroupName = destination?.Name ?? string.Empty;

            if (destination != null && wrestler.IsRegistrationApproved)
            {
                // Groups and tournament registrations must share the live object,
                // not the temporary copy used by the edit dialog.
                destination.Wrestlers.Add(wrestler);
            }

            foreach (var group in new[] { source, destination }.Where(g => g != null))
            {
                // Invalidate even when the entrant is still awaiting approval.
                // The newer version also clears the old bracket on sync peers.
                group.Bracket = null;
                group.BracketVersion++;
                foreach (var member in group.Wrestlers) member.FinalPlace = null;
                group.RefreshState();
                Mats.FirstOrDefault(m => m.ID == group.MatID)?.RefreshStats();
            }

            RefreshAggregates();
            return true;
        }

        public void RefreshAggregates()
        {
            OnPropertyChanged(nameof(MatchesCount));
            OnPropertyChanged(nameof(CompletedMatchesCount));
            OnPropertyChanged(nameof(PendingMatchesCount));
            OnPropertyChanged(nameof(ProgressPercent));
            OnPropertyChanged(nameof(ExpectedDurationInSeconds));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
