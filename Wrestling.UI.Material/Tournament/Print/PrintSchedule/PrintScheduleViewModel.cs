using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
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

            // carpet groups
            _groups = new List<AgeWeightGroup>(DataContext.Tournament.Groups.Where(g => g.Bracket != null && g.CarpetLabel == _carpet.Name));

            if (_groups.Count == 0) return;

            var matches = _groups.SelectMany(g => g.Bracket.Rounds).SelectMany(r => r.RoundMatches).OrderBy(m => m.MatchNumber).ToList();

            if (matches.Count == 0) return;

            var cycleMatches = new List<WrestlingMatch>();

            WrestlingMatch previousMatch = null;

            foreach(var match in matches)
            {
                if (previousMatch == null || (match.MatchNumber == (previousMatch.MatchNumber+1)))
                {
                    if (match.IsMatchCanStart)
                    {
                        cycleMatches.Add(match);
                        previousMatch = match;
                        continue;
                    }
                    else if (match.IsMatchCompleted)
                    {
                        previousMatch = match;
                        continue;
                    }                    
                }

                break;
            }

            Stat = new CarpetStats
            {
                CarpetID = _carpet.ID.Value,
                CarpetLabel = _carpet.Name,
                WrestlersCount = _carpet.WrestlersCount,
                GroupsCount = _carpet.Groups.Count,
                Matches = new ObservableCollection<WrestlingMatch>(cycleMatches.OrderBy(m => m.MatchNumber))//matches.OrderBy(m => m.MatchNumber))
            };
        }
    }
}