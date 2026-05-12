using System.Collections.Generic;

namespace Wrestling.UI.Material.Home
{
    public interface IRecentTournamentsService
    {
        // Reads the persisted list, drops entries whose file no longer exists
        // on disk, persists the pruned result, returns the surviving paths
        // newest-first (capped at the implementation's MaxItems).
        IList<string> LoadExisting();

        // Adds (or moves to front) a full file path. Dedupes case-insensitively
        // (Windows path semantics), caps at MaxItems, persists.
        void Add(string fileName);
    }
}
