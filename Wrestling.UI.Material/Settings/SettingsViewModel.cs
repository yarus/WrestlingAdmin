using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows.Input;
using Wrestling.UI.Material.Utils;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Theme;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Settings
{
    public class SettingsViewModel : ViewModelBase
    {
        private ICommand _setSliderBackgroundCommand;
        private ICommand _setStartGongCommand;
        private ICommand _setEndGongCommand;
        private ICommand _playEndGongCommand;
        private ICommand _playStartGongCommand;
        private ICommand _browseBackupFolderCommand;
        private ICommand _browseSignatureFooterImageCommand;
        private ICommand _removeSignatureFooterImageCommand;

        private GlobalSettings _subscribedItem;
        private ILocalizationService _localization;
        private ILocalUiSettingsStorage _localUiStorage;

        public SettingsViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            if (_subscribedItem != null)
            {
                _subscribedItem.PropertyChanged -= OnItemPropertyChanged;
                _subscribedItem = null;
            }

            Item = DataContext.Tournament == null ? Resolve<GlobalSettings>() : DataContext.Tournament.Settings;

            if (Item != null)
            {
                Item.PropertyChanged += OnItemPropertyChanged;
                _subscribedItem = Item;
            }

            if (ThemeManager == null)
            {
                ThemeManager = Resolve<IThemeManager>();
                OnPropertyChanged(nameof(ThemeManager));
            }

            if (_localization == null)
            {
                _localization = Resolve<ILocalizationService>();
                _localUiStorage = Resolve<ILocalUiSettingsStorage>();
                OnPropertyChanged(nameof(AvailableLanguages));
                OnPropertyChanged(nameof(SelectedLanguage));
            }

            OnPropertyChanged(nameof(EffectiveBackupFolderHint));
        }

        public IReadOnlyList<LanguageDescriptor> AvailableLanguages =>
            _localization?.AvailableLanguages ?? new List<LanguageDescriptor>();

        // Two-way bound to the ComboBox. Setter applies the language live and
        // persists the choice into local_ui_settings.json so the next launch
        // starts in the picked language.
        public LanguageDescriptor SelectedLanguage
        {
            get
            {
                if (_localization == null) return null;
                var current = _localization.CurrentLanguage;
                foreach (var lang in _localization.AvailableLanguages)
                {
                    if (string.Equals(lang.Code, current, StringComparison.OrdinalIgnoreCase)) return lang;
                }
                return null;
            }
            set
            {
                if (value == null || _localization == null) return;
                if (string.Equals(value.Code, _localization.CurrentLanguage, StringComparison.OrdinalIgnoreCase)) return;

                if (_localization.SetLanguage(value.Code) && _localUiStorage != null)
                {
                    var snapshot = _localUiStorage.Load();
                    snapshot.LanguageCode = value.Code;
                    _localUiStorage.Save(snapshot);
                }

                OnPropertyChanged(nameof(SelectedLanguage));
                OnPropertyChanged(nameof(PageTitle));
            }
        }

        // Exposed for the «Внешний вид» settings card. The theme manager is
        // an app-level singleton: setting IsDark / SelectedPrimary on it
        // immediately swaps the live palette AND persists to
        // local_ui_settings.json. No save button needed.
        public IThemeManager ThemeManager { get; private set; }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GlobalSettings.BackupFolderPath))
            {
                OnPropertyChanged(nameof(EffectiveBackupFolderHint));
            }
        }

        // Shows the operator the actual folder backups will be written into,
        // mirroring TournamentDataAccess.GetBackupFolder root resolution:
        // empty/whitespace path → <tournament-dir>/Backups, relative path →
        // resolved against tournament dir, absolute path → used as-is. Empty
        // when no tournament is open (general Settings, nothing to resolve).
        public string EffectiveBackupFolderHint
        {
            get
            {
                var fileName = DataContext?.Tournament?.FileName;
                if (string.IsNullOrEmpty(fileName)) return string.Empty;
                var dir = Path.GetDirectoryName(fileName) ?? string.Empty;
                var configured = Item?.BackupFolderPath;
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    return Path.IsPathRooted(configured) ? configured : Path.Combine(dir, configured);
                }
                return Path.Combine(dir, "Backups");
            }
        }

        public override string PageTitle => _localization == null
            ? "Настройки"
            : _localization.T("Settings_PageTitle");

        public GlobalSettings Item { get; set; }

        public ICommand PlayStartGongCommand
        {
            get
            {
                if (_playStartGongCommand == null)
                {
                    _playStartGongCommand = new RelayCommand(
                        param => PlayStartGong(),
                        param => true
                    );
                }
                return _playStartGongCommand;
            }
        }

        public ICommand PlayEndGongCommand
        {
            get
            {
                if (_playEndGongCommand == null)
                {
                    _playEndGongCommand = new RelayCommand(
                        param => PlayEndGong(),
                        param => true
                    );
                }
                return _playEndGongCommand;
            }
        }

        public ICommand SetStartGongCommand
        {
            get
            {
                if (_setStartGongCommand == null)
                {
                    _setStartGongCommand = new RelayCommand(
                        param => SetStartGong(),
                        param => true
                    );
                }
                return _setStartGongCommand;
            }
        }

        public ICommand SetEndGongCommand
        {
            get
            {
                if (_setEndGongCommand == null)
                {
                    _setEndGongCommand = new RelayCommand(
                        param => SetEndGong(),
                        param => true
                    );
                }
                return _setEndGongCommand;
            }
        }

        public ICommand SetSliderBackgroundCommand
        {
            get
            {
                if (_setSliderBackgroundCommand == null)
                {
                    _setSliderBackgroundCommand = new RelayCommand(
                        param => SetSliderBackground(),
                        param => true
                    );
                }
                return _setSliderBackgroundCommand;
            }
        }

        private static string T(string key, string fallback)
        {
            var value = LocalizationService.Instance?.T(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }

        private void SetSliderBackground()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = T("OpenImage_DialogTitle", "Открыть файл с изображением"),
                InitialDirectory = string.IsNullOrEmpty(Item.SliderBackgroundImagePath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Item.SliderBackgroundImagePath,
                Filter = T("ImageFilter", "Изображения (*.jpg)|*.jpg|All Files (*.*)|*.*")
            };

            bool? success = Dialog.ShowOpenFileDialog(this, settings);
            if (success == true)
            {
                Item.SliderBackgroundImagePath = settings.FileName;
            }
        }

        private void PlayStartGong()
        {
            if (File.Exists(Item.StartGongSoundPath))
            {
                SoundPlayer sp = new SoundPlayer(Item.StartGongSoundPath);
                sp.Play();
            }
        }

        private void PlayEndGong()
        {
            if (File.Exists(Item.EndGongSoundPath))
            {
                SoundPlayer sp = new SoundPlayer(Item.EndGongSoundPath);
                sp.Play();
            }
        }

        private void SetStartGong()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = T("OpenWav_DialogTitle", "Открыть wav файл"),
                InitialDirectory = string.IsNullOrEmpty(Item.StartGongSoundPath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Item.StartGongSoundPath,
                Filter = T("WavFilter", "Звуковой файл (*.wav)|*.wav")
            };

            bool? success = Dialog.ShowOpenFileDialog(this, settings);
            if (success == true)
            {
                Item.StartGongSoundPath = settings.FileName;
            }
        }

        private void SetEndGong()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = T("OpenWav_DialogTitle", "Открыть wav файл"),
                InitialDirectory = string.IsNullOrEmpty(Item.EndGongSoundPath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Item.EndGongSoundPath,
                Filter = T("WavFilter", "Звуковой файл (*.wav)|*.wav")
            };

            bool? success = Dialog.ShowOpenFileDialog(this, settings);
            if (success == true)
            {
                Item.EndGongSoundPath = settings.FileName;
            }
        }

        public ICommand BrowseBackupFolderCommand
        {
            get
            {
                if (_browseBackupFolderCommand == null)
                {
                    _browseBackupFolderCommand = new RelayCommand(
                        param => BrowseBackupFolder(),
                        param => true
                    );
                }
                return _browseBackupFolderCommand;
            }
        }

        private void BrowseBackupFolder()
        {
            var initial = string.IsNullOrWhiteSpace(Item.BackupFolderPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : Item.BackupFolderPath;

            var folder = FolderPicker.PickFolder(
                T("Backup_FolderPicker_Title", "Выберите папку для резервных копий"),
                initial);
            if (!string.IsNullOrEmpty(folder))
            {
                Item.BackupFolderPath = folder;
                OnPropertyChanged("Item");
            }
        }

        public ICommand BrowseSignatureFooterImageCommand
        {
            get
            {
                if (_browseSignatureFooterImageCommand == null)
                {
                    _browseSignatureFooterImageCommand = new RelayCommand(
                        param => BrowseSignatureFooterImage(),
                        param => true);
                }
                return _browseSignatureFooterImageCommand;
            }
        }

        public ICommand RemoveSignatureFooterImageCommand
        {
            get
            {
                if (_removeSignatureFooterImageCommand == null)
                {
                    _removeSignatureFooterImageCommand = new RelayCommand(
                        param =>
                        {
                            Item.SignatureFooterImagePath = null;
                            OnPropertyChanged("Item");
                        },
                        param => !string.IsNullOrEmpty(Item?.SignatureFooterImagePath));
                }
                return _removeSignatureFooterImageCommand;
            }
        }

        private void BrowseSignatureFooterImage()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = T("Signature_PickerTitle", "Выберите изображение печати и подписей"),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = T("Signature_PickerFilter", "Изображения (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Все файлы (*.*)|*.*")
            };

            bool? success = Dialog.ShowOpenFileDialog(this, settings);
            if (success != true) return;

            // Copy the user-selected file into the app's Images/ folder and
            // store only its filename in GlobalSettings. Same pattern as
            // EmblemPath: keeps the .wrt portable across user-folder layouts
            // on the same machine, and lets the path round-trip through save/
            // load without absolute-path drift. Cross-machine transfer still
            // requires copying the Images/ folder, but that's consistent with
            // how team emblems already behave.
            var previousPath = Item.SignatureFooterImagePath;
            try
            {
                var imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
                Directory.CreateDirectory(imagesDir);

                var fileName = Path.GetFileName(settings.FileName);
                var targetPath = Path.Combine(imagesDir, fileName);

                File.Copy(settings.FileName, targetPath, true);

                Item.SignatureFooterImagePath = fileName;
                OnPropertyChanged("Item");
            }
            catch (Exception ex)
            {
                ShowSnackMessage(string.Format(T("Snack_SaveImageError", "Не удалось сохранить изображение: {0}"), ex.Message));
                Item.SignatureFooterImagePath = previousPath;
            }
        }

    }
}