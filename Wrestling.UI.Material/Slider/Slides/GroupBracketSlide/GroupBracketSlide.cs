using Wrestling.UI.Utils;

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

        public string SlideType => "Сетка группы";
        public ISliderSettingsControl SettingsControl { get; }

        public ISliderViewControl CreateViewControl() => new GroupBracketViewModel(_di);
    }
}
