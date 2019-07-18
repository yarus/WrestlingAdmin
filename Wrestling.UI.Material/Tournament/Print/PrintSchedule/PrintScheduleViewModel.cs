using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Standing;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Print.PrintSchedule
{
    public class PrintScheduleViewModel : TournamentViewModelBase, IStandingPageViewModel
    {
        private Carpet _carpet;
        private CarpetStats _stat;
        private List<AgeWeightGroup> _groups;

        public string PageName => "Расписание";
        public override string PageTitle => "Расписание схваток по коврам";
        
        public PrintScheduleViewModel(IDiContainer container, Carpet carpet) : base(container)
        {
            _carpet = carpet;
        }

        public override bool IsBackButtonAvailable => true;

        public CarpetStats Stat
        {
            get { return _stat; }
            set
            {
                _stat = value;

                OnPropertyChanged("Stat");
            }
        }

        public override void InitData()
        {
            base.InitData();

            _groups = new List<AgeWeightGroup>(DataContext.Tournament.Groups.Where(g => g.Bracket != null));

            var matches = _groups.Where(g => g.CarpetLabel == _carpet.Name).SelectMany(g => g.Bracket.Rounds).SelectMany(r => r.RoundMatches).Where(m => m.IsMatchCanStart);

            Stat = new CarpetStats
            {
                CarpetID = _carpet.ID.Value,
                CarpetLabel = _carpet.Name,
                WrestlersCount = _carpet.WrestlersCount,
                GroupsCount = _carpet.Groups.Count,
                Matches = new ObservableCollection<WrestlingMatch>(matches.OrderBy(m => m.MatchNumber))
            };
        }
    }
}