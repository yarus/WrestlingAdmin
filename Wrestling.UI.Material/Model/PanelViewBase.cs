using System;
using System.Windows;

namespace Wrestling.UI.Material.Model
{
    public class PanelViewBase : ChildWindowBase
    {
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

            System.Windows.Forms.Screen monitor = System.Windows.Forms.Screen.AllScreens.Length > 1
                ? System.Windows.Forms.Screen.AllScreens[1]
                : System.Windows.Forms.Screen.AllScreens[0];

            if (Equals(System.Windows.Forms.Screen.PrimaryScreen, monitor) &&
                System.Windows.Forms.Screen.AllScreens.Length > 1)
            {
                monitor = System.Windows.Forms.Screen.AllScreens[0];
            }

            System.Drawing.Rectangle r1 = monitor.WorkingArea;

            Top = r1.Top;
            Left = r1.Left;
            Width = r1.Width;
            Height = r1.Height;

            WindowStyle = WindowStyle.None;
        }
    }
}
