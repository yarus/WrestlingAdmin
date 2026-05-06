using System.Collections.Generic;
using System.Windows.Input;

namespace Wrestling.UI.Material.Model
{
    public interface IShellViewModel
    {
        ViewModelBase CurrentViewModel { get; set; }
        void ShowSnackbarMessage(string message);
        void RequestClose();

        // Persistent left navigation rail (Material Design 3) populated by
        // App.xaml.cs after all phase-VM types are registered. Empty until
        // the builder sets it.
        IList<INavigationItem> NavigationItems { get; }

        // The rail item whose TargetViewModel matches CurrentViewModel — or
        // its "parent" item for full-screen overlays (MatchControl ⇒ Phase5,
        // PrintBracket ⇒ Phase4, etc.). Null when no rail item applies.
        INavigationItem ActiveItem { get; }

        // Visibility of the rail itself. False on the pre-tournament Home
        // screen and on full-screen overlays where the rail would compete
        // with the immersive content.
        bool IsRailVisible { get; }

        // Visibility of the FAB-style "Сохранить" button in the top bar.
        // Tied to whether the current VM is a TournamentViewModelBase that
        // can persist to a .wrt file.
        bool IsSaveCommandVisible { get; }

        // Manual escape hatch — delegates to TournamentViewModelBase.SaveDataAsync
        // on the current VM (which prompts for SaveAs if FileName is empty).
        // Autosave hook (SaveIfAutosaveEnabledAsync) is a separate background
        // path and stays event-driven.
        ICommand SaveCommand { get; }

        void SetNavigationItems(IList<INavigationItem> items);

        // Pin a full-screen overlay VM (e.g. MatchControl) to a phase tile in
        // the rail so the operator's "you are here" indicator stays useful
        // while the match runs.
        void RegisterOverlayParent(System.Type overlayVm, System.Type parentVm);

        // The last non-overlay VM (i.e. the screen the operator was on before
        // a MatchControl/MatchResults full-screen took over). Replaces the
        // old IDataContext.IsBracketView global flag — instead of a Boolean
        // hint, we remember the exact VM type and navigate back to it.
        // Null when no overlay return is in flight.
        System.Type GetReturnVmType();
    }
}
