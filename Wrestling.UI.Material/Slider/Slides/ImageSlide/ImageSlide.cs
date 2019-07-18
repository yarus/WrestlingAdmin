using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.ImageSlide
{
    public class ImageSlide : ISlideType
    {
        public ImageSlide(IDiContainer di)
        {
            SettingsControl = di.Resolve<ImageSlideSettingsViewModel>();
            ViewControl = di.Resolve<ImageSlideViewModel>();
        }

        public string SlideType => "Изображение";
        public ISliderSettingsControl SettingsControl { get; }
        public ISliderViewControl ViewControl { get; }
    }
}