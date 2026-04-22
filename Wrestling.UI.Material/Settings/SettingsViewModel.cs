using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.FolderBrowser;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.ScoreScreen;
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
        private ICommand _copyPublicUrlCommand;
        private ICommand _configureShareCommand;
        private ICommand _disableShareCommand;

        private string _validation;

        public SettingsViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            Item = DataContext.Tournament == null ? Resolve<GlobalSettings>() : DataContext.Tournament.Settings;
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

        public bool IsTournamentScoreInternational
        {
            get { return Item.IsTournamentScoreInternational; }
            set
            {
                Item.IsTournamentScoreInternational = value;
                SetupScoreScreen(value);
                OnPropertyChanged("IsTournamentScoreInternational");
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

        public bool IsAutosaveEnabled
        {
            get { return Item.IsAutosaveEnabled; }
            set
            {
                Item.IsAutosaveEnabled = value;

                if (Item.IsAutosaveEnabled && DataContext.Tournament != null && string.IsNullOrEmpty(DataContext.Tournament.FileName))
                {
                    var settings = new SaveFileDialogSettings
                    {
                        Title = "Сохранить турнир",
                        CheckFileExists = false,
                        OverwritePrompt = true,
                        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        Filter = "Wrestling Tournament (*.wrt)|*.wrt|All Files (*.*)|*.*"
                    };

                    bool? success = Dialog.ShowSaveFileDialog(this, settings);
                    if (success == true)
                    {
                        DataContext.Tournament.Settings.IsAutosaveEnabled = true;

                        var tournService = Resolve<ITournamentsManager>();

                        var result = tournService.SaveToFile(DataContext.Tournament, settings.FileName);
                        ShowSnackMessage(result ? "Турнир сохранен! Автосохранение включено." : "При сохранении произошла ошибка!");

                        if (!result)
                        {
                            Item.IsAutosaveEnabled = false;
                        }
                    }
                }

                OnPropertyChanged("IsAutosaveEnabled");
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

        private void SetupScoreScreen(bool isInternational)
        {
            DiContainer.Remove("ScoreScreen");

            if (isInternational)
            {
                DiContainer.Add(new InternationalScoreScreenView(), "ScoreScreen");
            }
            else
            {
                DiContainer.Add(new ScoreScreenView(), "ScoreScreen");
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

        public string PublicHttpUrl
        {
            get
            {
                var t = DataContext?.Tournament;
                if (t == null || !t.ID.HasValue) return string.Empty;
                var server = Resolve<ITournamentHttpServer>();
                if (server == null || !server.ActualPort.HasValue) return string.Empty;
                var ip = LocalIpAddressProbe.PickDefault();
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

        public ICommand ConfigureShareCommand
        {
            get
            {
                if (_configureShareCommand == null)
                {
                    _configureShareCommand = new RelayCommand(
                        param => ConfigureShare(),
                        param => true
                    );
                }
                return _configureShareCommand;
            }
        }

        // Fires a one-shot Windows share configuration for the folder the
        // operator picks. Shelling out to `net share` via the `runas` verb
        // triggers the UAC prompt so an operator without admin rights can
        // still consent interactively — setting up a share programmatically
        // without a UAC prompt would require service-level privileges we
        // deliberately don't run with.
        private void ConfigureShare()
        {
            var initialPath = GetCurrentTournamentFolder();
            var folderSettings = new FolderBrowserDialogSettings
            {
                Description = "Выберите папку с .wrt для раздачи соседям",
                ShowNewFolderButton = false,
                SelectedPath = initialPath
            };

            bool? ok = Dialog.ShowFolderBrowserDialog(this, folderSettings);
            if (ok != true) return;

            var folder = folderSettings.SelectedPath;
            if (string.IsNullOrEmpty(folder)) return;

            var shareName = SanitizeShareName(Path.GetFileName(folder.TrimEnd('\\', '/')));
            if (string.IsNullOrEmpty(shareName)) shareName = "WrestlingTournament";

            try
            {
                // `pause` keeps the cmd window open so the operator can see
                // whether the share was created, reused, or rejected (a
                // name-collision error is common on re-runs).
                //
                // Resolve the "Everyone" group to its localized name via the
                // well-known SID — a literal "Everyone" fails with system
                // error 1332 on non-English Windows (the group is "Все" on
                // ru-RU). The SID itself is identical across locales.
                var everyone = ResolveEveryoneGroupName();
                var arguments = "/c net share \"" + shareName + "=" + folder + "\" /GRANT:\"" + everyone + "\",READ /REMARK:\"WrestlingAdmin\" & pause";
                var psi = new ProcessStartInfo("cmd.exe", arguments)
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
            }
            catch (Win32Exception ex)
            {
                // Win32 1223 = user denied the UAC prompt. Anything else is a
                // more fundamental failure (shell unavailable, etc.).
                if (ex.NativeErrorCode == 1223)
                {
                    ShowSnackMessage("Настройка общего доступа отменена.");
                }
                else
                {
                    ShowSnackMessage("Не удалось запустить настройку: " + ex.Message);
                }
                return;
            }

            // Prefill SelfUncPath so the operator doesn't have to type it out —
            // even if the share command itself fails or is cancelled in the
            // elevated cmd window, the path is a correct starting guess they
            // can edit. The .wrt leaf is appended only if the currently-open
            // tournament is under the shared folder.
            var ip = LocalIpAddressProbe.PickDefault();
            if (IPAddress.IsLoopback(ip)) return;

            var unc = @"\\" + ip + @"\" + shareName;
            var tournamentFile = DataContext?.Tournament?.FileName;
            if (!string.IsNullOrEmpty(tournamentFile) &&
                tournamentFile.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            {
                var rel = tournamentFile.Substring(folder.Length).TrimStart('\\', '/');
                unc = unc + @"\" + rel;
            }
            Item.SelfUncPath = unc;
            OnPropertyChanged("Item");
        }

        private string GetCurrentTournamentFolder()
        {
            var file = DataContext?.Tournament?.FileName;
            if (!string.IsNullOrEmpty(file))
            {
                try { return Path.GetDirectoryName(file); }
                catch { }
            }
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private static string SanitizeShareName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var clean = Regex.Replace(name, @"[^A-Za-z0-9_\-]", "");
            if (string.IsNullOrEmpty(clean)) return null;
            return clean.Length > 80 ? clean.Substring(0, 80) : clean;
        }

        private static string ResolveEveryoneGroupName()
        {
            try
            {
                var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                var account = (NTAccount)sid.Translate(typeof(NTAccount));
                return account.Value;
            }
            catch
            {
                return "Everyone";
            }
        }

        public ICommand DisableShareCommand
        {
            get
            {
                if (_disableShareCommand == null)
                {
                    _disableShareCommand = new RelayCommand(
                        param => DisableShare(),
                        param => Item != null && !string.IsNullOrEmpty(Item.SelfUncPath)
                    );
                }
                return _disableShareCommand;
            }
        }

        // Symmetrical to ConfigureShare: parses the share name out of the
        // currently-set UNC, runs `net share <name> /DELETE` elevated, and
        // clears the SelfUncPath field regardless. Clearing the field even on
        // OS-level failure is intentional — an operator who hits "отключить"
        // wants the UI state to reflect intent; if the share still exists on
        // Windows they can see the cmd output and retry manually.
        private void DisableShare()
        {
            if (Item == null) return;
            var shareName = TryGetShareNameFromUnc(Item.SelfUncPath);

            if (!string.IsNullOrEmpty(shareName))
            {
                try
                {
                    var arguments = "/c net share \"" + shareName + "\" /DELETE & pause";
                    var psi = new ProcessStartInfo("cmd.exe", arguments)
                    {
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                }
                catch (Win32Exception ex)
                {
                    if (ex.NativeErrorCode == 1223)
                    {
                        ShowSnackMessage("Отключение общего доступа отменено.");
                        return;
                    }
                    ShowSnackMessage("Не удалось запустить отключение: " + ex.Message);
                    return;
                }
            }

            Item.SelfUncPath = string.Empty;
            OnPropertyChanged("Item");
        }

        private static string TryGetShareNameFromUnc(string unc)
        {
            if (string.IsNullOrEmpty(unc)) return null;
            // Expected: \\host\sharename[\optional\path]
            var trimmed = unc.TrimStart('\\', '/');
            var parts = trimmed.Split(new[] { '\\', '/' }, 3);
            if (parts.Length < 2) return null;
            return string.IsNullOrEmpty(parts[1]) ? null : parts[1];
        }

        #endregion
    }
}