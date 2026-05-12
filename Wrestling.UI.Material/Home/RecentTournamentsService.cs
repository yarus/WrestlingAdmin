using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wrestling.UI.Material.Theme;

namespace Wrestling.UI.Material.Home
{
    public class RecentTournamentsService : IRecentTournamentsService
    {
        public const int MaxItems = 5;

        private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

        private readonly ILocalUiSettingsStorage _storage;

        public RecentTournamentsService(ILocalUiSettingsStorage storage)
        {
            _storage = storage;
        }

        public IList<string> LoadExisting()
        {
            var snapshot = _storage.Load() ?? new LocalUiSettings();
            var current = snapshot.RecentTournamentFiles ?? new List<string>();

            var surviving = current
                .Where(p => !string.IsNullOrWhiteSpace(p) && SafeExists(p))
                .Distinct(PathComparer)
                .Take(MaxItems)
                .ToList();

            // Persist pruned result so the file stays in sync — but only
            // when something actually changed, to avoid pointless disk writes
            // every time Home is shown.
            if (!ListsEqual(current, surviving))
            {
                snapshot.RecentTournamentFiles = surviving;
                _storage.Save(snapshot);
            }

            return surviving;
        }

        public void Add(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            var snapshot = _storage.Load() ?? new LocalUiSettings();
            var current = snapshot.RecentTournamentFiles ?? new List<string>();

            var updated = new List<string> { fileName };
            updated.AddRange(current.Where(p => !string.IsNullOrWhiteSpace(p)
                                                 && !PathComparer.Equals(p, fileName)));

            if (updated.Count > MaxItems)
            {
                updated = updated.Take(MaxItems).ToList();
            }

            snapshot.RecentTournamentFiles = updated;
            _storage.Save(snapshot);
        }

        // File.Exists already swallows most I/O exceptions and returns false,
        // but UNC / invalid-path edge cases can still throw — guard so a
        // single bad entry never blocks the whole list from loading.
        private static bool SafeExists(string path)
        {
            try { return File.Exists(path); }
            catch { return false; }
        }

        private static bool ListsEqual(IList<string> a, IList<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!PathComparer.Equals(a[i], b[i])) return false;
            }
            return true;
        }
    }
}
