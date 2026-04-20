using Wrestling.Entities;

namespace Wrestling.UI.Material.Slider
{
    public interface ISliderWindowManager
    {
        int OpenCount { get; }

        void OpenOnMonitor(SlideChannel channel, System.Windows.Forms.Screen monitor);

        int CountForChannel(SlideChannel channel);

        // Returns the SlideHostViewModel driving the open window for this
        // channel, or null if no window is open. Used by the SliderControl
        // detail pane to let its timer toggle + ListView reflect the live
        // monitor window instead of a separate preview VM.
        SlideHostViewModel GetViewModelForChannel(SlideChannel channel);

        void RefreshChannel(SlideChannel channel);

        void CloseChannel(SlideChannel channel);

        void CloseAll();
    }
}
