using Wrestling.Entities;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.ImageSlide
{
    public class ImageSlideViewModel : TournamentViewModelBase, ISliderViewControl
    {
        private ScreenSlide _item;
        private string _imagePath;

        public ImageSlideViewModel(IDiContainer container) : base(container)
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

        public string ImagePath
        {
            get { return _imagePath; }
            set
            {
                _imagePath = value;
                OnPropertyChanged("ImagePath");
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
            
            var background = _item.GetNamedValue("ImagePath");
            if (background != null)
            {
                ImagePath = background.ToString();
            }
            else
            {
                ImagePath = DataContext.Tournament.Settings.SliderBackgroundImagePath;
            }
        }
    }
}