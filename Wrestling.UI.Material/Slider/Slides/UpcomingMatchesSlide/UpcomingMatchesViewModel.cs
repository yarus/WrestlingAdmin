using System;
using System.Collections.ObjectModel;
using System.Linq;
using Wrestling.Entities;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.UpcomingMatchesSlide
{
    public class UpcomingMatchesViewModel : TournamentViewModelBase, ISliderViewControl
    {
        private ScreenSlide _item;
        private ObservableCollection<WrestlingMatch> _upcomingMatches;

        private int _sliderOpacityValue;
        private string _sliderBackgroundImagePath;

        public UpcomingMatchesViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            UpcomingMatches = new ObservableCollection<WrestlingMatch>(DataContext.Tournament.Groups
                .SelectMany(g => g.Bracket.Rounds)
                .SelectMany(r => r.RoundMatches).Where(m => m.Status == MatchStatusEnum.Pending)
                .OrderBy(m => m.MatchNumber).Take(5));
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

        public ObservableCollection<WrestlingMatch> UpcomingMatches
        {
            get { return _upcomingMatches; }
            set
            {
                _upcomingMatches = value;
                OnPropertyChanged("UpcomingMatches");
            }
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
       
        #endregion

        public void TimerTick()
        {
            
        }

        public void InitContext(ScreenSlide slide)
        {
            _item = slide;

            InitData();

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