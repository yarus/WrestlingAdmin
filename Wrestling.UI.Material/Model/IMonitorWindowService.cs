using System.Threading.Tasks;

namespace Wrestling.UI.Material.Model
{
    // Wraps the IPanelView("ScoreScreen") + MonitorPicker.PickAsync flow that
    // MatchControlViewModel used inline. Now both MatchControlViewModel and
    // CarpetSubViewModel (Phase 5 → Ковер «Монитор» quick-action) share this
    // single entry point — no copy-paste of the picker / TargetMonitor logic.
    public interface IMonitorWindowService
    {
        bool IsVisible { get; }

        Task ShowAsync();

        void Close();
    }
}
