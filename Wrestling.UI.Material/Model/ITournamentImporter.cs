using System.Threading.Tasks;

namespace Wrestling.UI.Material.Model
{
    public interface ITournamentImporter
    {
        Task<ImportResult> ImportDataFromFileAsync(Entities.Tournament target, string fileName);
    }
}
