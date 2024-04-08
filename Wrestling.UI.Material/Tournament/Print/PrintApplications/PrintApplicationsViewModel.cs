using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Print.PrintApplications
{
    public class PrintApplicationsViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        private AgeWeightGroup _selectedGroup;

        private List<PrintWrestlerApplicationViewModel> _groupWrestlers = new List<PrintWrestlerApplicationViewModel>();

        public string PageName => "Протокол Взвешивания";
        public override string PageTitle => "Протокол взвешивания участников соревнований";
        
        public PrintApplicationsViewModel(IDiContainer container) : base(container)
        {
        }

        public override bool IsBackButtonAvailable => true;

        public AgeWeightGroup SelectedGroup
        {
            get { return _selectedGroup; }
            set
            {
                _selectedGroup = value;

                OnPropertyChanged("SelectedGroup");
            }
        }

        public List<PrintWrestlerApplicationViewModel> GroupWrestlers
        {
            get { return _groupWrestlers; }
            set
            {
                _groupWrestlers = value;

                OnPropertyChanged("GroupWrestlers");
            }
        }

        public override void InitData()
        {
            base.InitData();

            SelectedGroup = DataContext.Group;

            var results = new List<PrintWrestlerApplicationViewModel>();

            var wrestlers = SelectedGroup.Wrestlers.OrderBy(x => x.LastFirstName).ThenBy(x => x.SeedNumber).ToList();

            var i = 1;

            foreach (var wrestler in wrestlers)
            {
                results.Add(new PrintWrestlerApplicationViewModel
                {
                    Order = i,
                    SeedNumber = wrestler.SeedNumber,
                    AthleteName = wrestler.LastFirstName,
                    BirthYear = wrestler.BirthDate?.Year,
                    Level = wrestler.Level,
                    TeamName = wrestler.TeamName,
                    TeamCity = wrestler.TeamCity,
                    Weight = wrestler.Weight
                });

                i++;
            }

            GroupWrestlers = results;
        }
    }

    public class PrintWrestlerApplicationViewModel
    {
        public int? Order { get; set; }
        public int? SeedNumber { get; set; }
        public string AthleteName { get; set; }
        public int? BirthYear { get; set; }
        public string TeamName { get; set; }
        public string TeamCity { get; set; }
        public string Level { get; set; }
        public double? Weight { get; set; }
    }
}