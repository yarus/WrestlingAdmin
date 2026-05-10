using System;
using System.Diagnostics;
using System.IO;
using Wrestling.DataAccess;

namespace Wrestling.UI.Material.Theme
{
    public class LocalUiSettingsStorage : ILocalUiSettingsStorage
    {
        private const string FileName = "local_ui_settings.json";
        private const string AppFolder = "WrestlingAdmin";

        private readonly IStorageDataAccess _storage;

        public LocalUiSettingsStorage(IStorageDataAccess storage)
        {
            _storage = storage;
        }

        public LocalUiSettings Load()
        {
            var path = ResolvePath();
            if (string.IsNullOrEmpty(path)) return new LocalUiSettings();

            var loaded = _storage.ReadFromFile<LocalUiSettings>(path);
            return loaded ?? new LocalUiSettings();
        }

        public void Save(LocalUiSettings settings)
        {
            if (settings == null) return;

            var path = ResolvePath();
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                _storage.SaveToFile(settings, path);
            }
            catch (Exception ex) when (IsExpectedIoException(ex))
            {
                // Same tolerance as load — a per-machine pref failing to
                // persist must not crash the app. Operator will reapply
                // their selection on next session if it didn't stick.
                Debug.WriteLine($"LocalUiSettingsStorage.Save failed for {path}: {ex}");
            }
        }

        private static string ResolvePath()
        {
            try
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(root)) return null;
                return Path.Combine(root, AppFolder, FileName);
            }
            catch (Exception ex) when (IsExpectedIoException(ex))
            {
                Debug.WriteLine($"LocalUiSettingsStorage.ResolvePath failed: {ex}");
                return null;
            }
        }

        private static bool IsExpectedIoException(Exception ex)
        {
            return ex is IOException
                   || ex is UnauthorizedAccessException
                   || ex is System.Security.SecurityException
                   || ex is PathTooLongException
                   || ex is ArgumentException
                   || ex is NotSupportedException;
        }
    }
}
