using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    public interface IPanelView
    {
        bool WasShown { get; }
        void CloseScreen();
        void ShowScreen(ObservableObject dataContext);
    }
}
