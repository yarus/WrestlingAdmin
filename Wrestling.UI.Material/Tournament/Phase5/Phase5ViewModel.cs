using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Phase5
{
    // Phase 5 «Проведение» wrapper. Holds three sub-tabs and a one-shot
    // return state used to restore the Carpet sub-tab + Schedule/Brackets
    // toggle after a full-screen MatchControl/MatchResults overlay closes.
    public class Phase5ViewModel : TournamentViewModelBase
    {
        private IList<IPhase5SubViewModel> _subTabs;
        private IPhase5SubViewModel _currentSubTab;

        private ICommand _changeSubTabCommand;

        // One-shot deep-link target — set by Phase5AdminViewModel before
        // navigating sideways into Phase 6 (или by external code) so the
        // next plain navigation lands on a specific sub-tab. Consumed in
        // InitData and cleared.
        private Type _pendingInitialSubTabType;

        // One-shot return state — populated by CarpetSubViewModel just
        // before it kicks off MatchControlViewModel (full-screen overlay).
        // Consumed in InitData when the operator returns from the match.
        private Guid? _returnCarpetId;
        private bool _returnIsBrackets;
        private bool _hasReturnState;

        public Phase5ViewModel(IDiContainer container) : base(container)
        {
        }

        public override string PageTitle => "Проведение";

        public override bool IsBackButtonAvailable => false;

        public IList<IPhase5SubViewModel> SubTabs => _subTabs;

        public IPhase5SubViewModel CurrentSubTab
        {
            get => _currentSubTab;
            set
            {
                if (_currentSubTab == value) return;
                _currentSubTab = value;
                OnPropertyChanged(nameof(CurrentSubTab));
                OnPropertyChanged(nameof(QuickButtons));
                _currentSubTab?.InitData();
            }
        }

        public override IList<CommandButtonItem> QuickButtons => _currentSubTab?.QuickButtons;

        public ICommand ChangeSubTabCommand =>
            _changeSubTabCommand ?? (_changeSubTabCommand = new RelayCommand(
                p => CurrentSubTab = p as IPhase5SubViewModel,
                p => p is IPhase5SubViewModel));

        public override void InitData()
        {
            base.InitData();

            if (_subTabs == null)
            {
                _subTabs = new List<IPhase5SubViewModel>
                {
                    new CarpetSubViewModel(DiContainer),
                    new Phase5AdminViewModel(DiContainer),
                    Resolve<SliderTabViewModel>() ?? new SliderTabViewModel(DiContainer),
                };
            }

            // Restore tab from pending deep-link if set; otherwise default
            // to Carpet sub-tab. Return state from a closed match overlay
            // also wins (forces Carpet sub-tab + Schedule/Brackets restore).
            IPhase5SubViewModel target;
            CarpetSubViewModel returnTarget = null;

            if (_hasReturnState)
            {
                returnTarget = _subTabs.OfType<CarpetSubViewModel>().FirstOrDefault();
                target = returnTarget;
                _hasReturnState = false;
            }
            else if (_pendingInitialSubTabType != null)
            {
                target = _subTabs.FirstOrDefault(s => s.GetType() == _pendingInitialSubTabType)
                         ?? _subTabs.FirstOrDefault();
                _pendingInitialSubTabType = null;
            }
            else
            {
                target = _currentSubTab ?? _subTabs.FirstOrDefault();
            }

            CurrentSubTab = target;

            // RestoreReturnState must run AFTER the carpet sub-tab's InitData,
            // because InitData resets IsBracketsView=false (always-default-to-Расписание).
            // Returning from a match overlay should keep the operator on whichever
            // view they had open.
            if (returnTarget != null)
            {
                returnTarget.RestoreReturnState(_returnCarpetId, _returnIsBrackets);
            }
        }

        // Deep-link: Phase5AdminViewModel/external code prepositions the
        // sub-tab before NavigateToView<Phase5ViewModel>().
        public void SetInitialSubTab<TSub>() where TSub : class, IPhase5SubViewModel
        {
            _pendingInitialSubTabType = typeof(TSub);
        }

        // Called by CarpetSubViewModel right before launching MatchControl
        // so we can put the user back on the same carpet + view after they
        // return. Replaces the old IDataContext.IsBracketView global flag.
        public void RememberCarpetReturn(Guid carpetId, bool isBrackets)
        {
            _returnCarpetId = carpetId;
            _returnIsBrackets = isBrackets;
            _hasReturnState = true;
        }
    }
}
