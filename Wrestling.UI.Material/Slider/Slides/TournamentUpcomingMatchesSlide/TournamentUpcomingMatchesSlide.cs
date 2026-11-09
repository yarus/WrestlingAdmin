using Wrestling.UI.Utils;
using Wrestling.UI.Material.Tournament;

namespace Wrestling.UI.Material.Slider.Slides.TournamentUpcomingMatchesSlide
{
    public sealed class TournamentUpcomingMatchesSlide : ISlideType
    {
        private readonly IDiContainer _di;

        public TournamentUpcomingMatchesSlide(IDiContainer di)
        {
            _di = di;
            SettingsControl = new TournamentUpcomingMatchesSettingsViewModel(di);
        }

        internal static string GetTitle() => TournamentViewModelBase.T(
            "SlideType_TournamentUpcomingCards", "Ближайшие схватки турнира");

        public string SlideType => GetTitle();
        public ISliderSettingsControl SettingsControl { get; }
        public ISliderViewControl CreateViewControl() => new TournamentUpcomingMatchesViewModel(_di);
    }
}
