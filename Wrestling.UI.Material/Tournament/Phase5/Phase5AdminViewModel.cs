using System.Collections.Generic;
using System.Collections.ObjectModel;
using MaterialDesignThemes.Wpf;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Phase5
{
    // Phase 5 → Администратор. Step-5 minimum (per /grill-me decision):
    // peer status read-out + deep-link to Phase 6 «Журнал матчей» + a
    // print-current-day-protocols quick-button. The full progress
    // dashboard is a separate TodoList item.
    public class Phase5AdminViewModel : ViewModelBase, IPhase5SubViewModel
    {
        private PeerSyncStatusTracker _tracker;

        private IList<CommandButtonItem> _quickButtons;

        public Phase5AdminViewModel(IDiContainer container) : base(container)
        {
        }

        public string PageName => "Администратор";

        public PackIconKind IconKind => PackIconKind.AccountTie;

        public ObservableCollection<PeerStatusViewModel> PeerStatuses => _tracker?.Peers;

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                return _quickButtons ?? (_quickButtons = new List<CommandButtonItem>
                {
                    new CommandButtonItem(
                        "Печать протокола хода матчей",
                        PackIconKind.PrinterOutline,
                        new RelayCommand(
                            param => PrintMatchProgressProtocol(),
                            param => true))
                });
            }
        }

        public override void InitData()
        {
            base.InitData();

            if (_tracker == null)
            {
                _tracker = Resolve<PeerSyncStatusTracker>();
                OnPropertyChanged(nameof(PeerStatuses));
            }
        }

        private void PrintMatchProgressProtocol()
        {
            // Step-5 stub: full implementation lives in Step 7 alongside
            // the other export migrations. Hooks into the existing
            // PrintScheduleViewModel flow with an OnlyCompleted=true
            // parameter once that property is added.
        }
    }
}
