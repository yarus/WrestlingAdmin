using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Slider.Slides.GroupBracketSlide
{
    public class GroupBracketSlide : ISlideType
    {
        private readonly IDiContainer _di;

        public GroupBracketSlide(IDiContainer di)
        {
            _di = di;
            SettingsControl = di.Resolve<GroupBracketSlideSettingsViewModel>();
        }

        public string SlideType
        {
            get
            {
                var v = LocalizationService.Instance?.T("SlideType_GroupBracket");
                return string.IsNullOrEmpty(v) || v == "SlideType_GroupBracket" ? "Сетка группы" : v;
            }
        }
        public ISliderSettingsControl SettingsControl { get; }

        public ISliderViewControl CreateViewControl() => new GroupBracketViewModel(_di);
    }
}
