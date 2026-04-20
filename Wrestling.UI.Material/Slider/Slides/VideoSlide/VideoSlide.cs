using Wrestling.UI.Utils;

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

        public string SlideType => "Видео";
        public ISliderSettingsControl SettingsControl { get; }

        public ISliderViewControl CreateViewControl() => new VideoSlideViewModel(_di);
    }
}
