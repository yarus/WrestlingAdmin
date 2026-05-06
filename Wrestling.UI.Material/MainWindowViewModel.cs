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
        private INavigationItem _activeItem;
        private bool _isDrawerOpen;
        private ICommand _saveCommand;

        // Maps overlay/print VMs to the rail item that should appear "active"
        // while they are on screen. Populated externally via SetOverlayParent
        // so we don't hard-code phase-VM types in the shell. Step 6 wires it
        // up once Phase4/Phase5 etc. are real types.
        private readonly Dictionary<Type, Type> _overlayParents = new Dictionary<Type, Type>();

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

                // Capture the outgoing screen type if the incoming one is a
                // full-screen overlay (MatchControl/MatchResults/PrintBracket).
                // OnBackCommand on those VMs reads GetReturnVmType() to decide
                // where to navigate back to — replaces the legacy
                // IDataContext.IsBracketView Boolean flag.
                if (_currentViewModel != null
                    && IsOverlayType(value?.GetType())
                    && !IsOverlayType(_currentViewModel.GetType()))
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

        public void SetNavigationItems(IList<INavigationItem> items)
        {
            _navigationItems = items ?? new List<INavigationItem>();
            OnPropertyChanged(nameof(NavigationItems));
            UpdateActiveItem();
        }

        // Step 6 will call this so MatchControl/MatchResults stay highlighted
        // under "Проведение" while they're full-screen, etc. Step 2 just
        // exposes the API.
        public void RegisterOverlayParent(Type overlayVm, Type parentVm)
        {
            if (overlayVm == null || parentVm == null) return;
            _overlayParents[overlayVm] = parentVm;
            UpdateActiveItem();
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
            if (_navigationItems == null || _navigationItems.Count == 0)
            {
                ActiveItem = null;
                return;
            }

            var vmType = _currentViewModel?.GetType();
            if (vmType == null)
            {
                ActiveItem = null;
                return;
            }

            // Direct match — current VM is exactly one of the rail targets.
            var direct = _navigationItems.FirstOrDefault(i => !i.IsSeparator && i.TargetViewModel == vmType);
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
                var parentItem = _navigationItems.FirstOrDefault(i => !i.IsSeparator && i.TargetViewModel == parent);
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
