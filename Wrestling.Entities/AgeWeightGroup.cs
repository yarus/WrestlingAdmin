using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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
        private string _carpetLabel;
        private Guid? _carpetId;
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

        public Guid? CarpetID
        {
            get { return _carpetId; }
            set
            {
                _carpetId = value;
                OnPropertyChanged("CarpetID");
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

        public string CarpetLabel
        {
            get { return _carpetLabel; }
            set
            {
                _carpetLabel = value;
                OnPropertyChanged("CarpetLabel");
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

        // Bumped by high-level group field edits (timing, CarpetID, name/age/
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
        // so other carpets do not lose their work.
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
                string result;

                if (BirthYearMax == BirthYearMin)
                {
                    result = BirthYearMax + " г. ";
                }
                else
                {
                    result = $"{BirthYearMin}-{BirthYearMax} гг. ";
                }

                result += WeightMax + " кг. " + IsFemaleLabel + ".";

                return result;
            }
        }

        // Name without the trailing gender suffix (e.g. "2018 г. 18 кг.").
        // Used by UIs that surface gender separately when both genders are present.
        public string NameWithoutGender
        {
            get
            {
                if (BirthYearMax == BirthYearMin)
                {
                    return BirthYearMax + " г. " + WeightMax + " кг.";
                }
                return $"{BirthYearMin}-{BirthYearMax} гг. {WeightMax} кг.";
            }
        }

        public string IsFemaleLabel => IsFemale ? "Ж" : "М";

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
            CarpetLabel = item.CarpetLabel;
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