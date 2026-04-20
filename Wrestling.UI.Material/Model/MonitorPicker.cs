using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Model
{
    public class MonitorOption : ObservableObject
    {
        public System.Windows.Forms.Screen Screen { get; set; }
        public string DisplayName { get; set; }
    }

    public class MonitorPickerViewModel : ObservableObject
    {
        private readonly ObservableCollection<MonitorOption> _monitors = new ObservableCollection<MonitorOption>();

        public ObservableCollection<MonitorOption> Monitors => _monitors;

        public void LoadMonitors()
        {
            _monitors.Clear();

            var screens = System.Windows.Forms.Screen.AllScreens;
            var primary = System.Windows.Forms.Screen.PrimaryScreen;

            for (int i = 0; i < screens.Length; i++)
            {
                var s = screens[i];
                var bounds = s.Bounds;
                var label = $"Монитор {i + 1} — {bounds.Width}×{bounds.Height}";
                if (Equals(primary, s))
                {
                    label += " (основной)";
                }

                _monitors.Add(new MonitorOption
                {
                    Screen = s,
                    DisplayName = label
                });
            }
        }
    }

    public static class MonitorPicker
    {
        // Prompts the user to pick a monitor. When only one monitor is attached,
        // returns it directly without showing a dialog. Returns null if the user
        // cancels.
        public static async Task<System.Windows.Forms.Screen> PickAsync()
        {
            var screens = System.Windows.Forms.Screen.AllScreens;

            if (screens.Length == 0) return null;
            if (screens.Length == 1) return screens[0];

            var vm = new MonitorPickerViewModel();
            vm.LoadMonitors();

            var dialog = new MonitorPickerDialog
            {
                DataContext = vm
            };

            var result = await DialogHost.Show(dialog, "RootDialog");

            return (result as MonitorOption)?.Screen;
        }
    }
}
