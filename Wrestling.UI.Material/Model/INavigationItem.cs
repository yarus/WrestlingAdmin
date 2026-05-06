using System;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    // One pinned item in the always-visible left navigation rail. Items are
    // owned by the shell (MainWindowViewModel.NavigationItems), not by per-VM
    // contracts. Selection is communicated via the active-indicator pill in
    // the rail template; the icon stays the same in both states.
    public interface INavigationItem
    {
        string Label { get; }
        PackIconKind Icon { get; }
        bool IsSeparator { get; }
        bool IsActive { get; }
        ICommand ActivateCommand { get; }
        Type TargetViewModel { get; }
    }

    public sealed class NavigationItem : ObservableObject, INavigationItem
    {
        private bool _isActive;

        public NavigationItem(
            string label,
            PackIconKind icon,
            Type targetViewModel,
            ICommand activateCommand)
        {
            Label = label;
            Icon = icon;
            TargetViewModel = targetViewModel;
            ActivateCommand = activateCommand;
        }

        public static NavigationItem Separator() => new NavigationItem();

        private NavigationItem()
        {
            IsSeparator = true;
            Label = string.Empty;
        }

        public string Label { get; }
        public PackIconKind Icon { get; }
        public bool IsSeparator { get; }
        public Type TargetViewModel { get; }
        public ICommand ActivateCommand { get; }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value) return;
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
            }
        }
    }
}
