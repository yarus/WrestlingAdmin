using System.Collections.Generic;
using Wrestling.Entities.Results;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Print.PrintResults
{
    public class PrintPersonalResultsViewModel : TournamentViewModelBase
    {
        public PrintPersonalResultsViewModel(IDiContainer container) : base(container)
        {
        }

        private List<TournamentResult> _results;

        public override string PageTitle => "Печать Протокола";

        public List<TournamentResult> Results
        {
            get { return _results; }
            set
            {
                _results = value;
                OnPropertyChanged("Results");
            }
        }
    }
}
