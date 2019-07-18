using Wrestling.Data;

namespace Wrestling.DataAccess
{
    public interface IMatchDataAccess
    {
        TournamentMatchInfo LoadFromFile(string fileName);
        bool SaveToFile(TournamentMatchInfo item, string fileName);
    }
}