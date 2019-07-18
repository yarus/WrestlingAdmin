using System.Windows;
using System.Windows.Controls;

namespace Wrestling.UI.Material.Login
{
    /// <summary>
    /// Interaction logic for LoginView.xaml
    /// </summary>
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
        }

        private void UserNameBox_Loaded(object sender, RoutedEventArgs e)
        {
            ((TextBox) sender)?.Focus();
        }
    }
}