namespace Wrestling.UI.Material.Slider.Slides
{
    public interface ISlideType
    {
        string SlideType { get; }
        ISliderSettingsControl SettingsControl { get; }

        // Factory: each SlideHostViewModel must own its own ISliderViewControl
        // instance. A DI-singleton would be shared across every open slider
        // window, so multiple channels open simultaneously would end up bound
        // to the same view state and show identical content.
        ISliderViewControl CreateViewControl();
    }
}