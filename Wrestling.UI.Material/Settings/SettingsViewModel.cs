using System;
using System.IO;
using System.Media;
using System.Net;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows.Controls;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Entities;
using Wrestling.Integration;
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
        private ICommand _loadIntegrationDataCommand;

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
                        DataContext.Tournament.Settings.AutosaveMaxSecond = GlobalSettings.AutosaveMaxSecond;

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

        public ICommand LoadIntegrationDataCommand
        {
            get
            {
                if (_loadIntegrationDataCommand == null)
                {
                    _loadIntegrationDataCommand = new RelayCommand(
                        param => LoadIntegrationData((param as PasswordBox)?.Password ?? string.Empty),
                        param => true
                    );
                }
                return _loadIntegrationDataCommand;
            }
        }
        
        public void LoadIntegrationData(string password)
        {
            if (string.IsNullOrEmpty(Item.IntegrationUserName))
            {
                IsAuthenticated = false;
                Validation = "Ввведите имя пользователя";
                return;
            }            

            if (string.IsNullOrEmpty(password))
            {
                IsAuthenticated = false;
                Validation = "Ввведите пароль";
                return;
            }

            Item.IntegrationPassword = password;

            var api = DiContainer.Resolve<IRosbosApi>();
            var cache = DiContainer.Resolve<ICacheManager>();

            if (!VerifyLogin(api, Item.IntegrationUserName, Item.IntegrationPassword))
            {
                IsAuthenticated = false;
                Validation = "Неправильные данные";
                return;
            }

            IsAuthenticated = true;

            UpdateCache(api, cache);

            InitDataContextWithCache(cache);
        }

        private void InitDataContextWithCache(ICacheManager cache)
        {
            if (cache != null && (DataContext.WrestlersCache == null || DataContext.WrestlersCache.Count == 0 || DataContext.TeamsCache == null || DataContext.TeamsCache.Count == 0))
            {
                DataContext.WrestlersCache = cache.LoadWrestlers();
                DataContext.TeamsCache = cache.LoadTeams();
            }
        }

        private void UpdateCache(IRosbosApi api, ICacheManager cache)
        {
            var teams = api.GetTeams();

            foreach (var team in teams)
            {
                if (DataContext.TeamsCache.Find(x => team.HashTag == x.HashTag) == null)
                {
                    DataContext.TeamsCache.Add(team);
                }
            }

            var wrestlers = api.GetWrestlers();
            
            foreach (var wrestler in wrestlers)
            {
                if (DataContext.WrestlersCache.Find(x => wrestler.HashTag == x.HashTag) == null)
                {
                    DataContext.WrestlersCache.Add(wrestler);
                }
            }

            CheckTeamLogo();

            if (cache != null)
            {
                cache.SaveTeams(teams);
                cache.SaveWrestlers(wrestlers);
            }
        }

        private void CheckTeamLogo()
        {
            foreach (var app in DataContext.TeamsCache)
            {
                if (!string.IsNullOrEmpty(app.EmblemPath))
                {
                    // get file name and check if it exists
                    var fileNameItems = app.EmblemPath.Split('\\');
                    var fileName = fileNameItems[fileNameItems.Length - 1];

                    var storagePath = Path.GetFullPath("Images");

                    EnsureUploadFolder(storagePath);

                    var fullPath = $"{storagePath}\\{fileName}";

                    if (!File.Exists(fullPath))
                    {
                        using (WebClient client = new WebClient())
                        {
                            client.DownloadFile($"https://rosbos.ru/{app.EmblemPath}", fullPath);
                        }
                    }

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

        private bool VerifyLogin(IRosbosApi api, string userName, string password)
        {
            api.SetCredentials(userName, password);

            var token = api.LoadToken();

            if (!token)
            {
                return false;
            }

            return true;
        }

        public void ReloadConfig()
        {
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
    }
}