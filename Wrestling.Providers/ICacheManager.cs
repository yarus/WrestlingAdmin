using System.Collections.Generic;
using Wrestling.Entities;

namespace Wrestling.Providers
{
    public interface ICacheManager
    {
        List<TeamApplication> LoadTeams();
        List<Wrestler> LoadWrestlers();
        bool SaveTeams(List<TeamApplication> list);
        bool SaveWrestlers(List<Wrestler> list);
    }
}