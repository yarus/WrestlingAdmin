using System.Windows;

namespace Wrestling.UI.Material.Model
{
    public interface IShellViewModel
    {
        ViewModelBase CurrentViewModel { get; set; }
        void ShowSnackbarMessage(string message);
        void RequestClose();
    }
}