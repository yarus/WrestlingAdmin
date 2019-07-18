using System.Threading.Tasks;
using Wrestling.Data;

namespace Wrestling.DataAccess
{
    public class TournamentDataAccess : ITournamentDataAccess
    {
        private readonly IStorageDataAccess _storageDataAccess;

        public TournamentDataAccess(IStorageDataAccess storageDataAccess)
        {
            _storageDataAccess = storageDataAccess;
        }

        public bool SaveToFile(TournamentInfo item, string fileName)
        {
            return _storageDataAccess.SaveToFile(item, fileName);
        }

        public async Task<bool> SaveToFileAsync(TournamentInfo item, string fileName)
        {
            return await _storageDataAccess.SaveToFileAsync(item, fileName);
        }
        
        public TournamentInfo LoadFromFile(string fileName)
        {
            return _storageDataAccess.ReadFromFile<TournamentInfo>(fileName);
        }
    }
}