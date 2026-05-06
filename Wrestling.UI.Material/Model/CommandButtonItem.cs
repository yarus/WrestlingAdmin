using System;
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

        private string _label;
        public string Label
        {
            get => _label;
            set { if (_label != value) { _label = value; OnPropertyChanged(nameof(Label)); } }
        }

        private string _tooltipText;
        public string TooltipText
        {
            get => _tooltipText;
            set { if (_tooltipText != value) { _tooltipText = value; OnPropertyChanged(nameof(TooltipText)); } }
        }

        private PackIconKind? _iconKind;
        public PackIconKind? IconKind
        {
            get => _iconKind;
            set { if (!Nullable.Equals(_iconKind, value)) { _iconKind = value; OnPropertyChanged(nameof(IconKind)); } }
        }

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