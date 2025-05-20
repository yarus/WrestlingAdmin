using System.Threading.Tasks;

namespace Wrestling.UI.Material.Model
{
    public interface ITournamentImporter
    {
        Task<int> ImportDataFromFileAsync(Entities.Tournament target, string fileName);
    }
}