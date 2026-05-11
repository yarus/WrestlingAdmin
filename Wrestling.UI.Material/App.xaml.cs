using MvvmDialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Wrestling.DataAccess;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Entities.Bracket.Seeding;
using Wrestling.Entities.Results;
using Wrestling.Entities.Results.Achievements;
using Wrestling.Providers;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Match;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Settings;
using Wrestling.UI.Material.Slider;
using Wrestling.UI.Material.Slider.Slides;
using Wrestling.UI.Material.Slider.Slides.CarpetBracketsSlide;
using Wrestling.UI.Material.Slider.Slides.GroupBracketSlide;
using Wrestling.UI.Material.Slider.Slides.ImageSlide;
using Wrestling.UI.Material.Slider.Slides.UpcomingMatchesSlide;
using Wrestling.UI.Material.Slider.Slides.VideoSlide;
using Wrestling.UI.Material.Tournament.Conducting;
using Wrestling.UI.Material.Tournament.Results;
using Wrestling.UI.Material.Tournament.Print;
using Wrestling.UI.Material.Tournament.Print.PrintBracket;
using Wrestling.UI.Material.Tournament.Standing.Applications;
using Wrestling.UI.Material.Tournament.Standing.Carpets;
using Wrestling.UI.Material.Tournament.Standing.Details;
using Wrestling.UI.Material.Tournament.Standing.Draw;
using Wrestling.UI.Material.Utils;
using MaterialDesignThemes.Wpf;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;
using Wrestling.UI.Material.Localization;

namespace Wrestling.UI.Material
{
    public partial class App : Application
    {
        private int _shuttingDown; // 0 = normal, 1 = currently handling a crash-path save
        private readonly object _persistenceLock = new object();
        private readonly object _logLock = new object();

        protected override void OnExit(ExitEventArgs e)
        {
            // Tear down network services so their sockets release cleanly on
            // normal shutdown. Crash paths go through the exception handlers
            // above; the OS reclaims sockets either way, but an orderly stop
            // flushes final announce/response cycles and frees the UDP/TCP
            // ports faster for restarts.
            var di = DiContainer.Instance;
            SafeDispose(() => di.Resolve<PeerSyncStatusTracker>()?.Dispose(), nameof(PeerSyncStatusTracker));
            SafeDispose(() => di.Resolve<PeerSyncService>()?.Dispose(), nameof(PeerSyncService));
            SafeDispose(() => di.Resolve<NetworkServicesLifecycle>()?.Dispose(), nameof(NetworkServicesLifecycle));
            SafeDispose(() => (di.Resolve<IPeerDiscoveryService>() as IDisposable)?.Dispose(), nameof(IPeerDiscoveryService));
            SafeDispose(() => (di.Resolve<ITournamentHttpServer>() as IDisposable)?.Dispose(), nameof(ITournamentHttpServer));
            base.OnExit(e);
        }

