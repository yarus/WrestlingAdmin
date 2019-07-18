using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.VideoSlide
{
    public class VideoSlide : ISlideType
    {
        public VideoSlide(IDiContainer di)
        {
            SettingsControl = di.Resolve<VideoSlideSettingsViewModel>();
            ViewControl = di.Resolve<VideoSlideViewModel>();
        }

        public string SlideType => "Видео";
        public ISliderSettingsControl SettingsControl { get; }
        public ISliderViewControl ViewControl { get; }
    }
}