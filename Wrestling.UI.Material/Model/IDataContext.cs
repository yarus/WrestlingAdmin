using System.Collections.Generic;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Model
{
    public interface IDataContext
    {
        AgeWeightGroup Group { get; set; }
        WrestlingMatch WrestlingMatch { get; set; }
        Entities.Tournament Tournament { get; set; }
        TeamApplicationViewModel Team { get; set; }
        bool IsAuthenticated { get; set; }
        bool IsBracketView { get; set; }

        List<Wrestler> WrestlersCache { get; set; }
        List<TeamApplication> TeamsCache { get; set; }
        string UserName { get; set; }
        string Password { get; set; }
    }
}