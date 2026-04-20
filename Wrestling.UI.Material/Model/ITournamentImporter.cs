using System.Threading.Tasks;

namespace Wrestling.UI.Material.Model
{
    public interface ITournamentImporter
    {
        // Load + validate + adapt the remote tournament. Safe to call from any
        // thread — does not touch the target's ObservableCollection<T> graphs.
        // Returns a plan that either short-circuits (nothing to apply) or
        // carries the fully-loaded remote tournament for the apply phase.
        Task<ImportPlan> PrepareAsync(Entities.Tournament target, string fileName);

        // Apply the prepared plan to the target. MUST run on the UI dispatcher
        // since it mutates ObservableCollection<T> entities that have views
        // bound to them and invokes processors that raise INotifyPropertyChanged.
        // Fast: typically touches only the matches that genuinely changed since
        // the last import (~1-5 ms for a normal tick).
        ImportResult Apply(Entities.Tournament target, ImportPlan plan);
    }
}
