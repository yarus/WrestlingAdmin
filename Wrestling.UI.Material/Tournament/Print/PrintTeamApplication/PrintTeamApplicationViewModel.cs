using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Print.PrintTeamApplication
{
    public class PrintTeamApplicationViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        private TeamApplicationViewModel _selectedTeam;

        public string PageName => "Заявка";
        public override string PageTitle => "Заявка от команды";
        
        public PrintTeamApplicationViewModel(IDiContainer container) : base(container)
        {
        }

        public override bool IsBackButtonAvailable => true;

        public TeamApplicationViewModel SelectedTeam
        {
            get { return _selectedTeam; }
            set
            {
                _selectedTeam = value;

                OnPropertyChanged("SelectedTeam");
            }
        }

        public override void InitData()
        {
            base.InitData();

            SelectedTeam = DataContext.Team;
        }
    }
}