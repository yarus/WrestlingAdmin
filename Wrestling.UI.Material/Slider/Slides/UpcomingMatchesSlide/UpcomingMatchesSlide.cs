using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

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

        public string SlideType
        {
            get
            {
                var v = LocalizationService.Instance?.T("SlideType_Upcoming");
                return string.IsNullOrEmpty(v) || v == "SlideType_Upcoming" ? "Ближайшие Поединки" : v;
            }
        }
        public ISliderSettingsControl SettingsControl { get; }

        public ISliderViewControl CreateViewControl() => new UpcomingMatchesViewModel(_di);
    }
}
