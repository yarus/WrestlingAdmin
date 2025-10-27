using System.Threading;
using System.Threading.Tasks;
using Wrestling.DataAccess;
using Wrestling.Entities;

namespace Wrestling.Providers
{
    public class TournamentsManager : ITournamentsManager
    {
        private readonly ITournamentDataAccess _dataAccess;
        private readonly IEntityToInfoAdapter _adapter;
        
        public TournamentsManager(ITournamentDataAccess dataAccess, IEntityToInfoAdapter adapter)
        {
            _dataAccess = dataAccess;
            _adapter = adapter;
        }

        public Tournament LoadFromFile(string fileName)
        {
            var info = _dataAccess.LoadFromFile(fileName);

            if (info == null) return null;

            var entity = _adapter.GetEntityFromInfo(info);
            entity.FileName = fileName;

            return entity;
        }

        public async Task<Tournament> LoadFromFileAsync(string fileName)
        {
            var info = await _dataAccess.LoadFromFileAsync(fileName);

            if (info == null) return null;

            var entity = _adapter.GetEntityFromInfo(info);
            if (entity == null) return null;

            entity.FileName = fileName;

            return entity;
        }
        
        public async Task<bool> SaveToFileAsync(Tournament item, string fileName)
        {
            var info = _adapter.GetInfoFromEntity(item);
            var result = await _dataAccess.SaveToFileAsync(info, fileName);
            if (result)
            {
                item.FileName = fileName;
            }
            return result;
        }

        public bool SaveToFile(Tournament item, string fileName)
        {
            var info = _adapter.GetInfoFromEntity(item);
            var result = _dataAccess.SaveToFile(info, fileName);
            if (result)
            {
                item.FileName = fileName;
            }
            return result;
        }
    }
}