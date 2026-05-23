using System;
using System.ComponentModel;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.UI.Utils.Localization;

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
        private readonly string _labelKey;

        // labelKey is a localization key looked up via LocalizationService.
        // The item subscribes to language-change events so the rail updates
        // live. Items live for the whole app session — no unsubscribe path.
        public NavigationItem(
            string labelKey,
            PackIconKind icon,
            Type targetViewModel,
            ICommand activateCommand)
        {
            _labelKey = labelKey;
            Icon = icon;
            TargetViewModel = targetViewModel;
            ActivateCommand = activateCommand;

            LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
        }

        public static NavigationItem Separator() => new NavigationItem();

        private NavigationItem()
        {
            IsSeparator = true;
            _labelKey = string.Empty;
        }

        public string Label => string.IsNullOrEmpty(_labelKey)
            ? string.Empty
            : LocalizationService.Instance.T(_labelKey);

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

        private void OnLocalizationChanged(object sender, PropertyChangedEventArgs e)
        {
            // "Item[]" is what LocalizationService raises on a language switch
            // to invalidate every indexer binding. Match that and also the
            // CurrentLanguage notification, so we refresh on either signal.
            if (e.PropertyName == "Item[]" || e.PropertyName == nameof(LocalizationService.CurrentLanguage))
            {
                OnPropertyChanged(nameof(Label));
            }
        }
    }
}
