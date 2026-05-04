using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Entities;
using Wrestling.Providers;
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
        private ICacheManager _cacheManager;
        private IList<CommandButtonItem> _drawerItems;

        private ICommand _newTournamentCommand;
        private ICommand _openTournamentCommand;
        private ICommand _openSettingsCommand;

        #endregion

        public HomeViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            _tournManager = Resolve<ITournamentsManager>();
            _cacheManager = Resolve<ICacheManager>();
        }

        public override string PageTitle => "Вольная борьба - Администратор турниров версия 20260421";

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

                    UpdateCache();

                    Resolve<IResultsService>().Recalculate(tournament);

                    NavigateToView<DashboardViewModel>();
                }
            }
        }

        private void UpdateCache()
        {
            foreach (var teamApp in DataContext.Tournament.TeamApplications)
            {
                if (DataContext.TeamsCache.FirstOrDefault(x => x.ID == teamApp.ID || x.HashTag == teamApp.HashTag || x.FullName == teamApp.FullName) == null)
                {
                    DataContext.TeamsCache.Add(teamApp);
                }
            }
            _cacheManager.SaveTeams(DataContext.TeamsCache);

            foreach (var wrestler in DataContext.Tournament.Wrestlers)
            {
                if (DataContext.WrestlersCache.FirstOrDefault(x => x.ID == wrestler.ID || x.HashTag == wrestler.HashTag) == null)
                {
                    DataContext.WrestlersCache.Add(wrestler);
                }
            }
            _cacheManager.SaveWrestlers(DataContext.WrestlersCache);
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

            Resolve<IResultsService>().Recalculate(DataContext.Tournament);

            // Prompt for save location up front so event-driven autosaves
            // (after each match approval / peer-sync merge) have a target.
            // The dashboard re-prompts if the operator dismissed this dialog.
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
                ShowSnackMessage(result ? "Турнир сохранен!" : "При сохранении произошла ошибка!");

                if (!result)
                {
                    return;
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
                IsOverlayOlympic = GlobalSettings.IsOverlayOlympic
            };

            return settings;
        }

        private void OpenSettings()
        {
            NavigateToView<SettingsViewModel>();
        }

        #endregion
    }
}