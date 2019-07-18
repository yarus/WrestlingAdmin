using Wrestling.Data;

namespace Wrestling.DataAccess
{
    public class MatchDataAccess : IMatchDataAccess
    {
        private readonly IStorageDataAccess _storageDataAccess;

        public MatchDataAccess(IStorageDataAccess storageDataAccess)
        {
            _storageDataAccess = storageDataAccess;
        }

        public bool SaveToFile(TournamentMatchInfo item, string fileName)
        {
            return _storageDataAccess.SaveToFile(item, fileName);
        }

        public TournamentMatchInfo LoadFromFile(string fileName)
        {
            return _storageDataAccess.ReadFromFile<TournamentMatchInfo>(fileName);
        }
    }
}