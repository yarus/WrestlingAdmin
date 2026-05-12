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

        // T inherited from TournamentViewModelBase — same lazy-resolve pattern
        // as every other Print*ViewModel.
        public string PageName => T("PrintApplications_PageName", "Протокол Взвешивания");
        public override string PageTitle => T("PrintApplications_PageTitle", "Протокол взвешивания участников соревнований");
        
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

            var wrestlers = SelectedGroup.Wrestlers.OrderBy(x => x.SeedNumber).Select(y => new PrintWrestlerApplicationViewModel
            {
                SeedNumber = y.SeedNumber,
                AthleteName = y.LastFirstName,
                BirthYear = y.BirthDate?.Year,
                Level = y.LevelDisplay,
                TeamName = y.TeamName,
                TeamCity = y.TeamCity,
                Weight = y.Weight
            }).ToList();

            GroupWrestlers = wrestlers;
        }
    }

    public class PrintWrestlerApplicationViewModel
    {
        public int? SeedNumber { get; set; }
        public string AthleteName { get; set; }
        public int? BirthYear { get; set; }
        public string TeamName { get; set; }
        public string TeamCity { get; set; }
        public string Level { get; set; }
        public double? Weight { get; set; }
    }
}