        // Shutdown disposes are best-effort: the OS reclaims sockets anyway,
        // so a missing service or a disposal failure must never block exit.
        // Logging keeps DI-wiring bugs visible during development.
        private static void SafeDispose(Action dispose, string label)
        {
            try { dispose(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"OnExit dispose of {label} failed: {ex}"); }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var di = GetContainer();

            // Apply the operator-chosen theme + language before any window is
            // shown so the first rendered frame already matches the saved
            // preference. Defaults (Light / DeepPurple / Lime / "ru") kick in
            // for first launch or a missing prefs file — visually identical
            // to the historical hardcoded BundledTheme + ru-RU.
            var themeManager = di.Resolve<Wrestling.UI.Material.Theme.IThemeManager>();
            var uiStorage = di.Resolve<Wrestling.UI.Material.Theme.ILocalUiSettingsStorage>();
            var savedUi = uiStorage?.Load() ?? new Wrestling.UI.Material.Theme.LocalUiSettings();
            themeManager?.Apply(savedUi);

            var localization = di.Resolve<ILocalizationService>();
            if (localization != null)
            {
                if (!localization.SetLanguage(savedUi.LanguageCode) && localization.AvailableLanguages.Count > 0)
                {
                    localization.SetLanguage(localization.AvailableLanguages[0].Code);
                }
            }

            SetupExceptionHandling(di);

            var navService = LoadNavigation(di);

            LoadSpecialViewModels(di);

            var app = new MainWindow(di);

            navService.ShellVm = app.DataContext as IShellViewModel;

            MainWindow = app;

            // Network services bubble port conflicts, firewall hints, etc. as
            // DiagnosticMessage events. Route to the snackbar once the shell
            // VM is available — must marshal to the UI thread because the
            // firewall watchdog fires from a thread-pool Timer callback.
            var lifecycle = di.Resolve<NetworkServicesLifecycle>();
            var shell = navService.ShellVm;
            if (lifecycle != null && shell != null)
            {
                lifecycle.DiagnosticMessage += (s, msg) =>
                {
                    var dispatcher = Current?.Dispatcher;
                    if (dispatcher == null || dispatcher.CheckAccess()) shell.ShowSnackbarMessage(msg);
                    else dispatcher.BeginInvoke(new Action(() => shell.ShowSnackbarMessage(msg)));
                };
            }

            // Build the persistent left-rail items + overlay-parent mapping
            // now that all phase VMs are registered. Done before Show() so the
            // first frame already has the rail wired up (rail itself stays
            // hidden on Home — IsRailVisible reacts to TournamentChanged).
            if (shell != null)
            {
                shell.SetNavigationItems(BuildNavigationItems(navService), BuildFooterNavigationItems(navService, shell));

                // Match overlays — full-screen + dynamic back to launching screen.
                shell.RegisterOverlayParent(typeof(MatchControlViewModel), typeof(ConductingViewModel));
                shell.RegisterOverlayParent(typeof(MatchResultsViewModel), typeof(ConductingViewModel));
                shell.RegisterOverlayParent(typeof(PrintBracketViewModel), typeof(ConductingViewModel));
                shell.RegisterMatchOverlay(typeof(MatchControlViewModel));
                shell.RegisterMatchOverlay(typeof(MatchResultsViewModel));
                shell.RegisterMatchOverlay(typeof(PrintBracketViewModel));

                // Conducting fullscreen views — full-screen + static back to Conducting.
                shell.RegisterOverlayParent(typeof(Wrestling.UI.Material.Tournament.Progress.Schedule.ScheduleViewModel), typeof(ConductingViewModel));
                shell.RegisterOverlayParent(typeof(Wrestling.UI.Material.Tournament.Progress.Brackets.BracketsViewModel), typeof(ConductingViewModel));
                shell.RegisterOverlayParent(typeof(Wrestling.UI.Material.Slider.SliderControlViewModel), typeof(ConductingViewModel));
            }

            app.Show();

            navService.NavigateToView<HomeViewModel>();
        }

        private static IList<INavigationItem> BuildNavigationItems(INavigationService navService)
        {
            var items = new List<INavigationItem>
            {
                new NavigationItem("Nav_Standing", PackIconKind.FileDocumentOutline,
                    typeof(DetailsViewModel),
                    new RelayCommand(_ => navService.NavigateToView<DetailsViewModel>())),
                new NavigationItem("Nav_Registration", PackIconKind.ClipboardList,
                    typeof(ApplicationsViewModel),
                    new RelayCommand(_ => navService.NavigateToView<ApplicationsViewModel>())),
                new NavigationItem("Nav_Draw", PackIconKind.Shuffle,
                    typeof(DrawViewModel),
                    new RelayCommand(_ => navService.NavigateToView<DrawViewModel>())),
                new NavigationItem("Nav_Schedule", PackIconKind.Calendar,
                    typeof(CarpetsViewModel),
                    new RelayCommand(_ => navService.NavigateToView<CarpetsViewModel>())),
                new NavigationItem("Nav_Conducting", PackIconKind.Scoreboard,
                    typeof(ConductingViewModel),
                    new RelayCommand(_ => navService.NavigateToView<ConductingViewModel>())),
                new NavigationItem("Nav_Results", PackIconKind.Trophy,
                    typeof(ResultsViewModel),
                    new RelayCommand(_ => navService.NavigateToView<ResultsViewModel>()))
            };
            return items;
        }

        private static IList<INavigationItem> BuildFooterNavigationItems(INavigationService navService, IShellViewModel shell)
        {
            // "Закрыть" sits last so it ends up at the very bottom of the rail's
            // footer group. TargetViewModel=null keeps it out of the active-item
            // highlight (it's an action, not a destination); ActivateCommand
            // delegates to the shell so the close flow shares the same dialog
            // owner (MainWindow) as the app-exit confirmation.
            var shellVm = (MainWindowViewModel)shell;
            return new List<INavigationItem>
            {
                new NavigationItem("Nav_Settings", PackIconKind.Cog,
                    typeof(SettingsViewModel),
                    new RelayCommand(_ => navService.NavigateToView<SettingsViewModel>())),
                new NavigationItem("Nav_Close", PackIconKind.LogoutVariant,
                    null,
                    shellVm.CloseTournamentCommand)
            };
        }

