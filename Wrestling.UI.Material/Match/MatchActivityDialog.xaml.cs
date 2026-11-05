using System.Threading.Tasks;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament;

namespace Wrestling.UI.Material.Match
{
    public partial class MatchActivityDialog : UserControl
    {
        public MatchActivityDialog() { InitializeComponent(); }

        internal static async Task<bool> ConfirmAsync(MatchActivityStatus status)
        {
            var message = string.Format(TournamentViewModelBase.T("MatchActivity_ConfirmBody",
                "Схватка уже открыта на ковре «{0}». Открыть её на этом компьютере?"), status.Label);
            if (status.ConnectionLost) message += "\n\n" + TournamentViewModelBase.T("MatchActivity_ConnectionLost",
                "Связь потеряна — возможно, схватка продолжается.");
            return await DialogHost.Show(new MatchActivityDialog { DataContext = message }, "RootDialog") is true;
        }
    }
}
