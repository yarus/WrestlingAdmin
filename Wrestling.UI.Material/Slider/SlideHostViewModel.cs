using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using Wrestling.Entities;
using Wrestling.UI.Material.Slider.Slides;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Slider
{
    public class SlideHostViewModel : TournamentViewModelBase
    {
        #region Fields

        private DispatcherTimer _timer;
        private bool _isTimerEnabled;
        private int _currentSecond;
        private int _slideSeconds;

        private ScreenSlide _currentSlide;
        private SlideChannel _channel;

        private ObservableCollection<ScreenSlide> _slides;
        private List<ISlideType> _slideTypes;

        // Each host owns one ISliderViewControl per slide-type string, created
        // via the slide type's factory. Caching here keeps the control's state
        // stable across repeated selections of the same slide type within this
        // host (e.g., an image doesn't reload on every rotation), while still
        // giving a different host its own independent set of controls.
        private readonly Dictionary<string, ISliderViewControl> _viewControlsByType = new Dictionary<string, ISliderViewControl>();

        private ICommand _changePageCommand;
        private ICommand _prevPageCommand;
        private ICommand _nextPageCommand;

        #endregion

        public SlideHostViewModel(IDiContainer container) : base(container)
        {
        }

        // Bind the host to a specific SlideChannel before calling InitData().
        // Slide list, rotation timer default, and preview state all come from
        // this channel; InitData pulls them in.
        public SlideChannel Channel
        {
            get { return _channel; }
            set
            {
                _channel = value;
                OnPropertyChanged("Channel");
            }
        }

        public override void InitData()
        {
            base.InitData();

            _slideTypes = Resolve<List<ISlideType>>();

            if (_channel != null)
            {
                Slides = _channel.Slides;
                SlideSeconds = _channel.SliderMaxSecond > 0
                    ? _channel.SliderMaxSecond
                    : GlobalSettings.SliderMaxSecond;
            }
            else
            {
                Slides = new ObservableCollection<ScreenSlide>();
                SlideSeconds = GlobalSettings.SliderMaxSecond;
            }

            if (_slides.Count > 0)
            {
                ChangeSlide(_slides[0]);
            }

            //IsTimerEnabled = true;
        }

        public int SlideSeconds
        {
            get { return _slideSeconds; }
            set
            {
                _slideSeconds = value;

                OnPropertyChanged("SlideSeconds");
            }
        }

        public bool IsTimerEnabled
        {
            get { return _isTimerEnabled; }
            set
            {
                _isTimerEnabled = value;

                if (_isTimerEnabled)
                {
                    SetupTimer();
                }
                else
                {
                    _timer?.Stop();
                }

                OnPropertyChanged("IsTimerEnabled");
            }
        }

        public override string PageTitle
        {
            get
            {
                if (CurrentSlide != null)
                {
                    return CurrentSlide.Title;
                }

                var v = LocalizationService.Instance?.T("Slide_DefaultTitle");
                return string.IsNullOrEmpty(v) || v == "Slide_DefaultTitle" ? "Турнир" : v;
            }
        }

        public bool IsNotLastPage => Slides.IndexOf(CurrentSlide) < Slides.Count - 1;
        public bool IsNotFirstPage => Slides.IndexOf(CurrentSlide) > 0;

        #region Properties / Commands

        public ScreenSlide CurrentSlide
        {
            get { return _currentSlide; }
            set
            {
                _currentSlide = value;

                if (_currentSlide != null && _slideTypes != null)
                {
                    var slideType = _slideTypes.FirstOrDefault(s => s.SlideType == _currentSlide.SlideType);
                    if (slideType != null)
                    {
                        CurrentView = GetOrCreateViewControl(slideType);
                        CurrentView.InitContext(CurrentSlide);
                        CurrentSecond = 0;
                        SlideSeconds = _currentSlide.Duration;
                    }
                }

                OnPropertyChanged("CurrentSlide");
                OnPropertyChanged("PageTitle");
                OnPropertyChanged("IsNotFirstPage");
                OnPropertyChanged("IsNotLastPage");
            }
        }

        private ISliderViewControl _currentView;
        public ISliderViewControl CurrentView
        {
            get { return _currentView; }
            set
            {
                _currentView = value;
                OnPropertyChanged("CurrentView");
            }
        }

        public int CurrentSecond
        {
            get { return CurrentSlide?.Duration - _currentSecond ?? 0; }
            set
            {
                _currentSecond = value;

                OnPropertyChanged("CurrentSecond");
            }
        }

        public ObservableCollection<ScreenSlide> Slides
        {
            get { return _slides; }
            set
            {
                _slides = value;

                OnPropertyChanged("Slides");
            }
        }
        
        public ICommand ChangePageCommand
        {
            get
            {
                if (_changePageCommand == null)
                {
                    _changePageCommand = new RelayCommand(p => ChangeSlide((ScreenSlide)p), p => p != null);
                }

                return _changePageCommand;
            }
        }

        public ICommand NextSlideCommand
        {
            get
            {
                if (_nextPageCommand == null)
                {
                    _nextPageCommand = new RelayCommand(
                        p => NextSlide(),
                        p => true);
                }

                return _nextPageCommand;
            }
        }

        public ICommand PrevPageCommand
        {
            get
            {
                if (_prevPageCommand == null)
                {
                    _prevPageCommand = new RelayCommand(
                        p => PreviousViewModel(),
                        p => true);
                }

                return _prevPageCommand;
            }
        }
        
        private void ChangeSlide(ScreenSlide slide)
        {
            if (!Slides.Contains(slide))
                Slides.Add(slide);

            CurrentSlide = Slides.FirstOrDefault(vm => vm == slide);
        }

        private void NextSlide()
        {
            var index = Slides.IndexOf(CurrentSlide);
            if (index < Slides.Count - 1)
            {
                var nextVm = Slides[index + 1];
                ChangeSlide(nextVm);
            }
        }
        
        private void PreviousViewModel()
        {
            var index = Slides.IndexOf(CurrentSlide);
            if (index > 0)
            {
                var prevVm = Slides[index - 1];
                ChangeSlide(prevVm);
            }
        }

        private void SetupTimer()
        {
            StopTimer();

            if (Slides == null || Slides.Count == 0) return;

            _timer = new DispatcherTimer();
            _timer.Tick += OnTimerTick;
            _timer.Interval = new TimeSpan(0, 0, 0, 1);

            _timer.Start();
        }

        private void StopTimer()
        {
            if (_timer == null) return;

            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _timer = null;
        }

        // Stops the rotation timer and detaches event handlers so this VM can
        // be collected. Call from the slider window's Closed handler so every
        // closed slide-host window releases its timer.
        public void Shutdown()
        {
            StopTimer();
            _isTimerEnabled = false;
        }

        private ISliderViewControl GetOrCreateViewControl(ISlideType slideType)
        {
            if (!_viewControlsByType.TryGetValue(slideType.SlideType, out var view))
            {
                view = slideType.CreateViewControl();
                _viewControlsByType[slideType.SlideType] = view;
            }
            return view;
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (Slides.Count == 0)
            {
                _timer?.Stop();
                return;
            }

            // Two ways CurrentView can be null here: (a) InitData ran with an
            // empty Slides collection and slides were added afterwards, so the
            // first ChangeSlide call never happened; (b) the current slide's
            // SlideType string doesn't match any registered ISlideType, so the
            // CurrentSlide setter never assigned CurrentView. In both cases we
            // recover by (re)selecting the first slide and skip this tick.
            if (CurrentSlide == null || CurrentView == null || !Slides.Contains(CurrentSlide))
            {
                CurrentSlide = Slides[0];
                _currentSecond = 0;
                OnPropertyChanged("CurrentSecond");
                return;
            }

            _currentSecond++;

            if (_currentSecond >= SlideSeconds)
            {
                int index = Slides.IndexOf(CurrentSlide);
                if (index == Slides.Count - 1)
                {
                    index = 0;
                }
                else
                {
                    index++;
                }

                var slide = Slides[index];
                CurrentSlide = slide;

                _currentSecond = 0;
            }
            else
            {
                CurrentView?.TimerTick();
            }

            OnPropertyChanged("CurrentSecond");
        }

        #endregion
    }
}