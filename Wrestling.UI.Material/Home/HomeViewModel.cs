using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Tournament.Conducting;
using Wrestling.UI.Material.Tournament.Results;
using Wrestling.UI.Material.Tournament.Standing.Applications;
using Wrestling.UI.Material.Tournament.Standing.Mats;
using Wrestling.UI.Material.Tournament.Standing.Details;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Home
{
    public class HomeViewModel : ViewModelBase
    {
        #region Fields

        private ITournamentsManager _tournManager;
        private ICacheManager _cacheManager;
        private IResultsService _resultsService;
        private IRecentTournamentsService _recentService;

        private ICommand _newTournamentCommand;
        private ICommand _openTournamentCommand;
        private ICommand _openRecentCommand;

        #endregion

        public HomeViewModel(IDiContainer container) : base(container)
        {
            RecentTournaments = new ObservableCollection<string>();
        }

        public override void InitData()
        {
            base.InitData();

            _tournManager = Resolve<ITournamentsManager>();
            _cacheManager = Resolve<ICacheManager>();
            _resultsService = Resolve<IResultsService>();
            _recentService = Resolve<IRecentTournamentsService>();

            ReloadRecent();
        }

        // Most-recent-first list of .wrt paths the operator opened or created
        // on this machine. Bound to the Recent panel on the welcome card.
        public ObservableCollection<string> RecentTournaments { get; }

        public bool IsRecentEmpty => RecentTournaments.Count == 0;

        private void ReloadRecent()
        {
            RecentTournaments.Clear();
            if (_recentService == null) { OnPropertyChanged(nameof(IsRecentEmpty)); return; }

            foreach (var path in _recentService.LoadExisting())
            {
                RecentTournaments.Add(path);
            }
            OnPropertyChanged(nameof(IsRecentEmpty));
        }

        // App brand line — intentionally not localized (proper-noun "РОСБОС"
        // is a brand identifier). If marketing ever wants per-locale variants
        // promote this to T("Home_PageTitle", ...) with a fallback.
        public override string PageTitle => "РОСБОС © Сетка 2.0";

        private static string T(string key, string fallback)
        {
            var value = LocalizationService.Instance?.T(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }

        #region Commands

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

        public ICommand OpenRecentCommand
        {
            get
            {
                if (_openRecentCommand == null)
                {
                    _openRecentCommand = new RelayCommand(
                        param => OpenRecent(param as string),
                        param => param is string s && !string.IsNullOrWhiteSpace(s)
                    );
                }
                return _openRecentCommand;
            }
        }

        #endregion

        #region Private Methods

        private void OpenTournament()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = T("Home_OpenDialog_Title", "Открыть турнир"),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "Wrestling Tournament (*.wrt)|*.wrt|All Files (*.*)|*.*"
            };

            bool? success = Dialog.ShowOpenFileDialog(this, settings);
            if (success == true)
            {
                OpenFromFile(settings.FileName);
            }
        }

        private void OpenRecent(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            // Reuse the same load + route sequence as the file-dialog path.
            // If the file vanished between LoadExisting prune and the click,
            // OpenFromFile shows a snack and we refresh the list so the
            // dead entry disappears.
            if (!OpenFromFile(fileName))
            {
                ReloadRecent();
            }
        }

        // Returns true on a successful open. Handles the full pipeline used
        // by both OpenTournament (file dialog) and OpenRecent (tile click).
        private bool OpenFromFile(string fileName)
        {
            var tournament = _tournManager.LoadFromFile(fileName);
            if (tournament == null)
            {
                ShowSnackMessage(T("Snack_OpenError", "Не удалось открыть файл"));
                return false;
            }

            VerifyTeamEmblems(tournament);

            VerifySettings(tournament);

            DataContext.Tournament = tournament;

            UpdateCache();

            _resultsService.Recalculate(tournament);

            _recentService?.Add(fileName);

            // Land the operator on the phase that matches the
            // tournament's current state (e.g. mats configured →
            // straight to «Проведение») instead of always sending
            // them through «Положение».
            NavigateToOpenedTournamentPhase();
            return true;
        }

        // Picks the most relevant phase screen for a freshly-opened tournament.
        // Order matters — completion wins over mats, mats over brackets,
        // etc. — so we check the most-progressed conditions first.
        private void NavigateToOpenedTournamentPhase()
        {
            var t = DataContext.Tournament;
            if (t == null)
            {
                NavigateToView<DetailsViewModel>();
                return;
            }

            // Rule 5: every match in every group is completed → Результаты.
            if (t.MatchesCount > 0 && t.PendingMatchesCount == 0)
            {
                NavigateToView<ResultsViewModel>();
                return;
            }

            // Rule 4: at least one mat configured → Проведение.
            if (t.MatsCount > 0)
            {
                NavigateToView<ConductingViewModel>();
                return;
            }

            // Rule 3: brackets generated, no mats yet → Расписание (mats setup).
            if (t.MatchesCount > 0)
            {
                NavigateToView<MatsViewModel>();
                return;
            }

            // Rule 2: groups exist but no brackets → Регистрация.
            if (t.GroupsCount > 0)
            {
                NavigateToView<ApplicationsViewModel>();
                return;
            }

            // Rule 1: brand-new / empty tournament → Положение.
            NavigateToView<DetailsViewModel>();
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
                Name = T("Home_NewTournamentName", "Новый турнир"),
                Status = TournamentStatus.Pending,
                StartDate = DateTime.Now.AddDays(1)
            };

            _resultsService.Recalculate(DataContext.Tournament);

            // Prompt for save location up front so event-driven autosaves
            // (after each match approval / peer-sync merge) have a target.
            // The dashboard re-prompts if the operator dismissed this dialog.
            var settings = new SaveFileDialogSettings
            {
                Title = T("Home_SaveDialog_Title", "Сохранить турнир"),
                CheckFileExists = false,
                OverwritePrompt = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "Wrestling Tournament (*.wrt)|*.wrt|All Files (*.*)|*.*"
            };

            bool? success = Dialog.ShowSaveFileDialog(this, settings);
            if (success == true)
            {
                var result = _tournManager.SaveToFile(DataContext.Tournament, settings.FileName);
                ShowSnackMessage(result
                    ? T("Snack_TournamentSaved", "Турнир сохранен!")
                    : T("Snack_SaveError", "При сохранении произошла ошибка!"));

                if (!result)
                {
                    return;
                }

                _recentService?.Add(settings.FileName);
            }

            // Same routing as the «Open» path. For a brand-new empty
            // tournament this resolves to «Положение» (rule 1) just like
            // before; consolidating means future copy-from-template flows
            // automatically pick the right phase.
            NavigateToOpenedTournamentPhase();
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

        #endregion
    }
}