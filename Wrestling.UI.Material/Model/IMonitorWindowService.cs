using System.Threading.Tasks;

namespace Wrestling.UI.Material.Model
{
    // Wraps the IPanelView("ScoreScreen") + MonitorPicker.PickAsync flow that
    // MatchControlViewModel used inline. Single entry point shared by any
    // caller that needs to pop the score-screen monitor — no copy-paste of
    // the picker / TargetMonitor logic.
    public interface IMonitorWindowService
    {
        bool IsVisible { get; }

        Task ShowAsync();

        void Close();
    }
}
