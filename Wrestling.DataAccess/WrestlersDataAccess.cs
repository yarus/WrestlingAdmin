using System.Collections.Generic;
using Wrestling.Data;

namespace Wrestling.DataAccess
{
    public class WrestlersDataAccess : IWrestlersDataAccess
    {
        private readonly IStorageDataAccess _storageDataAccess;

        public WrestlersDataAccess(IStorageDataAccess storageDataAccess)
        {
            _storageDataAccess = storageDataAccess;
        }

        public bool SaveToFile(List<WrestlerInfo> list, string fileName)
        {
            return _storageDataAccess.SaveToFile(list, fileName);
        }

        public List<WrestlerInfo> LoadFromFile(string fileName)
        {
            return _storageDataAccess.ReadFromFile<List<WrestlerInfo>>(fileName);
        }
    }
}
