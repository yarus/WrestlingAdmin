namespace Wrestling.UI.Material.Model
{
    public interface INavigationService
    {
        IShellViewModel ShellVm { get; set; }
        void LoadNavigation();
        void NavigateToView<T>() where T : ViewModelBase;
        // Non-generic overload — used by overlay-return logic where the
        // target VM type is only known at runtime (the type is whatever
        // the shell captured before entering the full-screen overlay).
        void NavigateToView(System.Type viewModelType);
        T GetViewModel<T>() where T : ViewModelBase;
        ViewModelBase GetViewModel(System.Type viewModelType);
        void ShowPrintPreview(ViewModelBase vm);
        void CloseApp();
    }
}