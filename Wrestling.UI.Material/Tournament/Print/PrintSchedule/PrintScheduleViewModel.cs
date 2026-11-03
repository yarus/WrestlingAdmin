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
        private readonly TournamentPart _part;
        private MatStats _stat;
        private List<AgeWeightGroup> _groups;

        public string PageName => T("Nav_Schedule", "Расписание");
        public override string PageTitle => T("PrintSchedule_PageTitle", "Расписание схваток по коврам");
        
        public PrintScheduleViewModel(IDiContainer container, Mat mat, TournamentPart part = null) : base(container)
        {
            _mat = mat;
            _part = part;
        }

        public string MatPartLabel => _part == null ? _mat.Name : _mat.Name + " · " + _part.Name;

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

            // Printing has its own part selection, independent of the part
            // currently running on the mat. Null includes all parts.
            Stat = null;
            _groups = new List<AgeWeightGroup>(DataContext.Tournament.Groups
                .Where(g => g.Bracket != null
                            && g.MatID == _mat.ID
                            && (_part == null || g.PartID == _part.ID)));

            if (_groups.Count == 0) return;

            var parts = Tournament.Parts.Select(p => (Guid?)p.ID).ToList();
            var matches = _groups.GroupBy(g => g.PartID).OrderBy(g => parts.IndexOf(g.Key))
                .SelectMany(groups => groups.SelectMany(g => g.Bracket.Rounds).SelectMany(r => r.RoundMatches)
                    .Where(m => !m.IsMatchCompleted).OrderBy(m => m.MatchNumber)).ToList();

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
                WrestlersCount = _groups.Sum(g => g.Wrestlers.Count),
                GroupsCount = _groups.Count,
                Matches = new ObservableCollection<WrestlingMatch>(matches)//matches.OrderBy(m => m.MatchNumber))
            };
        }
    }
}
