using System.Collections.Generic;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Progress.Brackets;
using Wrestling.UI.Material.Tournament.Progress.Schedule;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Phase5
{
    // Phase 5 → Ковер. Hosts either ScheduleViewModel or BracketsViewModel
    // (both are existing singleton VMs with their own carpet picker rows).
    // The QuickButton on the page header toggles between the two views.
    public class CarpetSubViewModel : ViewModelBase, IPhase5SubViewModel
    {
        private bool _isBracketsView;

        private ScheduleViewModel _scheduleVm;
        private BracketsViewModel _bracketsVm;

        private ICommand _toggleViewCommand;

        private CommandButtonItem _toggleViewButton;
        private IList<CommandButtonItem> _quickButtons;

        public CarpetSubViewModel(IDiContainer container) : base(container)
        {
        }

        public string PageName => "Ковер";

        public PackIconKind IconKind => PackIconKind.Sofa;

        public bool IsBracketsView
        {
            get => _isBracketsView;
            set
            {
                if (_isBracketsView == value) return;
                _isBracketsView = value;
                OnPropertyChanged(nameof(IsBracketsView));
                OnPropertyChanged(nameof(CurrentInnerVm));

                UpdateToggleButton();

                _currentInnerVm()?.InitData();
            }
        }

        public ViewModelBase CurrentInnerVm => _currentInnerVm();

        private ViewModelBase _currentInnerVm()
            => _isBracketsView ? (ViewModelBase)_bracketsVm : _scheduleVm;

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                if (_quickButtons == null)
                {
                    _toggleViewButton = new CommandButtonItem(
                        string.Empty,
                        PackIconKind.Sitemap,
                        ToggleViewCommand);
                    UpdateToggleButton();
                    _quickButtons = new List<CommandButtonItem> { _toggleViewButton };
                }
                return _quickButtons;
            }
        }

        public ICommand ToggleViewCommand =>
            _toggleViewCommand ?? (_toggleViewCommand = new RelayCommand(
                _ => IsBracketsView = !IsBracketsView,
                _ => true));

        public override void InitData()
        {
            base.InitData();

            var nav = Resolve<INavigationService>();
            _scheduleVm = nav?.GetViewModel<ScheduleViewModel>();
            _bracketsVm = nav?.GetViewModel<BracketsViewModel>();

            // Always default to Расписание each time the page opens — the
            // toggle is intentionally not persisted.
            _isBracketsView = false;
            OnPropertyChanged(nameof(IsBracketsView));
            OnPropertyChanged(nameof(CurrentInnerVm));

            UpdateToggleButton();

            _currentInnerVm()?.InitData();
        }

        // Used by Phase5ViewModel.InitData when restoring after a match.
        // The carpetId argument is now ignored — inner Schedule/Brackets VMs
        // own their carpet selection. The view-mode flag is honored.
        public void RestoreReturnState(System.Guid? carpetId, bool isBrackets)
        {
            _isBracketsView = isBrackets;
            OnPropertyChanged(nameof(IsBracketsView));
            OnPropertyChanged(nameof(CurrentInnerVm));
            UpdateToggleButton();
        }

        private void UpdateToggleButton()
        {
            if (_toggleViewButton == null) return;
            // The button advertises the *destination* mode — i.e. clicking
            // it switches you to whichever view you're not on right now.
            if (_isBracketsView)
            {
                _toggleViewButton.IconKind = PackIconKind.Calendar;
                _toggleViewButton.TooltipText = "Показать расписание";
            }
            else
            {
                _toggleViewButton.IconKind = PackIconKind.Sitemap;
                _toggleViewButton.TooltipText = "Показать сетки";
            }
        }
    }
}
