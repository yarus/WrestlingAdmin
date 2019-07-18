using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.UpcomingMatchesSlide
{
    public class UpcomingMatchesSlide : ISlideType
    {
        public UpcomingMatchesSlide(IDiContainer di)
        {
            SettingsControl = di.Resolve<UpcomingMatchesSlideSettingsViewModel>();
            ViewControl = di.Resolve<UpcomingMatchesViewModel>();
        }

        public string SlideType => "Ближайшие Поединки";
        public ISliderSettingsControl SettingsControl { get; }
        public ISliderViewControl ViewControl { get; }
    }
}