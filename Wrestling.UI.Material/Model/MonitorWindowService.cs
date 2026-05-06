using System.Threading.Tasks;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Model
{
    public sealed class MonitorWindowService : IMonitorWindowService
    {
        private readonly IDiContainer _di;

        public MonitorWindowService(IDiContainer di)
        {
            _di = di;
        }

        public bool IsVisible
        {
            get
            {
                var view = _di.Resolve("ScoreScreen") as IPanelView;
                return view != null && view.WasShown;
            }
        }

        public async Task ShowAsync()
        {
            var view = _di.Resolve("ScoreScreen") as IPanelView;
            if (view == null) return;

            var scoreScreen = _di.Resolve<ScoreScreenViewModel>();
            if (scoreScreen == null) return;

            if (!view.WasShown)
            {
                var monitor = await MonitorPicker.PickAsync();
                if (monitor == null) return;

                if (view is PanelViewBase panel)
                {
                    panel.TargetMonitor = monitor;
                }
            }

            view.ShowScreen(scoreScreen);
        }

        public void Close()
        {
            var view = _di.Resolve("ScoreScreen") as IPanelView;
            view?.CloseScreen();
        }
    }
}
