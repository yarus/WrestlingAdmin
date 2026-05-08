using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material
{
    public class MainWindowViewModel : ViewModelBase, IShellViewModel
    {
        private static readonly IReadOnlyList<INavigationItem> EmptyItems = new List<INavigationItem>().AsReadOnly();

        private readonly ISnackbarMessageQueue _snackbarMessageQueue;

        private ViewModelBase _currentViewModel;
        private IList<INavigationItem> _navigationItems = EmptyItems.ToList();
        private IList<INavigationItem> _footerNavigationItems = EmptyItems.ToList();
        private INavigationItem _activeItem;
        private bool _isDrawerOpen;
        private ICommand _saveCommand;

        // Maps overlay/print VMs to the rail item that should appear "active"
        // while they are on screen. Populated externally via SetOverlayParent
        // so we don't hard-code phase-VM types in the shell.
        private readonly Dictionary<Type, Type> _overlayParents = new Dictionary<Type, Type>();

        // VMs that participate in the dynamic return-source chain — back
        // returns to whichever non-match-overlay screen launched them. Set is
        // populated externally via RegisterMatchOverlay so we don't hard-code
        // MatchControl/MatchResults/PrintBracket types in the shell.
        private readonly HashSet<Type> _matchOverlays = new HashSet<Type>();

        public MainWindowViewModel(ISnackbarMessageQueue snackbarMessageQueue, IDiContainer di) : base(di)
        {
            if (snackbarMessageQueue == null) throw new ArgumentNullException(nameof(snackbarMessageQueue));
            _snackbarMessageQueue = snackbarMessageQueue;
        }

        public override void InitData()
        {
            base.InitData();

            // IsRailVisible mirrors whether a tournament is open — the only
            // state where phase-rail navigation is meaningful.
            DataContext.TournamentChanged += OnTournamentChanged;
            UpdateRailVisibility();
        }

        public event EventHandler OnRequestClose;

        public void RequestClose()
        {
            OnRequestClose?.Invoke(this, EventArgs.Empty);
        }

        public void ShowSnackbarMessage(string message)
        {
            _snackbarMessageQueue.Enqueue(message);
        }

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                if (_currentViewModel == value) return;

                // Capture the outgoing screen type when entering a match
                // overlay (MatchControl/MatchResults/PrintBracket) from
                // anything that ISN'T already a match overlay. This lets the
                // chain MatchControl→MatchResults preserve the screen that
                // launched MatchControl (e.g. Schedule), so back from
                // MatchResults lands there. Schedule/Brackets/Slider count as
                // "non-match-overlay" sources here even though they hide the
                // rail — their own OnBackCommand goes to Conducting explicitly.
                if (_currentViewModel != null
                    && IsMatchOverlay(value?.GetType())
                    && !IsMatchOverlay(_currentViewModel.GetType()))
                {
                    _returnVmType = _currentViewModel.GetType();
                }

                _currentViewModel = value;
                OnPropertyChanged(nameof(CurrentViewModel));
                OnPropertyChanged(nameof(IsRailVisible));
                OnPropertyChanged(nameof(IsSaveCommandVisible));
                UpdateActiveItem();
            }
        }

        public System.Type GetReturnVmType() => _returnVmType;

        private System.Type _returnVmType;

        private bool IsOverlayType(System.Type t) => t != null && _overlayParents.ContainsKey(t);

        private bool IsMatchOverlay(System.Type t) => t != null && _matchOverlays.Contains(t);

        // Legacy property — bound by the current MainWindow.xaml DrawerHost
        // and hamburger ToggleButton. Removed entirely in Step 6 when the
        // hideable drawer is replaced by the persistent NavigationRail.
        public bool IsDrawerOpen
        {
            get => _isDrawerOpen;
            set
            {
                if (_isDrawerOpen == value) return;
                _isDrawerOpen = value;
                OnPropertyChanged(nameof(IsDrawerOpen));
            }
        }

        public IList<INavigationItem> NavigationItems => _navigationItems;

        public IList<INavigationItem> FooterNavigationItems => _footerNavigationItems;

        public INavigationItem ActiveItem
        {
            get => _activeItem;
            private set
            {
                if (_activeItem == value) return;
                if (_activeItem != null && _activeItem is NavigationItem oldItem) oldItem.IsActive = false;
                _activeItem = value;
                if (_activeItem != null && _activeItem is NavigationItem newItem) newItem.IsActive = true;
                OnPropertyChanged(nameof(ActiveItem));
            }
        }

        // Rail is visible whenever a tournament is open AND the active view
        // isn't a full-screen overlay (MatchControl/MatchResults/PrintBracket).
        public bool IsRailVisible =>
            DataContext?.Tournament != null
            && !IsOverlayType(_currentViewModel?.GetType());

        public bool IsSaveCommandVisible =>
            IsRailVisible
            && _currentViewModel is TournamentViewModelBase tvm
            && tvm.Tournament != null;

        public ICommand SaveCommand
        {
            get
            {
                return _saveCommand ?? (_saveCommand = new AsyncRelayCommand(
                    execute: async _ =>
                    {
                        if (_currentViewModel is TournamentViewModelBase tvm && tvm.Tournament != null)
                        {
                            await tvm.SaveDataAsync();
                        }
                    },
                    canExecute: _ => IsSaveCommandVisible));
            }
        }

        public void SetNavigationItems(IList<INavigationItem> mainItems, IList<INavigationItem> footerItems)
        {
            _navigationItems = mainItems ?? new List<INavigationItem>();
            _footerNavigationItems = footerItems ?? new List<INavigationItem>();
            OnPropertyChanged(nameof(NavigationItems));
            OnPropertyChanged(nameof(FooterNavigationItems));
            UpdateActiveItem();
        }

        // Called from App.xaml.cs to map overlay VMs to their rail-tile parent
        // (e.g. MatchControl → Conducting). Drives both rail visibility and the
        // active-item highlight while the overlay is on screen.
        public void RegisterOverlayParent(Type overlayVm, Type parentVm)
        {
            if (overlayVm == null || parentVm == null) return;
            _overlayParents[overlayVm] = parentVm;
            UpdateActiveItem();
        }

        // Marks a VM as a match overlay — only these participate in the
        // dynamic return-source chain (back to whichever screen launched the
        // chain). Wired in App.xaml.cs alongside RegisterOverlayParent.
        public void RegisterMatchOverlay(Type matchOverlayVm)
        {
            if (matchOverlayVm == null) return;
            _matchOverlays.Add(matchOverlayVm);
        }

        private void OnTournamentChanged(object sender, Wrestling.Entities.Tournament tournament)
        {
            UpdateRailVisibility();
        }

        private void UpdateRailVisibility()
        {
            OnPropertyChanged(nameof(IsRailVisible));
            OnPropertyChanged(nameof(IsSaveCommandVisible));
        }

        private void UpdateActiveItem()
        {
            var vmType = _currentViewModel?.GetType();
            if (vmType == null)
            {
                ActiveItem = null;
                return;
            }

            var allItems = _navigationItems.Concat(_footerNavigationItems);

            // Direct match — current VM is exactly one of the rail targets.
            var direct = allItems.FirstOrDefault(i => !i.IsSeparator && i.TargetViewModel == vmType);
            if (direct != null)
            {
                ActiveItem = direct;
                return;
            }

            // Overlay match — e.g. MatchControl while a "Проведение" match
            // is running. Walk the parent chain in case overlays nest.
            var probe = vmType;
            while (probe != null && _overlayParents.TryGetValue(probe, out var parent))
            {
                var parentItem = allItems.FirstOrDefault(i => !i.IsSeparator && i.TargetViewModel == parent);
                if (parentItem != null)
                {
                    ActiveItem = parentItem;
                    return;
                }
                probe = parent;
            }

            ActiveItem = null;
        }
    }
}
