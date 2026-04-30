using MaterialDesignThemes.Wpf;
using MvvmDialogs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using Wrestling.DataAccess;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Entities.Bracket.Seeding;
using Wrestling.Entities.Results;
using Wrestling.Entities.Results.Achievements;
using Wrestling.Providers;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Slider;
using Wrestling.UI.Material.Slider.Slides;
using Wrestling.UI.Material.Slider.Slides.CarpetBracketsSlide;
using Wrestling.UI.Material.Slider.Slides.GroupBracketSlide;
using Wrestling.UI.Material.Slider.Slides.ImageSlide;
using Wrestling.UI.Material.Slider.Slides.UpcomingMatchesSlide;
using Wrestling.UI.Material.Slider.Slides.VideoSlide;
using Wrestling.UI.Material.Tournament.Print;
using Wrestling.UI.Material.Tournament.Standing.Details;
using Wrestling.UI.Material.Utils;
using Wrestling.UI.Utils;

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
            try { di.Resolve<NetworkServicesLifecycle>()?.Dispose(); } catch { }
            try { (di.Resolve<IPeerDiscoveryService>() as IDisposable)?.Dispose(); } catch { }
            try { (di.Resolve<ITournamentHttpServer>() as IDisposable)?.Dispose(); } catch { }
            base.OnExit(e);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("ru-RU");
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(
                XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

            var di = GetContainer();

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

            app.Show();

            navService.NavigateToView<HomeViewModel>();
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

                    MessageBox.Show($"Ошибка: {exception.Message}", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

                MessageBox.Show($"Ошибка: {args.Exception.Message}", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                LogException("TaskScheduler.UnobservedTaskException", args.Exception);
                args.SetObserved();
                CreateBackup(di);
            };
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

            di.Add<ITournamentImporter>(new TournamentImporter(di.Resolve<ITournamentsManager>(), di.Resolve<List<IGroupBracketProcessor>>()));

            // Network services: peer discovery via UDP broadcast + embedded
            // HTTP server that serves this node's .wrt. Both are singletons and
            // are driven by NetworkServicesLifecycle (below) which watches the
            // data context.
            var discovery = new PeerDiscoveryService();
            var httpServer = new TournamentHttpServer();
            di.Add<IPeerDiscoveryService>(discovery);
            di.Add<ITournamentHttpServer>(httpServer);
            di.Add<NetworkServicesLifecycle>(new NetworkServicesLifecycle(dc, discovery, httpServer));

            di.Add(new WwfScoreScreenView(), "ScoreScreen");

            di.Add<ISliderWindowManager>(new SliderWindowManager(di));

            di.Add(new PrintView(), "PrintHost");

            di.Add<IMatchNumbersGenerator>(new CarpetMatchNumbersGenerator());

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

            di.Add<IKeyHandler>(new KeyHandler());

            return di;
        }
    }
}