        private void LoadSpecialViewModels(IDiContainer di)
        {
            di.Add<ScoreScreenViewModel>(new ScoreScreenViewModel(di));
        }

        private void SetupExceptionHandling(IDiContainer di)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exception = (Exception)args.ExceptionObject;

                LogException("AppDomain.UnhandledException", exception);

                if (args.IsTerminating)
                {
                    CreateBackup(di);                    

                    ShowCriticalError(exception);
                }
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                // Benign WPF resource-lookup miss — typically raised by injected
                // overlays (accessibility tools, IME candidate windows, touch
                // keyboard) doing DynamicResource lookups against keys we never
                // defined. It cannot leave the tournament in a half-mutated
                // state, so skip the backup + MessageBox ceremony and log quietly.
                if (args.Exception is ResourceReferenceKeyNotFoundException)
                {
                    LogException("Application.DispatcherUnhandledException (ignored: ResourceReferenceKeyNotFoundException)", args.Exception);
                    args.Handled = true;
                    return;
                }

                LogException("Application.DispatcherUnhandledException", args.Exception);

                // Always write a dated backup instead of overwriting the active
                // save — a UI-thread exception can leave the in-memory tournament
                // in a half-mutated state, and persisting it onto FileName would
                // destroy a previously-good save.
                CreateBackup(di);

                args.Handled = true;

