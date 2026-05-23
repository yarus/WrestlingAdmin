using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

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

            var monitor = ResolveMonitor();
            if (monitor == null) return;

            var bounds = monitor.Bounds;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowState = WindowState.Normal;

            // Force HWND creation so we can position via Win32 before the window
            // becomes visible (prevents a flash on the wrong monitor).
            var hwnd = new WindowInteropHelper(this).EnsureHandle();

            if (hwnd != IntPtr.Zero)
            {
                // SetWindowPos speaks physical pixels directly — bypasses WPF's
                // per-monitor-DPI math, which produced wrong sizes when the
                // target monitor had a different DPI than the primary one
                // (e.g. 150% laptop + 100% TV). Bounds (not WorkingArea) means
                // we cover the taskbar on the target screen.
                SetWindowPos(hwnd, IntPtr.Zero,
                    bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                    SWP_NOZORDER | SWP_NOACTIVATE);
            }
            else
            {
                // Extremely unlikely fallback: DIP math against main-window DPI.
                var dpi = GetMainWindowDpi();
                Left = bounds.Left / dpi.X;
                Top = bounds.Top / dpi.Y;
                Width = bounds.Width / dpi.X;
                Height = bounds.Height / dpi.Y;
            }
        }

        private System.Windows.Forms.Screen ResolveMonitor()
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (screens.Length == 0) return null;

            // Revalidate the user-selected monitor — if it was unplugged since
            // selection (or its bounds changed), fall back to default picking
            // so we don't try to position onto coordinates that no longer exist.
            if (TargetMonitor != null)
            {
                var stillPresent = screens.Any(s =>
                    string.Equals(s.DeviceName, TargetMonitor.DeviceName, StringComparison.OrdinalIgnoreCase) &&
                    s.Bounds == TargetMonitor.Bounds);

                if (!stillPresent)
                {
                    TargetMonitor = null;
                }
            }

            return TargetMonitor ?? PickDefaultMonitor();
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

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
    }
}
