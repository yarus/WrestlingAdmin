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
        private Mat _mat;
        private MatStats _stat;
        private List<AgeWeightGroup> _groups;

        public string PageName => T("Nav_Schedule", "Расписание");
        public override string PageTitle => T("PrintSchedule_PageTitle", "Расписание схваток по коврам");
        
        public PrintScheduleViewModel(IDiContainer container, Mat mat) : base(container)
        {
            _mat = mat;
        }

        public override bool IsBackButtonAvailable => true;

        public MatStats Stat
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

            // Per-mat active-part filter: a single printout is one schedule
            // for one mat at one point in time. The mat's ActivePartID picks
            // which part's matches are listed; legacy single-part tournaments
            // include all groups because every group's PartID matches.
            _groups = new List<AgeWeightGroup>(DataContext.Tournament.Groups
                .Where(g => g.Bracket != null
                            && g.MatID == _mat.ID
                            && (!_mat.ActivePartID.HasValue || g.PartID == _mat.ActivePartID.Value)));

            if (_groups.Count == 0) return;

            var matches = _groups.SelectMany(g => g.Bracket.Rounds).SelectMany(r => r.RoundMatches).Where(rm => !rm.IsMatchCompleted).OrderBy(m => m.MatchNumber).ToList();

            if (matches.Count == 0) return;

            /*
            var cycleMatches = new List<WrestlingMatch>();

            WrestlingMatch previousMatch = null;

            List<Guid> _pathedGroups = new List<Guid>();
            Guid? currentGroupId = null;

            foreach(var match in matches)
            {
                if (previousMatch == null || (match.MatchNumber == (previousMatch.MatchNumber+1)))
                {
                    if (match.IsMatchCanStart)
                    {
                        if (!currentGroupId.HasValue)
                        {
                            currentGroupId = match.GroupID;
                        }

                        if (match.GroupID != currentGroupId.Value)
                        {
                            if (_pathedGroups.Contains(match.GroupID))
                            {
                                break;
                            }

                            _pathedGroups.Add(currentGroupId.Value);
                            currentGroupId = match.GroupID;
                        }


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
            */

            Stat = new MatStats
            {
                MatID = _mat.ID.Value,
                MatLabel = _mat.Name,
                WrestlersCount = _mat.WrestlersCount,
                GroupsCount = _mat.Groups.Count,
                Matches = new ObservableCollection<WrestlingMatch>(matches)//matches.OrderBy(m => m.MatchNumber))
            };
        }
    }
}