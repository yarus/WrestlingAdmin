using System.Drawing;
using Wrestling.UI.Material.ScoreScreen;

namespace Wrestling.UI.Material.Utils.Recording.OverlayDrawer
{
    public interface IOverlayDrawer
    {
        void DrawOverlay(Bitmap frame, ScoreScreenViewModel currentMatch);
    }
}
