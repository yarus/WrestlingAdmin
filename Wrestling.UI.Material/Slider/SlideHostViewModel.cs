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

        private ObservableCollection<ScreenSlide> _slides;
        private List<ISlideType> _slideTypes;
       
        private ICommand _changePageCommand;
        private ICommand _prevPageCommand;
        private ICommand _nextPageCommand;

        #endregion

        public SlideHostViewModel(IDiContainer container) : base(container)
        {
        }
        
        public override void InitData()
        {
            base.InitData();

            Slides = DataContext.Tournament.Slides;

            _slideTypes = Resolve<List<ISlideType>>();

            if (Tournament.Settings.SliderMaxSecond == 0 && SlideSeconds == 0)
            {
                SlideSeconds = GlobalSettings.SliderMaxSecond;
            }
            else
            {
                SlideSeconds = Tournament.Settings.SliderMaxSecond;
            }

            if (_slides.Count > 0)
            {
                ChangeSlide(_slides[0]);
            }

            IsTimerEnabled = true;
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

                return "Турнир";
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
                        CurrentView = slideType.ViewControl;
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
                        p => PreviusViewModel(),
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
        
        private void PreviusViewModel()
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
            if (Slides == null || Slides.Count == 0) return;

            _timer?.Stop();

            _timer = new DispatcherTimer();
            _timer.Tick += OnTimerTick;
            _timer.Interval = new TimeSpan(0, 0, 0, 1);

            _timer.Start();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
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
                CurrentView.TimerTick();
            }

            OnPropertyChanged("CurrentSecond");
        }

        #endregion
    }
}