                ShowCriticalError(args.Exception);
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                LogException("TaskScheduler.UnobservedTaskException", args.Exception);
                args.SetObserved();
                CreateBackup(di);
            };
        }

        // Crash-time MessageBox helper. LocalizationService falls back to the
        // raw key when no language has been set yet, so a startup-time crash
        // before LoadAll completes still produces a readable (if untranslated)
        // dialog. Format string is fetched the same way.
        private static void ShowCriticalError(Exception exception)
        {
            var loc = LocalizationService.Instance;
            var title = loc.T("App_CriticalError_Title");
            var format = loc.T("App_Error_Format");
            string body;
            try { body = string.Format(format, exception?.Message ?? string.Empty); }
            catch (FormatException) { body = exception?.Message ?? string.Empty; }
            MessageBox.Show(body, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void CreateBackup(IDiContainer di)
        {
            // Re-entrancy guard so two concurrent crash handlers don't both spawn
            // a backup write. Reset on exit so a later unobserved exception can
            // still trigger its own backup.
            if (Interlocked.Exchange(ref _shuttingDown, 1) == 1) return;

            lock (_persistenceLock)
            {
                try
                {
                    var ctx = di.Resolve<IDataContext>();
                    var mgr = di.Resolve<ITournamentsManager>();

                    if (ctx != null && mgr != null && ctx.Tournament != null)
                    {
                        string backupFilePath = GetBackupFilePath();

                        mgr.SaveToFile(ctx.Tournament, backupFilePath);
                    }
                }
                catch (Exception ex)
                {
                    LogException("CreateBackup", ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _shuttingDown, 0);
                }
            }
        }

        private void LogException(string source, Exception ex)
        {
            try
            {
                string logEntry = $@"
[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] - EXCEPTION SOURCE: {source}
EXCEPTION TYPE: {ex.GetType().FullName}
MESSAGE: {ex.Message}
STACK TRACE:
{ex.StackTrace}
INNER EXCEPTION: {ex.InnerException?.ToString() ?? "None"}

{new string('=', 80)}
";

                string logPath = GetLogFilePath();

                // Concurrent crash handlers (AppDomain, Dispatcher, TaskScheduler) can
                // fire simultaneously; AppendAllText is not thread safe.
                lock (_logLock)
                {
                    File.AppendAllText(logPath, logEntry);
                }
            }
            catch(Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine(logEx);
            }
        }

        private string GetLogFilePath()
        {
            var logDirectory = GetAppDirectory("Logs");

            return Path.Combine(logDirectory, $"error_log_{DateTime.Now:yyyyMMdd}.txt");
        }

        private string GetBackupFilePath()
        {
            var logDirectory = GetAppDirectory("Backups");

            return Path.Combine(logDirectory, $"backup_{DateTime.Now:yyyyMMdd_HHmmss.fff}.wrt");
        }

        private string GetAppDirectory(string folder)
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            string logDirectory = Path.Combine(appDataPath, appName, folder);

            Directory.CreateDirectory(logDirectory);

            return logDirectory;
        }

        private INavigationService LoadNavigation(IDiContainer di)
        {
            var navService = new NavigationService(di);

            di.Add<INavigationService>(navService);

            navService.LoadNavigation();

            return navService;
        }
        
        private IDiContainer GetContainer()
        {
            var di = DiContainer.Instance;

            di.Add<IDialogService>(new DialogService());

            di.Add<IStorageDataAccess>(new JsonStorageDataAccess());

            di.Add<IWrestlersDataAccess>(new WrestlersDataAccess(di.Resolve<IStorageDataAccess>()));
            di.Add<ITeamsDataAccess>(new TeamsDataAccess(di.Resolve<IStorageDataAccess>()));

            di.Add<IMatchDataAccess>(new MatchDataAccess(di.Resolve<IStorageDataAccess>()));

            di.Add<ITournamentDataAccess>(new TournamentDataAccess(di.Resolve<IStorageDataAccess>()));

            di.Add<IEntityToInfoAdapter>(new EntityToInfoAdapter());

            di.Add<ITournamentsManager>(new TournamentsManager(di.Resolve<ITournamentDataAccess>(), di.Resolve<IEntityToInfoAdapter>()));


            di.Add<GlobalSettings>(new GlobalSettings { IsSoundEnabled = true, IsTimerBackward = true });

            var dc = new DataContext();
            di.Add<IDataContext>(dc);

            var cacheMgr = new CacheManager(di.Resolve<ITeamsDataAccess>(), di.Resolve<IWrestlersDataAccess>(), di.Resolve<IEntityToInfoAdapter>());
            dc.TeamsCache = cacheMgr.LoadTeams();
            dc.WrestlersCache = cacheMgr.LoadWrestlers();
            di.Add<ICacheManager>(cacheMgr);

            di.Add<IGroupGenerator>(new GroupGenerator());

            di.Add<List<IGroupBracketProcessor>>(new List<IGroupBracketProcessor>
            {
                new OlympicWithConsolationFromFinalistsGroupBracketProcessor(),
                new OlympicGroupBracketProcessor(),
                new RoundRobinGroupBracketProcessor(),
                new SubGroupsToOlympicBracketProcessor()
            });

            // Seeding strategy drives DrawViewModel.SeedWrestlers. Default is
            // ClubCityLevelSeedingStrategy which keeps same-club / same-city /
            // high-Level wrestlers on opposite sides of the bracket. Swap in
            // ShuffleSeedingStrategy here if a pure random draw is ever needed.
            di.Add<ISeedingStrategy>(new ClubCityLevelSeedingStrategy());

            // Per-host ISliderViewControl instances are now constructed via
            // ISlideType.CreateViewControl(), so these view-model types are no
            // longer DI singletons. Only the settings VMs (which back the one
            // AddSlide dialog) stay singletons.
            di.Add<GroupBracketSlideSettingsViewModel>(new GroupBracketSlideSettingsViewModel(di));
            di.Add<CarpetBracketsSlideSettingsViewModel>(new CarpetBracketsSlideSettingsViewModel(di));
            di.Add<ImageSlideSettingsViewModel>(new ImageSlideSettingsViewModel(di));
            di.Add<VideoSlideSettingsViewModel>(new VideoSlideSettingsViewModel(di));
            di.Add<UpcomingMatchesSlideSettingsViewModel>(new UpcomingMatchesSlideSettingsViewModel(di));

            di.Add<List<ISlideType>>(new List<ISlideType>
            {
                new GroupBracketSlide(di),
                new CarpetBracketsSlide(di),
                new UpcomingMatchesSlide(di),
                new ImageSlide(di),
                new VideoSlide(di)
            });

            di.Add<IMatchNumbersGenerator>(new CarpetMatchNumbersGenerator());

            di.Add<ITournamentImporter>(new TournamentImporter(
                di.Resolve<ITournamentsManager>(),
                di.Resolve<List<IGroupBracketProcessor>>(),
                di.Resolve<IMatchNumbersGenerator>()));

            di.Add<ITeamResultsCalculator>(new TeamResultsCalculator());

            di.Add(new OlympicTeamResultsOrderer(), "OlympicOrderer");
            di.Add(new MedalsTeamResultsOrderer(), "MedalsOrderer");
            di.Add(new PointsTeamResultsOrderer(), "PointsOrderer");

            di.Add<List<IAchievementCalculator>>(new List<IAchievementCalculator>
            {
                new FastestWinAchievementCalculator(),
                new FastestActionAchievementCalculator(),
                new MostAmplitudeActionsAchievementCalculator(),
                new MostPointsCountAchievementCalculator(),
                new MostTusheWinsAchievementCalculator(),
                new MostDominationWinsAchievementCalculator(),
                new WinInLast10SecondsAchievementCalculator()
            });

            // Event-driven cache of computed tournament results. Recalculated
            // on tournament open/close, match approve/revert and peer-sync
            // merge. Consumer VMs subscribe to ResultsChanged.
            di.Add<IResultsService>(new ResultsService(
                di.Resolve<List<IGroupBracketProcessor>>(),
                di.Resolve<ITeamResultsCalculator>(),
                di.Resolve<List<IAchievementCalculator>>()));

            // Network services: peer discovery via UDP broadcast + embedded
            // HTTP server that serves this node's .wrt. Both are singletons and
            // are driven by NetworkServicesLifecycle (below) which watches the
            // data context.
            var discovery = new PeerDiscoveryService();
            var httpServer = new TournamentHttpServer();
            di.Add<IPeerDiscoveryService>(discovery);
            di.Add<ITournamentHttpServer>(httpServer);
            di.Add<NetworkServicesLifecycle>(new NetworkServicesLifecycle(dc, discovery, httpServer, Current.Dispatcher));

            // PeerSyncService listens for incoming peer advertisements with a
            // divergent stateHash and pulls+applies via the existing importer.
            // Replaces the old DispatcherTimer-based pull import. Constructed
            // here (after discovery/importer/manager are registered) on the UI
            // dispatcher so Apply marshals correctly.
            di.Add<PeerSyncService>(new PeerSyncService(
                discovery,
                dc,
                di.Resolve<ITournamentImporter>(),
                di.Resolve<ITournamentsManager>(),
                di.Resolve<IResultsService>(),
                Current.Dispatcher));

            // Read-model for the Dashboard "Синхронизация" Card. Holds an
            // ObservableCollection<PeerStatusViewModel> with live status and
            // a 5-minute session-cache for recently disconnected peers.
            di.Add<PeerSyncStatusTracker>(new PeerSyncStatusTracker(discovery, dc, Current.Dispatcher));

            di.Add(new WwfScoreScreenView(), "ScoreScreen");

            // Single entry point for the score-screen monitor window. Wraps
            // the IPanelView("ScoreScreen") + MonitorPicker.PickAsync flow so
            // MatchControlViewModel and the new Phase 5 → Ковер «Монитор»
            // quick-action share one implementation.
            di.Add<IMonitorWindowService>(new MonitorWindowService(di));

            di.Add<ISliderWindowManager>(new SliderWindowManager(di));

            di.Add(new PrintView(), "PrintHost");

            di.Add<IKeyHandler>(new KeyHandler());

            // Per-machine UI prefs (theme + language) — stored in
            // %LocalAppData%/WrestlingAdmin/local_ui_settings.json, separate
            // from .wrt so the operator's chosen theme does not change when
            // opening a tournament authored on another machine.
            var localUiStorage = new Wrestling.UI.Material.Theme.LocalUiSettingsStorage(di.Resolve<IStorageDataAccess>());
            di.Add<Wrestling.UI.Material.Theme.ILocalUiSettingsStorage>(localUiStorage);
            di.Add<Wrestling.UI.Material.Theme.IThemeManager>(new Wrestling.UI.Material.Theme.ThemeManager(localUiStorage));

            // Localization — singleton (LocalizationService.Instance) so the
            // {loc:Loc Key=...} markup extension can find it from XAML, also
            // registered into DI so view-model code can resolve it the usual
            // way. JSON files live next to the exe under i18n/.
            var i18nFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "i18n");
            JsonLocalizationLoader.LoadAll(LocalizationService.Instance, i18nFolder);
            di.Add<ILocalizationService>(LocalizationService.Instance);

            // Bridge for non-UI code (Wrestling.Providers can't take a WPF
            // dependency). GroupGenerator and friends call ProviderLocalization.T
            // which routes through this delegate to the real service.
            Wrestling.Providers.Localization.ProviderLocalization.Translate = (key, fallback) =>
            {
                var value = LocalizationService.Instance.T(key);
                return string.IsNullOrEmpty(value) || value == key ? fallback : value;
            };

            return di;
        }
    }
}
