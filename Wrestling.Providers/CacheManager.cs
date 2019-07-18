using System.Collections.Generic;
using Wrestling.Data;
using Wrestling.DataAccess;
using Wrestling.Entities;

namespace Wrestling.Providers
{
    public class CacheManager : ICacheManager
    {
        private const string TEAMS_FILE_NAME = "Cache_Teams.json";
        private const string WRESTLERS_FILE_NAME = "Cache_Wrestlers.json";
        private readonly ITeamsDataAccess _teamDa;
        private readonly IWrestlersDataAccess _wrestlersDa;
        private readonly IEntityToInfoAdapter _adapter;

        public CacheManager(ITeamsDataAccess teamDa, IWrestlersDataAccess wrestlersDa, IEntityToInfoAdapter adapter)
        {
            _teamDa = teamDa;
            _wrestlersDa = wrestlersDa;
            _adapter = adapter;
        }

        public bool SaveTeams(List<TeamApplication> list)
        {
            var infoList = new List<TeamApplicationInfo>();

            foreach (var app in list)
            {
                var info = _adapter.GetInfoFromEntity(app);
                infoList.Add(info);
            }

            var result = _teamDa.SaveToFile(infoList, TEAMS_FILE_NAME);
            
            return result;
        }

        public List<TeamApplication> LoadTeams()
        {
            var result = new List<TeamApplication>();

            var info = _teamDa.LoadFromFile(TEAMS_FILE_NAME);

            if (info != null)
            {
                foreach (var item in info)
                {
                    var entity = _adapter.GetEntityFromInfo(item, new List<Wrestler>());
                    result.Add(entity);
                }
            };

            return result;
        }

        public bool SaveWrestlers(List<Wrestler> list)
        {
            var infoList = new List<WrestlerInfo>();

            foreach (var app in list)
            {
                var info = _adapter.GetInfoFromEntity(app);
                infoList.Add(info);
            }

            var result = _wrestlersDa.SaveToFile(infoList, WRESTLERS_FILE_NAME);

            return result;
        }

        public List<Wrestler> LoadWrestlers()
        {
            var result = new List<Wrestler>();

            var info = _wrestlersDa.LoadFromFile(WRESTLERS_FILE_NAME);

            if (info != null)
            {
                foreach (var item in info)
                {
                    var entity = _adapter.GetEntityFromInfo(item);
                    result.Add(entity);
                }
            };

            return result;
        }
    }
}
