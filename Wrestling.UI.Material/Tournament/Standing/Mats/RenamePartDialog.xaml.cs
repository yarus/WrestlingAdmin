using System.Windows.Controls;

namespace Wrestling.UI.Material.Tournament.Standing.Mats
{
    public partial class RenamePartDialog : UserControl
    {
        public RenamePartDialog()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                NameBox.Focus();
                NameBox.SelectAll();
            };
        }
    }

    public sealed class RenamePartDialogViewModel : Wrestling.Entities.ObservableObject
    {
        private string _newName;
        public string NewName
        {
            get => _newName;
            set { _newName = value; OnPropertyChanged(nameof(NewName)); }
        }
    }
}
