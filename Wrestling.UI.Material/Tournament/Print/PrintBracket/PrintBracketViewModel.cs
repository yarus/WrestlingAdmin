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
        private List<PrintWrestlerApplicationViewModel> _groupWrestlers = new List<PrintWrestlerApplicationViewModel>();

        public override string PageTitle => "Печать Протокола";

        // True when this bracket is being rendered as a draw protocol —
        // changes the heading to «Протокол Жеребьевки» and swaps the wrestler
        // table so Жребий replaces Место as the leading column.
        public bool IsDrawProtocol { get; set; }

        public string ProtocolTitle => IsDrawProtocol ? "Протокол Жеребьевки" : "Протокол";

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
            GroupMainRounds = SelectedGroup.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Main).ToList();
            GroupAdditoinalRounds = SelectedGroup.Bracket.Rounds.Where(p => p.RoundType == GroupRoundTypeEnum.Additional).ToList();

            var results = new List<PrintWrestlerApplicationViewModel>();

            // Draw protocol orders by Жребий (SeedNumber); итог orders by Место
            // (FinalPlace) and falls back to SeedNumber/name for ties.
            var wrestlers = IsDrawProtocol
                ? SelectedGroup.Wrestlers.OrderBy(x => x.SeedNumber).ThenBy(x => x.LastFirstName).ToList()
                : SelectedGroup.Wrestlers.OrderBy(x => x.FinalPlace).ThenBy(x => x.SeedNumber).ThenBy(x => x.LastFirstName).ToList();
            
            foreach (var wrestler in wrestlers)
            {
                results.Add(new PrintWrestlerApplicationViewModel
                {
                    Order = wrestler.FinalPlace,
                    SeedNumber = wrestler.SeedNumber,
                    AthleteName = wrestler.LastFirstName,
                    BirthYear = wrestler.BirthDate?.Year,
                    Level = wrestler.Level,
                    TeamName = wrestler.TeamName,
                    TeamCity = wrestler.TeamCity,
                    Weight = wrestler.Weight
                });
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