using System.ComponentModel;
using System.Windows;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    public class ChildWindowBase : Window, IPanelView
    {
        protected bool WasShown { get; private set; }
        
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

            if (!WasShown)
            {
                WasShown = true;

                AdjustScreenOnShow();

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
