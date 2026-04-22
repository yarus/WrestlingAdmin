using System;
using System.Collections.Generic;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    public class DataContext : IDataContext
    {
        private Entities.Tournament _tournament;

        public WrestlingMatch WrestlingMatch { get; set; }

        public Entities.Tournament Tournament
        {
            get { return _tournament; }
            set
            {
                if (object.ReferenceEquals(_tournament, value)) return;
                _tournament = value;
                TournamentChanged?.Invoke(this, value);
            }
        }

        public TeamApplicationViewModel Team { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool IsBracketView { get; set; }
        public List<Wrestler> WrestlersCache { get; set; }
        public List<TeamApplication> TeamsCache { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public AgeWeightGroup Group { get; set; }

        public event EventHandler<Entities.Tournament> TournamentChanged;

        public DataContext()
        {
            WrestlersCache = new List<Wrestler>();
            TeamsCache = new List<TeamApplication>();
        }
    }
}
