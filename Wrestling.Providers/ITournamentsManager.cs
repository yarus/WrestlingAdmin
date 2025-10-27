using System.Threading.Tasks;
using Wrestling.Entities;

namespace Wrestling.Providers
{
    public interface ITournamentsManager
    {
        Tournament LoadFromFile(string fileName);
        Task<Tournament> LoadFromFileAsync(string fileName);
        bool SaveToFile(Tournament item, string fileName);
        Task<bool> SaveToFileAsync(Tournament item, string fileName);
    }
}