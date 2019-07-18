using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.FolderBrowser;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.Recorder;
using Wrestling.Recorder.DataAccess;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Tournament.Dashboard;
using Wrestling.UI.Material.Utils.Recording.OverlayDrawer;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Settings
{
    public class SettingsViewModel : ViewModelBase
    {
        private ICommand _setVideoStoragePathCommand;
        private ICommand _setSliderBackgroundCommand;
        private ICommand _setStartGongCommand;
        private ICommand _setEndGongCommand;
        private ICommand _playEndGongCommand;
        private ICommand _playStartGongCommand;
        private ICommand _reloadRecConfigCommand;
        private ICommand _openConfiguratorCommand;

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

        public bool IsOverlayOlympic
        {
            get { return Item.IsOverlayOlympic; }
            set
            {
                Item.IsOverlayOlympic = value;
                SetupOverlay(value);
                OnPropertyChanged("IsOverlayOlympic");
            }
        }

        public bool IsVideoRecordingEnabledF
        {
            get { return Item.IsVideoRecordingEnabled; }
            set
            {
                if (value)
                {
                    if (IsConfigValid())
                    {
                        Item.IsVideoRecordingEnabled = true;
                    }
                    else
                    {
                        Dialog.ShowMessageBox(this, "Некорректно выполнена конфигурация видеорегистратора!");
                        Item.IsVideoRecordingEnabled = false;
                    }
                }
                else
                {
                    Item.IsVideoRecordingEnabled = false;
                }

                OnPropertyChanged("IsVideoRecordingEnabledF");
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

        public ICommand ReloadRecConfigCommand
        {
            get
            {
                if (_reloadRecConfigCommand == null)
                {
                    _reloadRecConfigCommand = new RelayCommand(
                        param => ReloadConfig(),
                        param => true
                    );
                }
                return _reloadRecConfigCommand;
            }
        }

        public ICommand OpenConfiguratorCommand
        {
            get
            {
                if (_openConfiguratorCommand == null)
                {
                    _openConfiguratorCommand = new RelayCommand(
                        param => OpenConfigurator(),
                        param => true
                    );
                }
                return _openConfiguratorCommand;
            }
        }

        private void OpenConfigurator()
        {
            Process.Start("Wrestling.Recorder.Configurator.exe");
        }

        public bool IsConfigValid()
        {
            if (string.IsNullOrEmpty(Item.VideoStoragePath)) return false;

            var config = Resolve<RecorderConfiguration>();
            if (config != null)
            {
                return !string.IsNullOrEmpty(config.VideoDeviceID);
            }

            return false;
        }

        public void ReloadConfig()
        {
            var data = GetConfig();
            if (data != null)
            {
                DiContainer.Remove<RecorderConfiguration>();
                DiContainer.Add<RecorderConfiguration>(data);
                Dialog.ShowMessageBox(this, "Конфигурация видеорегистратора успешно обновлена!", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                Dialog.ShowMessageBox(this, "Файл конфигурации видеорегистратора не может быть прочитан!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private RecorderConfiguration GetConfig()
        {
            var da = Resolve<IRecorderConfigurationDataAccess>();
            return da?.LoadFromFile("CamConfig.json");
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

        public ICommand SetVideoStoragePathCommand
        {
            get
            {
                if (_setVideoStoragePathCommand == null)
                {
                    _setVideoStoragePathCommand = new RelayCommand(
                        param => SetVideoStoragePath(),
                        param => true
                    );
                }
                return _setVideoStoragePathCommand;
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

        private void SetupOverlay(bool isOlympic)
        {
            DiContainer.Remove<IOverlayDrawer>();

            if (isOlympic)
            {
                DiContainer.Add<IOverlayDrawer>(new OlympicOverlayDrawer());
            }
            else
            {
                DiContainer.Add<IOverlayDrawer>(new SimpleOverlayDrawer());
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

        private void SetVideoStoragePath()
        {
            var settings = new FolderBrowserDialogSettings
            {
                Description = "Укажите путь к папке для сохранения видео",
                SelectedPath = string.IsNullOrEmpty(Item.VideoStoragePath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Item.VideoStoragePath
            };

            bool? success = Dialog.ShowFolderBrowserDialog(this, settings);
            if (success == true)
            {
                Item.VideoStoragePath = settings.SelectedPath;
            }
        }
    }
}