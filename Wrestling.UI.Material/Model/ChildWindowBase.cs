using System.ComponentModel;
using System.Windows;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    public class ChildWindowBase : Window, IPanelView
    {
        public bool WasShown { get; protected set; }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;

            CloseScreen();
        }

        public void CloseScreen()
        {
            Visibility = Visibility.Hidden;
        }

        public virtual void ShowScreen(ObservableObject dataContext)
        {
            DataContext = dataContext;

            // Re-apply positioning on every show, not only the first. This
            // handles monitor hot-plug: if the user unplugged / replugged / swapped
            // the external display between opens, the window gets placed on the
            // right screen this time around. Subclasses that don't care (e.g. the
            // print host) get a no-op base implementation.
            AdjustScreenOnShow();

            if (!WasShown)
            {
                WasShown = true;
                Show();
            }
            else
            {
                Visibility = Visibility.Visible;
            }
        }

        protected virtual void AdjustScreenOnShow()
        {

        }
    }
}
