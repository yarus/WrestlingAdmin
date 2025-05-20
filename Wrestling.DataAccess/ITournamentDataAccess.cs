using System.Threading.Tasks;
using Wrestling.Data;

namespace Wrestling.DataAccess
{
    public interface ITournamentDataAccess
    {
        TournamentInfo LoadFromFile(string fileName);
        Task<TournamentInfo> LoadFromFileAsync(string fileName);
        bool SaveToFile(TournamentInfo item, string fileName);
        Task<bool> SaveToFileAsync(TournamentInfo item, string fileName);
    }
}