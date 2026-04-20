using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.UpcomingMatchesSlide
{
    public class UpcomingMatchesSlide : ISlideType
    {
        private readonly IDiContainer _di;

        public UpcomingMatchesSlide(IDiContainer di)
        {
            _di = di;
            SettingsControl = di.Resolve<UpcomingMatchesSlideSettingsViewModel>();
        }

        public string SlideType => "Ближайшие Поединки";
        public ISliderSettingsControl SettingsControl { get; }

        public ISliderViewControl CreateViewControl() => new UpcomingMatchesViewModel(_di);
    }
}
