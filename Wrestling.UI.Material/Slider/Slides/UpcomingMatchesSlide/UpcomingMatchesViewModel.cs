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
        private ObservableCollection<AgeWeightGroup> _groups;

        private int _sliderOpacityValue;
        private int _showMatchesCount;
        private string _sliderBackgroundImagePath;
        private string _matName;

        public UpcomingMatchesViewModel(IDiContainer container) : base(container)
        {
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

        public string SliderName => Item?.Title;

        public string MatName
        {
            get { return _matName; }
            set
            {
                _matName = value;
                OnPropertyChanged("MatName");
            }
        }


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

        public int ShowMatchesCount
        {
            get { return _showMatchesCount; }
            set
            {
                _showMatchesCount = value;
                OnPropertyChanged("ShowMatchesCount");
                OnPropertyChanged("UpcomingMatches");
            }
        }

        public ObservableCollection<AgeWeightGroup> Groups
        {
            get { return _groups; }
            set
            {
                _groups = value;

                OnPropertyChanged("Groups");
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
                OnPropertyChanged("SliderName");
            }
        }
       
        #endregion

        private void LoadMatches()
        {
            UpcomingMatches = new ObservableCollection<WrestlingMatch>(Groups
                .SelectMany(g => g.Bracket.Rounds)
                .SelectMany(r => r.RoundMatches)
                .Where(m => m.IsMatchCanStart)
                .OrderBy(m => m.MatchNumber).Take(ShowMatchesCount));
        }

        public void TimerTick()
        {
            
        }

        public void InitContext(ScreenSlide slide)
        {
            Item = slide;

            InitData();

            var showMatchesCount = _item.GetNamedValue("ShowMatchesCount");
            if (showMatchesCount != null)
            {
                ShowMatchesCount = Convert.ToInt32(showMatchesCount);
            }
            else
            {
                ShowMatchesCount = 4;
            }

            var matID = _item.GetNamedValue("MatID");
            if (matID != null)
            {
                var matGuid = new Guid(matID.ToString());
                var mat = DataContext.Tournament.Mats.FirstOrDefault(c => c.ID == matGuid);
                if (mat != null)
                {
                    Groups = mat.Groups;
                    MatName = mat.Name;
                }
                else
                {
                    Groups = DataContext.Tournament.Groups;
                }
            }
            else
            {
                Groups = DataContext.Tournament.Groups;
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

            LoadMatches();
        }
    }
}