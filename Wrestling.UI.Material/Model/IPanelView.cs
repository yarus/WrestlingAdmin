using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    public interface IPanelView
    {
        void CloseScreen();
        void ShowScreen(ObservableObject dataContext);
    }
}