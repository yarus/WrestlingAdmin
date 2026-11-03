using System;
using System.IO;
using System.Security;
using Wrestling.DataAccess;
using Wrestling.Entities;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Model
{
    // Only the human-readable name persists. Addresses and ports belong to a running session.
    public sealed class LocalNetworkIdentity : ObservableObject
    {
        private readonly IStorageDataAccess _storage;
        private readonly string _fileName;
        private string _nodeName;
        private string _saveError;

        public sealed class Info { public string NodeName { get; set; } }

        public LocalNetworkIdentity(IStorageDataAccess storage, string fileName = null)
        {
            _storage = storage;
            _fileName = fileName ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WrestlingAdmin", "network_identity.json");
            var saved = _storage?.ReadFromFile<Info>(_fileName);
            _nodeName = string.IsNullOrWhiteSpace(saved?.NodeName) ? MachineName : saved.NodeName.Trim();
        }

        public string MachineName => Environment.MachineName;

        public string NodeName
        {
            get => _nodeName;
            set
            {
                var name = string.IsNullOrWhiteSpace(value) ? MachineName : value.Trim();
                if (_nodeName == name) return;
                _nodeName = name;
                _saveError = null;
                try
                {
                    if (_storage != null)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_fileName)));
                        if (!_storage.SaveToFile(new Info { NodeName = name }, _fileName))
                            _saveError = SaveFailedMessage();
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException
                    or ArgumentException or NotSupportedException)
                {
                    FileLogger.Log("NetworkIdentity.save", _fileName, ex);
                    _saveError = SaveFailedMessage();
                }
                OnPropertyChanged(nameof(SaveError));
                OnPropertyChanged(nameof(NodeName));
            }
        }

        public string SaveError => _saveError;

        private static string SaveFailedMessage()
        {
            const string key = "Network_NameSaveFailed";
            var localized = LocalizationService.Instance?.T(key);
            return string.IsNullOrEmpty(localized) || localized == key
                ? "Имя изменено на этот запуск, но не сохранено на компьютере. Проверьте доступ к локальным настройкам." : localized;
        }
    }
}
