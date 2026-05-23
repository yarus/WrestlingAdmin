using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Slider.Slides.VideoSlide
{
    public class VideoSlide : ISlideType
    {
        private readonly IDiContainer _di;

        public VideoSlide(IDiContainer di)
        {
            _di = di;
            SettingsControl = di.Resolve<VideoSlideSettingsViewModel>();
        }

        public string SlideType
        {
            get
            {
                var v = LocalizationService.Instance?.T("SlideType_Video");
                return string.IsNullOrEmpty(v) || v == "SlideType_Video" ? "Видео" : v;
            }
        }
        public ISliderSettingsControl SettingsControl { get; }

        public ISliderViewControl CreateViewControl() => new VideoSlideViewModel(_di);
    }
}
