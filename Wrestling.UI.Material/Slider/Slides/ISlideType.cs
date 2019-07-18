namespace Wrestling.UI.Material.Slider.Slides
{
    public interface ISlideType
    {
        string SlideType { get; }
        ISliderSettingsControl SettingsControl { get; }
        ISliderViewControl ViewControl { get; }
    }
}