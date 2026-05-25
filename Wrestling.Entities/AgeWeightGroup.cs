using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Wrestling.Entities.Localization;

namespace Wrestling.Entities
{
    public class AgeWeightGroup : INotifyPropertyChanged, ICloneable
    {
        #region Fields

        private Guid _id;
        private bool _isFemale;
        private int? _birthYearMin;
        private int? _birthYearMax;
        private double? _weightMax;
        private int _maxRoundSecond;
        private int _maxTimeoutSecond;
        private int _maxActionSecond;
        private string _matLabel;
        private Guid? _matId;
        private Guid? _partId;
        private GroupBracket _bracket;
        private List<Wrestler> _wrestlers;
        private bool _isExpanded;
        private int _fieldsVersion;
        private int _bracketVersion;

        #endregion

        public AgeWeightGroup()
        {
            _wrestlers = new List<Wrestler>();
        }

        public Guid ID
        {
            get { return _id; }
            set
            {
                _id = value;
                OnPropertyChanged("ID");
            }
        }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                _isExpanded = value;
                OnPropertyChanged("IsExpanded");
            }
        }

        public Guid? MatID
        {
            get { return _matId; }
            set
            {
                _matId = value;
                OnPropertyChanged("MatID");
            }
        }

        // Which part of the tournament this group belongs to. Nullable for
        // groups that didn't make it onto the schedule (insufficient
        // wrestlers); such groups never get a PartID. Once assigned, this
        // travels with the group across peers via FieldsVersion.
        public Guid? PartID
        {
            get { return _partId; }
            set
            {
                _partId = value;
                OnPropertyChanged("PartID");
            }
        }

        public double? WeightMax
        {
            get { return _weightMax; }
            set
            {
                _weightMax = value;
                OnPropertyChanged("WeightMax");
                OnPropertyChanged("Name");
                OnPropertyChanged("NameWithoutGender");
            }
        }

        public int? BirthYearMax
        {
            get { return _birthYearMax; }
            set
            {
                _birthYearMax = value;
                OnPropertyChanged("BirthYearMax");
                OnPropertyChanged("Name");
                OnPropertyChanged("NameWithoutGender");
            }
        }

        public int? BirthYearMin
        {
            get { return _birthYearMin; }
            set
            {
                _birthYearMin = value;
                OnPropertyChanged("BirthYearMin");
                OnPropertyChanged("Name");
                OnPropertyChanged("NameWithoutGender");
            }
        }

        public bool IsFemale
        {
            get { return _isFemale; }
            set
            {
                _isFemale = value;
                OnPropertyChanged("IsFemale");
                OnPropertyChanged("Name");
            }
        }

        public int MaxRoundSecond
        {
            get { return _maxRoundSecond; }
            set
            {
                _maxRoundSecond = value;
                OnPropertyChanged("MaxRoundSecond");
            }
        }

        public string MatLabel
        {
            get { return _matLabel; }
            set
            {
                _matLabel = value;
                OnPropertyChanged("MatLabel");
            }
        }

        public int MaxTimeoutSecond
        {
            get { return _maxTimeoutSecond; }
            set
            {
                _maxTimeoutSecond = value;
                OnPropertyChanged("MaxTimeoutSecond");
            }
        }

        public int MaxActionSecond
        {
            get { return _maxActionSecond; }
            set
            {
                _maxActionSecond = value;
                OnPropertyChanged("MaxActionSecond");
            }
        }

        public GroupBracket Bracket
        {
            get { return _bracket; }
            set
            {
                _bracket = value;
                OnPropertyChanged("Bracket");
                OnPropertyChanged("IsBracketGenerated");
            }
        }

        public List<Wrestler> Wrestlers
        {
            get { return _wrestlers; }
            set
            {
                _wrestlers = value;
                OnPropertyChanged("Wrestlers");
            }
        }

        // Bumped by high-level group field edits (timing, MatID, name/age/
        // weight ranges) — anything that does NOT change bracket shape. Apply
        // copies the new field values onto the local group and cascades the
        // new timing into local pending matches without touching the bracket.
        public int FieldsVersion
        {
            get { return _fieldsVersion; }
            set
            {
                _fieldsVersion = value;
                OnPropertyChanged("FieldsVersion");
            }
        }

        // Bumped only by IGroupBracketProcessor.Generate() — i.e. the bracket
        // was rebuilt from scratch. Apply replaces the bracket and Wrestlers
        // list wholesale, then re-applies any locally-newer match completions
        // so other mats do not lose their work.
        public int BracketVersion
        {
            get { return _bracketVersion; }
            set
            {
                _bracketVersion = value;
                OnPropertyChanged("BracketVersion");
            }
        }

        #region Readonly Properties

        public int PendingMatchesCount => _bracket?.Rounds.SelectMany(r => r.RoundMatches).Count(m => m.Status == MatchStatusEnum.Pending) ?? 0;

        public bool IsGroupValid => Wrestlers.Count > 0 && Wrestlers.FirstOrDefault(w => !w.IsApplicationValid || !w.IsRegistrationApproved) == null;

        public int WrestlersApprovedCount => Wrestlers.Count(x => x.IsRegistrationApproved);

        public void RefreshState()
        {
            OnPropertyChanged("Wrestlers");
            OnPropertyChanged("Bracket");
            OnPropertyChanged("IsGroupValid");
            OnPropertyChanged("IsBracketGenerated");
            OnPropertyChanged("IsBracketCompleted");
        }

        public bool IsBracketGenerated => Bracket != null;

        public bool IsBracketCompleted => Bracket != null
            && !Bracket.Rounds.SelectMany(r => r.RoundMatches).Any(m => m.Status == MatchStatusEnum.Pending);
        public string Name
        {
            get
            {
                var yearSingle = EntityLocalization.T("Group_YearSuffix", "г.");
                var yearRange = EntityLocalization.T("Group_YearRangeSuffix", "гг.");
                var weight = EntityLocalization.T("Group_WeightSuffix", "кг.");

                string result;
                if (BirthYearMax == BirthYearMin)
                {
                    result = BirthYearMax + " " + yearSingle + " ";
                }
                else
                {
                    result = string.Format("{0}-{1} {2} ", BirthYearMin, BirthYearMax, yearRange);
                }

                result += WeightMax + " " + weight + " " + IsFemaleLabel + ".";

                return result;
            }
        }

        // Name without the trailing gender suffix (e.g. "2018 г. 18 кг.").
        // Used by UIs that surface gender separately when both genders are present.
        public string NameWithoutGender
        {
            get
            {
                var yearSingle = EntityLocalization.T("Group_YearSuffix", "г.");
                var yearRange = EntityLocalization.T("Group_YearRangeSuffix", "гг.");
                var weight = EntityLocalization.T("Group_WeightSuffix", "кг.");

                if (BirthYearMax == BirthYearMin)
                {
                    return BirthYearMax + " " + yearSingle + " " + WeightMax + " " + weight;
                }
                return string.Format("{0}-{1} {2} {3} {4}", BirthYearMin, BirthYearMax, yearRange, WeightMax, weight);
            }
        }

        public string IsFemaleLabel => IsFemale
            ? EntityLocalization.T("Gender_FemaleShort", "Ж")
            : EntityLocalization.T("Gender_MaleShort", "М");

        #endregion
        
        public object Clone()
        {
            var tmp = new AgeWeightGroup();
            tmp.Sync(this);
            return tmp;
        }

        public void Sync(AgeWeightGroup item)
        {
            if (item == null) return;
            ID = item.ID;
            Bracket = item.Bracket;
            MatLabel = item.MatLabel;
            MaxActionSecond = item.MaxActionSecond;
            MaxRoundSecond = item.MaxRoundSecond;
            MaxTimeoutSecond = item.MaxTimeoutSecond;
            Wrestlers = item.Wrestlers;

            BirthYearMax = item.BirthYearMax;
            BirthYearMin = item.BirthYearMin;
            IsFemale = item.IsFemale;
            WeightMax = item.WeightMax;
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}