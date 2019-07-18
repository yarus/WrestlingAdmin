using System.Collections.Generic;
using Wrestling.Data;

namespace Wrestling.DataAccess
{
    public class TeamsDataAccess : ITeamsDataAccess
    {
        private readonly IStorageDataAccess _storageDataAccess;

        public TeamsDataAccess(IStorageDataAccess storageDataAccess)
        {
            _storageDataAccess = storageDataAccess;
        }

        public bool SaveToFile(List<TeamApplicationInfo> list, string fileName)
        {
            return _storageDataAccess.SaveToFile(list, fileName);
        }

        public List<TeamApplicationInfo> LoadFromFile(string fileName)
        {
            return _storageDataAccess.ReadFromFile<List<TeamApplicationInfo>>(fileName);
        }
    }
}
