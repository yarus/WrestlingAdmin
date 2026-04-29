using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    public class CommandButtonItem : ObservableObject
    {
        public CommandButtonItem(string label, ICommand open) : this(label, string.Empty, null, open)
        {
        }

        public CommandButtonItem(string tooltipText, PackIconKind iconKind, ICommand open) : this(string.Empty, tooltipText, iconKind, open)
        {
        }

        public CommandButtonItem(string label, string tooltipText, PackIconKind? iconKind, ICommand open)
        {
            Label = label;
            TooltipText = tooltipText;
            IconKind = iconKind;
            Open = open;
        }

        public string Label { get; set; }

        public string TooltipText { get; set; }

        public PackIconKind? IconKind { get; set; }

        public ICommand Open { get; }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged("IsBusy");
            }
        }
    }
}