using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.ImageSlide
{
    public class ImageSlide : ISlideType
    {
        private readonly IDiContainer _di;

        public ImageSlide(IDiContainer di)
        {
            _di = di;
            SettingsControl = di.Resolve<ImageSlideSettingsViewModel>();
        }

        public string SlideType => "Изображение";
        public ISliderSettingsControl SettingsControl { get; }

        public ISliderViewControl CreateViewControl() => new ImageSlideViewModel(_di);
    }
}
