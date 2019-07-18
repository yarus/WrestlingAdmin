using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.GroupBracketSlide
{
    public class GroupBracketSlide : ISlideType
    {
        public GroupBracketSlide(IDiContainer di)
        {
            SettingsControl = di.Resolve<GroupBracketSlideSettingsViewModel>();
            ViewControl = di.Resolve<GroupBracketViewModel>();
        }

        public string SlideType => "Сетка группы";
        public ISliderSettingsControl SettingsControl { get; }
        public ISliderViewControl ViewControl { get; }
    }
}