using System;
using System.Collections.Generic;
using System.Linq;

namespace Wrestling.Entities.Results
{
    public class TournamentTeamResult
    {
        public TournamentTeamResult(Guid teamID, string teamName, List<TournamentResult> wrestlers)
        {
            TeamID = teamID;
            TeamName = teamName;

            Wrestlers = wrestlers.Where(w => w.Wrestler != null && w.Wrestler.TeamID.HasValue && w.Wrestler.TeamID == teamID).ToList();
        }

        public Guid TeamID { get; }

        public string TeamName { get; }

        public List<TournamentResult> Wrestlers { get; }

        public int GroupCount => Wrestlers.Select(g => g.GroupName).ToList().Distinct().Count();
        public int GoldMedals => Wrestlers.Count(w => w.Wrestler.FinalPlace.HasValue && w.Wrestler.FinalPlace.Value == 1);
        public int SilverMedals => Wrestlers.Count(w => w.Wrestler.FinalPlace.HasValue && w.Wrestler.FinalPlace.Value == 2);
        public int BronzeMedals => Wrestlers.Count(w => w.Wrestler.FinalPlace.HasValue && w.Wrestler.FinalPlace.Value == 3);
        public int TotalPoints => Wrestlers.Sum(w => w.TotalPoints);
        public int TotalWinsCount => Wrestlers.Sum(w => w.Wins);
        public int TotalAutoWins => Wrestlers.Sum(w => w.AutoWinsCount);
        public int TotalMedals => GoldMedals + SilverMedals + BronzeMedals;
    }
}