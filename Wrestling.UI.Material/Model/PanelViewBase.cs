using System;
using System.Windows;

namespace Wrestling.UI.Material.Model
{
    public class PanelViewBase : ChildWindowBase
    {
        // Set by the caller before ShowScreen() to direct the window to a specific
        // monitor. Null falls back to "prefer secondary monitor if attached".
        public System.Windows.Forms.Screen TargetMonitor { get; set; }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        protected override void AdjustScreenOnShow()
        {
            base.AdjustScreenOnShow();

            var monitor = TargetMonitor ?? PickDefaultMonitor();

            // Screen.Bounds is in GDI physical pixels; WPF Top/Left/Width/Height
            // are device-independent pixels (1/96 inch). Without DPI conversion
            // the window ends up smaller than the monitor on high-DPI displays
            // (visible gaps on all sides). Bounds (not WorkingArea) so the
            // window also covers the taskbar on the target screen.
            var bounds = monitor.Bounds;
            var dpi = GetMainWindowDpi();

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowState = WindowState.Normal;

            Left = bounds.Left / dpi.X;
            Top = bounds.Top / dpi.Y;
            Width = bounds.Width / dpi.X;
            Height = bounds.Height / dpi.Y;
        }

        private struct DpiScale
        {
            public double X;
            public double Y;
        }

        private static DpiScale GetMainWindowDpi()
        {
            var main = Application.Current?.MainWindow;
            if (main != null)
            {
                var src = PresentationSource.FromVisual(main);
                if (src?.CompositionTarget != null)
                {
                    return new DpiScale
                    {
                        X = src.CompositionTarget.TransformToDevice.M11,
                        Y = src.CompositionTarget.TransformToDevice.M22
                    };
                }
            }

            return new DpiScale { X = 1.0, Y = 1.0 };
        }

        private static System.Windows.Forms.Screen PickDefaultMonitor()
        {
            var screens = System.Windows.Forms.Screen.AllScreens;

            var monitor = screens.Length > 1 ? screens[1] : screens[0];

            if (Equals(System.Windows.Forms.Screen.PrimaryScreen, monitor) && screens.Length > 1)
            {
                monitor = screens[0];
            }

            return monitor;
        }
    }
}
