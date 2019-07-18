using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tournament.Print.PrintBracket
{
    public class PrintBracketViewModel : TournamentViewModelBase
    {
        private AgeWeightGroup _selectedGroup;
        private List<GroupRound> _groupMainRounds;
        private List<GroupRound> _groupAddRounds;

        public override string PageTitle => "Печать Протокола";
        
        public PrintBracketViewModel(IDiContainer container) : base(container)
        {
        }
        
        public AgeWeightGroup SelectedGroup
        {
            get { return _selectedGroup; }
            set
            {
                _selectedGroup = value;

                OnPropertyChanged("SelectedGroup");
            }
        }

        public List<GroupRound> GroupMainRounds
        {
            get { return _groupMainRounds; }
            set
            {
                _groupMainRounds = value;

                OnPropertyChanged("GroupMainRounds");
            }
        }

        public List<GroupRound> GroupAdditoinalRounds
        {
            get { return _groupAddRounds; }
            set
            {
                _groupAddRounds = value;

                OnPropertyChanged("GroupAdditoinalRounds");
            }
        }

        public override void InitData()
        {
            base.InitData();

            SelectedGroup = DataContext.Group;
            GroupMainRounds = SelectedGroup.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Main).ToList();
            GroupAdditoinalRounds = SelectedGroup.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Additional).ToList();
        }
    }
}