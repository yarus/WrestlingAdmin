using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

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

        public string SlideType
        {
            get
            {
                var v = LocalizationService.Instance?.T("SlideType_Image");
                return string.IsNullOrEmpty(v) || v == "SlideType_Image" ? "Изображение" : v;
            }
        }
        public ISliderSettingsControl SettingsControl { get; }

        public ISliderViewControl CreateViewControl() => new ImageSlideViewModel(_di);
    }
}
