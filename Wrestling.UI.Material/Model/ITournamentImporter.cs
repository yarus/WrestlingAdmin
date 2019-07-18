namespace Wrestling.UI.Material.Model
{
    public interface ITournamentImporter
    {
        int ImportDataFromFile(Entities.Tournament target, string fileName);
    }
}