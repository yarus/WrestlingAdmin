using System;
using System.IO;
using System.Reflection;
using Wrestling.DataAccess;

namespace Wrestling.UI.Material.Model
{
    public sealed class LocalUiSettingsService : ILocalUiSettingsService
    {
        private readonly IStorageDataAccess _storage;
        private readonly object _lock = new object();
        private LocalUiSettingsDto _cache;

        public LocalUiSettingsService(IStorageDataAccess storage)
        {
            _storage = storage;
        }

        public Guid? LoadCarpetId()
        {
            var dto = Load();
            if (string.IsNullOrEmpty(dto.Phase5SelectedCarpetId)) return null;
            return Guid.TryParse(dto.Phase5SelectedCarpetId, out var id) ? id : (Guid?)null;
        }

        public void SaveCarpetId(Guid? carpetId)
        {
            Mutate(dto => dto.Phase5SelectedCarpetId = carpetId?.ToString("D") ?? string.Empty);
        }

        public bool LoadIsBracketsView() => Load().Phase5IsBracketsView;

        public void SaveIsBracketsView(bool isBracketsView)
        {
            Mutate(dto => dto.Phase5IsBracketsView = isBracketsView);
        }

        private LocalUiSettingsDto Load()
        {
            lock (_lock)
            {
                if (_cache != null) return _cache;

                var path = GetSettingsPath();
                var loaded = _storage.ReadFromFile<LocalUiSettingsDto>(path);
                _cache = loaded ?? new LocalUiSettingsDto();
                return _cache;
            }
        }

        private void Mutate(Action<LocalUiSettingsDto> change)
        {
            lock (_lock)
            {
                var dto = Load();
                change(dto);
                try
                {
                    _storage.SaveToFile(dto, GetSettingsPath());
                }
                catch
                {
                    // Best-effort: a sticky-setting save failure must never crash
                    // the app. Worst case the carpet selection won't persist.
                }
            }
        }

        private static string GetSettingsPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appName = Assembly.GetExecutingAssembly().GetName().Name;
            var dir = Path.Combine(appData, appName);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "local_ui_settings.json");
        }

        // Public for tests + serializer; behaviorless DTO. New fields use
        // safe defaults via the parameterless ctor (Newtonsoft overlays JSON
        // on top), so older settings files keep loading.
        public sealed class LocalUiSettingsDto
        {
            public string Phase5SelectedCarpetId { get; set; } = string.Empty;
            public bool Phase5IsBracketsView { get; set; }
        }
    }
}
