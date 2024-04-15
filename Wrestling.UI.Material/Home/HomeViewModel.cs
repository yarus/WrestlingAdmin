using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.UI.Material.Match;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Settings;
using Wrestling.UI.Material.Tournament.Dashboard;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Home
{
    public class HomeViewModel : ViewModelBase
    {
        #region Fields

        private ITournamentsManager _tournManager;
        private IList<CommandButtonItem> _drawerItems;

        private ICommand _newTournamentCommand;
        private ICommand _openTournamentCommand;
        private ICommand _openSettingsCommand;
        private ICommand _newQuickMatchCommand;

        #endregion

        public HomeViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            _tournManager = Resolve<ITournamentsManager>();
            
            var cache = DiContainer.Resolve<ICacheManager>();
            if (cache != null && (DataContext.WrestlersCache == null || DataContext.WrestlersCache.Count == 0 || DataContext.TeamsCache == null || DataContext.TeamsCache.Count == 0))
            {
                DataContext.WrestlersCache = cache.LoadWrestlers();
                DataContext.TeamsCache = cache.LoadTeams();
            }
        }

        public override string PageTitle => "Вольная борьба - Администратор турниров v20240415";

        public override IList<CommandButtonItem> DrawerItems
        {
            get
            {
                return _drawerItems ?? (_drawerItems = new List<CommandButtonItem>
                {
                    new CommandButtonItem("Настройки", new RelayCommand(param => OpenSettings(), param => true)),
                    new CommandButtonItem("Выйти", new RelayCommand(param => CloseApp(), param => true))
                });
            }
        }

        #region Commands

        public ICommand NewQuickMatchCommand
        {
            get
            {
                if (_newQuickMatchCommand == null)
                {
                    _newQuickMatchCommand = new RelayCommand(
                        param => NewQuickMatch(),
                        param => true
                    );
                }
                return _newQuickMatchCommand;
            }
        }
        
        public ICommand OpenSettingsCommand
        {
            get
            {
                if (_openSettingsCommand == null)
                {
                    _openSettingsCommand = new RelayCommand(
                        param => OpenSettings(),
                        param => true
                    );
                }
                return _openSettingsCommand;
            }
        }

        public ICommand NewTournamentCommand
        {
            get
            {
                if (_newTournamentCommand == null)
                {
                    _newTournamentCommand = new RelayCommand(
                        param => OpenNewTournamentPage(),
                        param => true
                    );
                }
                return _newTournamentCommand;
            }
        }

        public ICommand OpenTournamentCommand
        {
            get
            {
                if (_openTournamentCommand == null)
                {
                    _openTournamentCommand = new RelayCommand(
                        param => OpenTournament(),
                        param => true
                    );
                }
                return _openTournamentCommand;
            }
        }

        #endregion

        #region Private Methods

        private void OpenTournament()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = "Открыть турнир",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "Wrestling Tournament (*.wrt)|*.wrt|All Files (*.*)|*.*"
            };

            bool? success = Dialog.ShowOpenFileDialog(this, settings);
            if (success == true)
            {
                var tournament = _tournManager.LoadFromFile(settings.FileName);
                if (tournament != null)
                {
                    VerifyTeamEmblems(tournament);

                    VerifySettings(tournament);

                    DataContext.Tournament = tournament;

                    NavigateToView<DashboardViewModel>();
                }
            }
        }

        private void VerifyTeamEmblems(Entities.Tournament entity)
        {
            foreach(var app in entity.TeamApplications)
            {
                if (string.IsNullOrEmpty(app.EmblemPath)) continue;                
                
                if (!File.Exists(app.EmblemPath))
                {
                    var imgPath = $"{AppDomain.CurrentDomain.BaseDirectory}Images\\";
                    
                    var fileNameItems = app.EmblemPath.ToString().Split('\\');

                    if (fileNameItems.Length == 0) continue;

                    string fileName = fileNameItems[fileNameItems.Length - 1];

                    var fullFilePath = $"{imgPath}{fileName}";

                    if (!File.Exists(fullFilePath))
                    {
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

                        var teamNameItems = fileNameWithoutExt.Split('_');

                        if (teamNameItems.Length == 0) continue;

                        var teamName = teamNameItems[teamNameItems.Length - 1];

                        var existingTeamEmblems = Directory.EnumerateFiles(imgPath, "*.*");

                        foreach (var img in existingTeamEmblems)
                        {
                            var loadedLogo = Path.GetFileName(img);

                            if (loadedLogo.Contains(teamName))
                            {
                                app.EmblemPath = loadedLogo;
                                break;
                            }
                        }
                    }
                }

            }
        }

        private void VerifySettings(Entities.Tournament entity)
        {
            if (entity.Settings.MaxRoundSecond == 0) entity.Settings.MaxRoundSecond = GlobalSettings.MaxRoundSecond;
            if (entity.Settings.MaxTimeoutSecond == 0) entity.Settings.MaxTimeoutSecond = GlobalSettings.MaxTimeoutSecond;
            if (entity.Settings.MaxActionSecond == 0) entity.Settings.MaxActionSecond = GlobalSettings.MaxActionSecond;

            if (string.IsNullOrEmpty(entity.Settings.SliderBackgroundImagePath) || !File.Exists(entity.Settings.SliderBackgroundImagePath))
            {
                entity.Settings.SliderBackgroundImagePath = GlobalSettings.DefaultSliderImage;
            }

            if (string.IsNullOrEmpty(entity.Settings.StartGongSoundPath) || !File.Exists(entity.Settings.StartGongSoundPath))
            {
                entity.Settings.StartGongSoundPath = GlobalSettings.DefaultStartGongSound;
            }

            if (string.IsNullOrEmpty(entity.Settings.EndGongSoundPath) || !File.Exists(entity.Settings.EndGongSoundPath))
            {
                entity.Settings.EndGongSoundPath = GlobalSettings.DefaultEndGongSound;
            }
        }

        private void OpenNewTournamentPage()
        {
            DataContext.Tournament = new Entities.Tournament(GetSettingsObject())
            {
                ID = Guid.NewGuid(),
                Name = "Новый турнир",
                Status = TournamentStatus.Pending,
                StartDate = DateTime.Now.AddDays(1)
            };

            if (DataContext.Tournament.Settings.IsAutosaveEnabled)
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
                    var tournService = Resolve<ITournamentsManager>();

                    var result = tournService.SaveToFile(DataContext.Tournament, settings.FileName);
                    ShowSnackMessage(result ? "Турнир сохранен! Автосохранение включено." : "При сохранении произошла ошибка!");

                    if (!result)
                    {
                        return;
                    }
                }
            }

            NavigateToView<DashboardViewModel>();
        }

        private GlobalSettings GetSettingsObject()
        {
            var settings = new GlobalSettings
            {
                MaxRoundSecond = GlobalSettings.MaxRoundSecond,
                MaxTimeoutSecond = GlobalSettings.MaxTimeoutSecond,
                MaxActionSecond = GlobalSettings.MaxActionSecond,
                IsTimerBackward = GlobalSettings.IsTimerBackward,
                IsSoundEnabled = GlobalSettings.IsSoundEnabled,
                EndGongSoundPath = GlobalSettings.EndGongSoundPath,
                SliderBackgroundImagePath = GlobalSettings.SliderBackgroundImagePath,
                StartGongSoundPath = GlobalSettings.StartGongSoundPath,
                SliderMaxSecond = GlobalSettings.SliderMaxSecond,
                SliderOpacityValue = GlobalSettings.SliderOpacityValue,
                IsAutosaveEnabled = GlobalSettings.IsAutosaveEnabled,
                AutosaveMaxSecond = GlobalSettings.AutosaveMaxSecond,
                IsTournamentScoreInternational = GlobalSettings.IsTournamentScoreInternational,
                IsOverlayOlympic = GlobalSettings.IsOverlayOlympic,
                IsVideoRecordingEnabled = GlobalSettings.IsVideoRecordingEnabled,
                VideoStoragePath = GlobalSettings.VideoStoragePath
            };

            return settings;
        }

        private void OpenSettings()
        {
            NavigateToView<SettingsViewModel>();
        }
        
        private void NewQuickMatch()
        {
            DataContext.WrestlingMatch = new WrestlingMatch
            {
                MaxTimeoutSecond = 30,
                MaxRoundSecond = 180,
                MaxActionSecond = 30,
                MatchNumber = 1,
                WrestlerInRed = new Wrestler { ID = Guid.NewGuid()},
                WrestlerInBlue = new Wrestler { ID = Guid.NewGuid()}
            };

            NavigateToView<MatchControlViewModel>();
        }

        #endregion
    }
}