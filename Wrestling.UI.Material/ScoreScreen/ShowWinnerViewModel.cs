using System.Linq;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.ScoreScreen
{
    public class ShowWinnerViewModel : ViewModelBase
    {
        private string _emblemPath;
        private Wrestler _wrestler;

        public ShowWinnerViewModel(IDiContainer container, Wrestler wrestler) : base(container)
        {
            _wrestler = wrestler;
        }

        public override void InitData()
        {
            base.InitData();

            if (_wrestler?.TeamID != null)
            {
                var team = DataContext.Tournament.TeamApplications.FirstOrDefault(a => a.ID == _wrestler.TeamID.Value);
                if (team != null)
                {
                    EmblemPath = team.EmblemPath;
                }
            }
        }

        public string EmblemPath
        {
            get { return _emblemPath; }
            set
            {
                _emblemPath = value;
                OnPropertyChanged("EmblemPath");
            }
        }

        public Wrestler Wrestler
        {
            get { return _wrestler; }
            set
            {
                _wrestler = value;
                OnPropertyChanged("Wrestler");
            }
        }
    }
}