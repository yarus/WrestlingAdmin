using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using MvvmDialogs;
using Wrestling.DataAccess;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Entities.Results;
using Wrestling.Integration;
using Wrestling.Providers;
using Wrestling.Recorder;
using Wrestling.Recorder.DataAccess;
using Wrestling.UI.Material.Home;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.ScoreScreen;
using Wrestling.UI.Material.Slider;
using Wrestling.UI.Material.Slider.Slides;
using Wrestling.UI.Material.Slider.Slides.GroupBracketSlide;
using Wrestling.UI.Material.Slider.Slides.ImageSlide;
using Wrestling.UI.Material.Slider.Slides.UpcomingMatchesSlide;
using Wrestling.UI.Material.Slider.Slides.VideoSlide;
using Wrestling.UI.Material.Tournament.Print;
using Wrestling.UI.Material.Utils;
using Wrestling.UI.Material.Utils.Recording;
using Wrestling.UI.Material.Utils.Recording.OverlayDrawer;
using Wrestling.UI.Utils;
using InternationalScoreScreenView = Wrestling.UI.Material.ScoreScreen.InternationalScoreScreenView;
using SlideHostView = Wrestling.UI.Material.Slider.SlideHostView;

namespace Wrestling.UI.Material
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("ru-RU");
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(
                XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

            var di = GetContainer();
            
            var recConfigDataAccess = di.Resolve<IRecorderConfigurationDataAccess>();
            var recConfig = recConfigDataAccess?.LoadFromFile("CamConfig.json");

            if (recConfig != null)
            {
                di.Add<RecorderConfiguration>(recConfig);
            }

            var navService = LoadNavigation(di);

            LoadSpecialViewModels(di);

            var app = new MainWindow(di);

            navService.ShellVm = app.DataContext as IShellViewModel;

            MainWindow = app;

            app.Show();

            navService.NavigateToView<HomeViewModel>();
        }

        private void LoadSpecialViewModels(IDiContainer di)
        {
            di.Add<ScoreScreenViewModel>(new ScoreScreenViewModel(di));
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

            //di.Add<Utils.Recording.App.ICamRecorderGenerator>(new Utils.Recording.App.FfmpegCamRecorderGenerator());
            //di.Add<IRecorder>(new FfmpegCamRecorder());
            di.Add<IOverlayDrawer>(new OlympicOverlayDrawer());
            di.Add<IMatchRecorder>(new MatchRecorder(di.Resolve<IOverlayDrawer>()));
            //di.Add<IMatchRecorderGenerator>(new MatchRecorderGenerator(di.Resolve<Utils.Recording.App.ICamRecorderGenerator>(), di.Resolve<IOverlayDrawer>()));

            di.Add<IDialogService>(new DialogService());

            di.Add<IStorageDataAccess>(new JsonStorageDataAccess());

            di.Add<IRecorderConfigurationDataAccess>(new RecorderConfigurationDataAccess(new JsonStorageDataAccess()));

            di.Add<IWrestlersDataAccess>(new WrestlersDataAccess(di.Resolve<IStorageDataAccess>()));
            di.Add<ITeamsDataAccess>(new TeamsDataAccess(di.Resolve<IStorageDataAccess>()));

            di.Add<IMatchDataAccess>(new MatchDataAccess(di.Resolve<IStorageDataAccess>()));

            di.Add<ITournamentDataAccess>(new TournamentDataAccess(di.Resolve<IStorageDataAccess>()));

            di.Add<IEntityToInfoAdapter>(new EntityToInfoAdapter());

            di.Add<ITournamentsManager>(new TournamentsManager(di.Resolve<ITournamentDataAccess>(), di.Resolve<IEntityToInfoAdapter>()));

            di.Add<ICacheManager>(new CacheManager(di.Resolve<ITeamsDataAccess>(), di.Resolve<IWrestlersDataAccess>(), di.Resolve<IEntityToInfoAdapter>()));

            di.Add<GlobalSettings>(new GlobalSettings { IsSoundEnabled = true, IsTimerBackward = true });

            di.Add<IDataContext>(new DataContext());

            di.Add<List<IGroupBracketProcessor>>(new List<IGroupBracketProcessor>
            {
                new OlympicWithConsilationFromFinalistsGroupBracketProcessor(),
                new OlympicGroupBracketProcessor(),
                new RoundRobinGroupBracketProcessor(),
                new SubGroupsToOlympicBracketPorcessor()
            });

            di.Add<GroupBracketViewModel>(new GroupBracketViewModel(di));
            di.Add<GroupBracketSlideSettingsViewModel>(new GroupBracketSlideSettingsViewModel(di));
            di.Add<ImageSlideViewModel>(new ImageSlideViewModel(di));
            di.Add<ImageSlideSettingsViewModel>(new ImageSlideSettingsViewModel(di));
            di.Add<VideoSlideViewModel>(new VideoSlideViewModel(di));
            di.Add<VideoSlideSettingsViewModel>(new VideoSlideSettingsViewModel(di));
            di.Add<UpcomingMatchesViewModel>(new UpcomingMatchesViewModel(di));
            di.Add<UpcomingMatchesSlideSettingsViewModel>(new UpcomingMatchesSlideSettingsViewModel(di));

            di.Add<List<ISlideType>>(new List<ISlideType>
            {
                new GroupBracketSlide(di),
                new UpcomingMatchesSlide(di),
                new ImageSlide(di),
                new VideoSlide(di)
            });

            di.Add<ITournamentImporter>(new TournamentImporter(di.Resolve<ITournamentsManager>(), di.Resolve<List<IGroupBracketProcessor>>()));

            //di.Add(new InternationalScoreScreenView(), "ScoreScreen");
            di.Add(new WwfScoreScreenView(), "ScoreScreen");

            di.Add(new SlideHostView(), "SlideHost");
            di.Add<SlideHostViewModel>(new SlideHostViewModel(di));

            di.Add(new PrintView(), "PrintHost");

            di.Add<IMatchNumbersGenerator>(new CarpetMatchNumbersGenerator());

            di.Add<ITeamResultsCalculator>(new TeamResultsCalculator());

            di.Add(new OlympicTeamResultsOrderer(), "OlympicOrderer");
            di.Add(new MedalsTeamResultsOrderer(), "MedalsOrderer");
            di.Add(new PointsTeamResultsOrderer(), "PointsOrderer");

            di.Add<IRosbosApi>(new RosbosApi());

            di.Add<IKeyHandler>(new KeyHandler());

            return di;
        }
    }
}
