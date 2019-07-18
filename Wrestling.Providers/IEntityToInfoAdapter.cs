using System.Collections.Generic;
using Wrestling.Data;
using Wrestling.Entities;

namespace Wrestling.Providers
{
    public interface IEntityToInfoAdapter
    {
        TeamApplication GetEntityFromInfo(TeamApplicationInfo info, IEnumerable<Wrestler> wrestlers);
        TeamApplicationInfo GetInfoFromEntity(TeamApplication entity);
        Tournament GetEntityFromInfo(TournamentInfo info);
        TournamentInfo GetInfoFromEntity(Tournament item);
        Wrestler GetEntityFromInfo(WrestlerInfo info);
        WrestlerInfo GetInfoFromEntity(Wrestler item);
    }
}