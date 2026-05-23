using System.ComponentModel;
using Wrestling.UI.Material.Model;

namespace Wrestling.UI.Material.Slider
{
    public partial class SlideHostView : PanelViewBase
    {
        public SlideHostView()
        {
            InitializeComponent();
        }

        // Slider windows are multi-instance: closing the X button must really
        // close the window rather than hide it (the base PanelViewBase keeps it
        // alive for reuse, which fits a singleton but would leak here). The
        // owning SliderWindowManager listens on Closed to stop the VM's timer
        // and drop the tracked entry.
        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = false;
        }
    }
}
