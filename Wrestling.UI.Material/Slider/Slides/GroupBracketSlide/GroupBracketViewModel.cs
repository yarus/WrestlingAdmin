using System;
using System.Collections.ObjectModel;
using System.Linq;
using Wrestling.Entities;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.GroupBracketSlide
{
    public class GroupBracketViewModel : TournamentViewModelBase, ISliderViewControl
    {
        private ScreenSlide _item;
        private DateTime _currentTime;
        private AgeWeightGroup _selectedGroup;
        private ObservableCollection<AgeWeightGroup> _groups;
        private ObservableCollection<GroupRound> _groupRounds;

        private int _sliderOpacityValue;
        private string _sliderBackgroundImagePath;

        public GroupBracketViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();
            
            _groups = Tournament.Groups;
        }

        public ScreenSlide Item
        {
            get { return _item; }
            set
            {
                _item = value;
                OnPropertyChanged("Item");
            }
        }


        #region Binding Properties

        public string SliderBackgroundImagePath
        {
            get { return _sliderBackgroundImagePath; }
            set
            {
                _sliderBackgroundImagePath = value;
                OnPropertyChanged("SliderBackgroundImagePath");
            }
        }

        public double SliderOpacity => (double)_sliderOpacityValue / 100;

        public int SliderOpacityValue
        {
            get { return _sliderOpacityValue; }
            set
            {
                _sliderOpacityValue = value;
                OnPropertyChanged("SliderOpacity");
                OnPropertyChanged("SliderOpacityValue");
            }
        }

        public DateTime CurrentTime
        {
            get { return _currentTime; }
            set
            {
                _currentTime = value;
                OnPropertyChanged("CurrentTime");
            }
        }

        public ObservableCollection<GroupRound> GroupRounds
        {
            get { return _groupRounds; }
            set
            {
                _groupRounds = value;

                OnPropertyChanged("GroupRounds");
            }
        }

        public AgeWeightGroup SelectedGroup
        {
            get { return _selectedGroup; }
            set
            {
                if (_selectedGroup != value && value != null)
                {
                    LoadDataForGroup(value);
                }

                _selectedGroup = value;

                OnPropertyChanged("SelectedGroup");
            }
        }

        #endregion
        
        private void LoadDataForGroup(AgeWeightGroup group)
        {
            if (group?.Bracket == null)
            {
                GroupRounds = new ObservableCollection<GroupRound>();
            }
            else
            {
                GroupRounds = new ObservableCollection<GroupRound>(group.Bracket.Rounds);
            }
        }

        public void TimerTick()
        {
            CurrentTime = DateTime.Now;
        }

        public void InitContext(ScreenSlide slide)
        {
            _item = slide;

            InitData();

            CurrentTime = DateTime.Now;

            var groupID = _item?.GetNamedValue("GroupID");
            if (groupID != null)
            {
                var groupGuid = new Guid(groupID.ToString());
                SelectedGroup = _groups.FirstOrDefault(g => g.ID == groupGuid);
            }

            var opacity = _item.GetNamedValue("SliderOpacityValue");
            if (opacity != null)
            {
                SliderOpacityValue = Convert.ToInt32(opacity);
            }
            else
            {
                SliderOpacityValue = DataContext.Tournament.Settings.SliderOpacityValue;
            }

            var background = _item.GetNamedValue("SliderBackgroundImagePath");
            if (background != null)
            {
                SliderBackgroundImagePath = background.ToString();
            }
            else
            {
                SliderBackgroundImagePath = DataContext.Tournament.Settings.SliderBackgroundImagePath;
            }
        }
    }
}