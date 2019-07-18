using Wrestling.Entities;

namespace Wrestling.UI.Material.Slider.Slides
{
    public interface ISliderViewControl
    {
        void TimerTick();
        void InitContext(ScreenSlide slide);
    }
}
