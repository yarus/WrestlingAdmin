namespace Wrestling.UI.Material.Model
{
    public interface INavigationService
    {
        IShellViewModel ShellVm { get; set; }
        void LoadNavigation();
        void NavigateToView<T>() where T : ViewModelBase;
        T GetViewModel<T>() where T : ViewModelBase;
        void ShowPrintPreview(ViewModelBase vm);
        void CloseApp();
    }
}