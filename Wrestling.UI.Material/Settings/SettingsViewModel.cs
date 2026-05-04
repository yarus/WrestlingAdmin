using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.FolderBrowser;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using Wrestling.Entities;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Dashboard;
using Wrestling.UI.Utils;

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
        private ICommand _copyPublicUrlCommand;

        private string _validation;
        private GlobalSettings _subscribedItem;

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

            OnPropertyChanged(nameof(EffectiveBackupFolderHint));
        }

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

        public override string PageTitle => DataContext.Tournament == null ? "Общие Настройки" : "Настройки Турнира";

        public override bool IsBackButtonAvailable => true;

        public GlobalSettings Item { get; set; }

        public string Validation
        {
            get { return _validation; }
            set
            {
                _validation = value;

                OnPropertyChanged("Validation");
            }
        }

        public bool IsAuthenticated
        {
            get { return DataContext.IsAuthenticated; }
            set
            {
                DataContext.IsAuthenticated = value;

                if (value)
                {
                    Validation = string.Empty;
                }

                OnPropertyChanged("IsAuthenticated");
            }
        }

        protected override void OnBackCommand()
        {
            if (DataContext.Tournament == null)
            {
                NavigateToView<HomeViewModel>();
            }
            else
            {
                NavigateToView<DashboardViewModel>();
            }
        }

        private void CheckTeamLogo()
        {
            foreach (var app in DataContext.TeamsCache)
            {
                if (string.IsNullOrEmpty(app.EmblemPath)) continue;
                
                // get file name and check if it exists
                var fileNameItems = app.EmblemPath.Split('\\');
                var fileName = fileNameItems[fileNameItems.Length - 1];

                var storagePath = Path.GetFullPath("Images");

                EnsureUploadFolder(storagePath);

                var fullPath = $"{storagePath}\\{fileName}";

                if (File.Exists(fullPath))
                {
                    app.EmblemPath = fullPath;
                }
            }
        }

        private void EnsureUploadFolder(string folder)
        {
            Directory.CreateDirectory(folder);

            DirectoryInfo dInfo = new DirectoryInfo(folder);

            DirectorySecurity dSecurity = dInfo.GetAccessControl();

            dSecurity.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.FullControl,
                    InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                    PropagationFlags.NoPropagateInherit,
                    AccessControlType.Allow));

            dInfo.SetAccessControl(dSecurity);
        }

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

        private void SetSliderBackground()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = "Открыть файл с изображением",
                InitialDirectory = string.IsNullOrEmpty(Item.SliderBackgroundImagePath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Item.SliderBackgroundImagePath,
                Filter = "Изображения (*.jpg)|*.jpg|All Files (*.*)|*.*"
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
                Title = "Открыть wav файл",
                InitialDirectory = string.IsNullOrEmpty(Item.StartGongSoundPath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Item.StartGongSoundPath,
                Filter = "Звуковой файл (*.wav)|*.wav"
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
                Title = "Открыть wav файл",
                InitialDirectory = string.IsNullOrEmpty(Item.EndGongSoundPath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Item.EndGongSoundPath,
                Filter = "Звуковой файл (*.wav)|*.wav"
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
            var settings = new FolderBrowserDialogSettings
            {
                Description = "Выберите папку для резервных копий",
                ShowNewFolderButton = true,
                SelectedPath = string.IsNullOrWhiteSpace(Item.BackupFolderPath)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : Item.BackupFolderPath
            };

            bool? success = Dialog.ShowFolderBrowserDialog(this, settings);
            if (success == true)
            {
                Item.BackupFolderPath = settings.SelectedPath;
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
                Title = "Выберите изображение печати и подписей",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "Изображения (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Все файлы (*.*)|*.*"
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
                ShowSnackMessage($"Не удалось сохранить изображение: {ex.Message}");
                Item.SignatureFooterImagePath = previousPath;
            }
        }

        #region Network settings

        public string LocalIpAddressesLine
        {
            get
            {
                var list = LocalIpAddressProbe.EnumerateLanAddresses();
                if (list.Count == 0) return "не обнаружены";
                return string.Join(", ", list.Select(ip => ip.ToString()));
            }
        }

        // The override picker is shown only when the operator actually has a
        // choice to make — a single-NIC laptop has no ambiguity and the
        // control would be pure noise. Stale overrides from a previous
        // session also count: keep the picker visible so the operator can
        // see (and clear) a value that no longer matches reality.
        public bool IsAnnounceAddressPickerVisible
        {
            get
            {
                if (LocalIpAddressProbe.EnumerateLanAddresses().Count > 1) return true;
                return Item != null && !string.IsNullOrEmpty(Item.AnnounceIpOverride);
            }
        }

        // ComboBox source: a sentinel "(Авто)" entry plus every IPv4 address
        // found on the machine. Empty selection (the sentinel) means "fall
        // back to LocalIpAddressProbe.PickDefault()" — same behavior the app
        // had before the override was added.
        public const string AnnounceAuto = "(Авто)";

        public IList<string> AnnounceAddressOptions
        {
            get
            {
                var options = new List<string> { AnnounceAuto };
                foreach (var ip in LocalIpAddressProbe.EnumerateLanAddresses())
                {
                    options.Add(ip.ToString());
                }
                // If the stored override no longer matches any current NIC
                // (laptop moved networks, NIC unplugged) keep it visible
                // anyway — otherwise the ComboBox would fail to render the
                // current selection and the operator would be confused about
                // why the override silently went away. PickAnnounceAddress
                // already falls back to auto in this case at runtime.
                var saved = Item?.AnnounceIpOverride;
                if (!string.IsNullOrWhiteSpace(saved) && !options.Contains(saved))
                {
                    options.Add(saved);
                }
                return options;
            }
        }

        // Two-way binding glue: the entity stores empty string for "auto",
        // but the ComboBox needs a non-null SelectedItem to render its label,
        // so we map empty ↔ "(Авто)" here. Setter also re-broadcasts
        // PublicHttpUrl because the displayed URL depends on this value.
        public string SelectedAnnounceAddress
        {
            get
            {
                if (Item == null) return AnnounceAuto;
                return string.IsNullOrEmpty(Item.AnnounceIpOverride) ? AnnounceAuto : Item.AnnounceIpOverride;
            }
            set
            {
                if (Item == null) return;
                Item.AnnounceIpOverride = (string.IsNullOrEmpty(value) || value == AnnounceAuto) ? string.Empty : value;
                OnPropertyChanged(nameof(SelectedAnnounceAddress));
                OnPropertyChanged(nameof(PublicHttpUrl));
                OnPropertyChanged(nameof(IsAnnounceAddressPickerVisible));
            }
        }

        public string PublicHttpUrl
        {
            get
            {
                var t = DataContext?.Tournament;
                if (t == null || !t.ID.HasValue) return string.Empty;
                var server = Resolve<ITournamentHttpServer>();
                if (server == null || !server.ActualPort.HasValue) return string.Empty;
                var ip = LocalIpAddressProbe.PickAnnounceAddress(Item?.AnnounceIpOverride);
                if (IPAddress.IsLoopback(ip)) return string.Empty;
                return "http://" + ip + ":" + server.ActualPort.Value + "/tournament/" + t.ID.Value + ".wrt";
            }
        }

        public ICommand CopyPublicUrlCommand
        {
            get
            {
                if (_copyPublicUrlCommand == null)
                {
                    _copyPublicUrlCommand = new RelayCommand(
                        param => CopyPublicUrl(),
                        param => !string.IsNullOrEmpty(PublicHttpUrl)
                    );
                }
                return _copyPublicUrlCommand;
            }
        }

        private void CopyPublicUrl()
        {
            var url = PublicHttpUrl;
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                Clipboard.SetText(url);
                ShowSnackMessage("Адрес скопирован в буфер обмена.");
            }
            catch
            {
                // Clipboard can transiently fail when another app holds it open;
                // surfacing a snackbar still helps the operator understand what
                // happened without crashing the settings page.
                ShowSnackMessage("Не удалось скопировать — попробуйте ещё раз.");
            }
        }

        #endregion
    }
}