using System.Collections.Generic;
using Wrestling.Entities.Results;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Print.PrintResults
{
    public class PrintTeamResultsViewModel : TournamentViewModelBase
    {
        private List<TournamentTeamResult> _teamResults;

        public override string PageTitle => "Печать Протокола";
        
        public List<TournamentTeamResult> TeamResults
        {
            get { return _teamResults; }
            set
            {
                _teamResults = value;
                OnPropertyChanged("TeamResults");
            }
        }

        public PrintTeamResultsViewModel(IDiContainer container) : base(container)
        {
        }
    }
}