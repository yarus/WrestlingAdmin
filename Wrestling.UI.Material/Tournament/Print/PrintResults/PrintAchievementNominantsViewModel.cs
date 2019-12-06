using System.Collections.Generic;
using Wrestling.Entities;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Print.PrintResults
{
    public class PrintAchievementNominantsViewModel : TournamentViewModelBase
    {
        public PrintAchievementNominantsViewModel(IDiContainer container) : base(container)
        {
        }

        private List<WrestlerAchievement> _results;

        public override string PageTitle => "Печать Протокола";

        public List<WrestlerAchievement> Results
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
