using System.Collections.Generic;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    public class DataContext : IDataContext
    {
        public WrestlingMatch WrestlingMatch { get; set; }
        public Entities.Tournament Tournament { get; set; }
        public TeamApplication Team { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool IsBracketView { get; set; }
        public List<Wrestler> WrestlersCache { get; set; }
        public List<TeamApplication> TeamsCache { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public AgeWeightGroup Group { get; set; }
        public DataContext()
        {
            WrestlersCache = new List<Wrestler>();
            TeamsCache = new List<TeamApplication>();
        }
    }
}