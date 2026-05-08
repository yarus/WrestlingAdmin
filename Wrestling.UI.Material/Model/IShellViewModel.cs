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
        // the builder sets it. Top group of items (tournament phases).
        IList<INavigationItem> NavigationItems { get; }

        // Footer items pinned to the bottom of the rail (e.g. Settings),
        // visually separated from the main phase navigation by an empty
        // spacer row.
        IList<INavigationItem> FooterNavigationItems { get; }

        // The rail item whose TargetViewModel matches CurrentViewModel — or
        // its "parent" item for full-screen overlays (MatchControl ⇒ Conducting,
        // PrintBracket ⇒ Conducting, etc.). Null when no rail item applies.
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

        void SetNavigationItems(IList<INavigationItem> mainItems, IList<INavigationItem> footerItems);

        // Pin a full-screen overlay VM (e.g. MatchControl) to a phase tile in
        // the rail so the operator's "you are here" indicator stays useful
        // while the match runs.
        void RegisterOverlayParent(System.Type overlayVm, System.Type parentVm);

        // Mark a VM as a "match overlay" — i.e. one that participates in the
        // dynamic return-source chain (back returns to whichever screen
        // launched it). Only MatchControl / MatchResults / PrintBracket
        // qualify. Schedule/Brackets/Slider hide the rail too, but they have
        // a static back target (Conducting) so they don't go through this chain.
        void RegisterMatchOverlay(System.Type matchOverlayVm);

        // The last screen the operator was on before a match overlay
        // (MatchControl/MatchResults/PrintBracket) took over. Captured by
        // MainWindowViewModel on the transition into a match overlay; read
        // by those overlays' OnBackCommand to navigate back. Replaces the
        // old IDataContext.IsBracketView global flag.
        // Null when no match-overlay return is in flight.
        System.Type GetReturnVmType();
    }
}
