using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Wrestling.UI.Material.Tournament.Standing.Applications
{
    public partial class AddWrestlerDialog : UserControl
    {
        public AddWrestlerDialog()
        {
            InitializeComponent();
        }

        private void AthleteSearch_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not AddWrestlerViewModel vm) return;

            if (e.Key == Key.Enter)
            {
                // Enter in search must not trigger the dialog's default Save button.
                e.Handled = true;
                if (vm.IsAthleteSearchOpen && vm.SelectAthleteCommand.CanExecute(null))
                {
                    vm.SelectAthleteCommand.Execute(null);
                    AthleteSearch.Focus();
                }
            }
            else if (e.Key == Key.Escape && vm.IsAthleteSearchOpen)
            {
                vm.IsAthleteSearchOpen = false;
                AthleteSearch.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Down && sender == AthleteSearch && vm.FilteredAthletes.Count > 0)
            {
                vm.IsAthleteSearchOpen = true;
                AthleteSuggestions.SelectedIndex = 0;
                AthleteSuggestions.Focus();
                e.Handled = true;
            }
        }

        private void AthleteSuggestions_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AddWrestlerViewModel vm
                && e.OriginalSource is DependencyObject source
                && ItemsControl.ContainerFromElement(AthleteSuggestions, source) is ListBoxItem item
                && vm.SelectAthleteCommand.CanExecute(item.DataContext))
            {
                vm.SelectAthleteCommand.Execute(item.DataContext);
                AthleteSearch.Focus();
                e.Handled = true;
            }
        }
    }
}
