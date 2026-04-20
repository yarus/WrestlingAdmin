using System;
using System.IO;
using System.Media;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.FolderBrowser;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Entities;
using Wrestling.Providers;
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
    }
}