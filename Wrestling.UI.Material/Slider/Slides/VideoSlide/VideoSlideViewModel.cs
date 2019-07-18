using Wrestling.Entities;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.VideoSlide
{
    public class VideoSlideViewModel : TournamentViewModelBase, ISliderViewControl
    {
        private ScreenSlide _item;
        private string _videoPath;

        public VideoSlideViewModel(IDiContainer container) : base(container)
        {
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

        public string VideoPath
        {
            get { return _videoPath; }
            set
            {
                _videoPath = value;
                OnPropertyChanged("VideoPath");
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
            
            var path = _item.GetNamedValue("VideoPath");
            if (path != null)
            {
                VideoPath = path.ToString();
            }
        }
    }
}