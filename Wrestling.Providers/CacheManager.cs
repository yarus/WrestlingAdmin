using System;
using System.Collections.Generic;
using System.IO;
using Wrestling.Data;
using Wrestling.DataAccess;
using Wrestling.Entities;

namespace Wrestling.Providers
{
    public class CacheManager : ICacheManager
    {
        private const string TeamsFileName = "Cache_Teams.json";
        private const string WrestlersFileName = "Cache_Wrestlers.json";
        private readonly ITeamsDataAccess _teamDa;
        private readonly IWrestlersDataAccess _wrestlersDa;
        private readonly IEntityToInfoAdapter _adapter;
        private readonly string _cacheDirectory;

        public CacheManager(ITeamsDataAccess teamDa, IWrestlersDataAccess wrestlersDa, IEntityToInfoAdapter adapter)
            : this(teamDa, wrestlersDa, adapter, GetDefaultCacheDirectory())
        {
        }

        public CacheManager(ITeamsDataAccess teamDa, IWrestlersDataAccess wrestlersDa, IEntityToInfoAdapter adapter, string cacheDirectory)
        {
            _teamDa = teamDa;
            _wrestlersDa = wrestlersDa;
            _adapter = adapter;
            _cacheDirectory = cacheDirectory;
            Directory.CreateDirectory(_cacheDirectory);
        }

        // Cache files live under %LocalAppData%/WrestlingAdmin/ so they survive
        // the app being launched from arbitrary working directories (ClickOnce,
        // drag-from-explorer, tests) instead of being dropped in cwd.
        private static string GetDefaultCacheDirectory()
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "WrestlingAdmin");
        }

        private string TeamsPath => Path.Combine(_cacheDirectory, TeamsFileName);
        private string WrestlersPath => Path.Combine(_cacheDirectory, WrestlersFileName);

        public bool SaveTeams(List<TeamApplication> list)
        {
            var infoList = new List<TeamApplicationInfo>();

            foreach (var app in list)
            {
                var info = _adapter.GetInfoFromEntity(app);
                infoList.Add(info);
            }

            return _teamDa.SaveToFile(infoList, TeamsPath);
        }

        public List<TeamApplication> LoadTeams()
        {
            var result = new List<TeamApplication>();

            var info = _teamDa.LoadFromFile(TeamsPath);

            if (info != null)
            {
                foreach (var item in info)
                {
                    var entity = _adapter.GetEntityFromInfo(item);
                    result.Add(entity);
                }
            }

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

            return _wrestlersDa.SaveToFile(infoList, WrestlersPath);
        }

        public List<Wrestler> LoadWrestlers()
        {
            var result = new List<Wrestler>();

            var info = _wrestlersDa.LoadFromFile(WrestlersPath);

            if (info != null)
            {
                foreach (var item in info)
                {
                    var entity = _adapter.GetEntityFromInfo(item);
                    result.Add(entity);
                }
            }

            return result;
        }
    }
}

