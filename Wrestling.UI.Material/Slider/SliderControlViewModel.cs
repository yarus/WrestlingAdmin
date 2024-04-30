using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Material.Slider.Slides.GroupBracketSlide;
using Wrestling.UI.Material.Tournament;
using Wrestling.UI.Material.Tournament.Dashboard;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider
{
    public class SliderControlViewModel : TournamentViewModelBase
    {
        //private ObservableCollection<Carpet> _carpets;
        private ObservableCollection<AgeWeightGroup> _groups;
        private ObservableCollection<ScreenSlide> _slides;

        private ICommand _deleteSlideCommand;
        private ICommand _upSlideCommand;
        private ICommand _downSlideCommand;
        private ICommand _addSlideCommand;
        private ICommand _editSlideCommand;
        private ICommand _deleteAllSlidesCommand;

        private IPanelView _slider;
        private SlideHostViewModel _slideHostVm;

        private IList<CommandButtonItem> _quickButtons;

        public SliderControlViewModel(IDiContainer container) : base(container)
        {
        }

        public override string PageTitle => "Управление слайдером";

        public override void InitData()
        {
            base.InitData();

            _slider = Resolve<IPanelView>("SlideHost");
            _slideHostVm = Resolve<SlideHostViewModel>();

            //_carpets = DataContext.Tournament.Carpets;
            _groups = DataContext.Tournament.Groups;

            _slides = DataContext.Tournament.Slides;

            if (_slides.Count == 0)
            {
                InitDefaultSlides();
            }
        }

        public override IList<CommandButtonItem> QuickButtons
        {
            get
            {
                return _quickButtons ??
                       (
                           _quickButtons = new List<CommandButtonItem>
                           {
                               new CommandButtonItem("Открыть слайдер", PackIconKind.ImageFilter, new RelayCommand(param => OpenSlider(), param => true))
                           }
                       );
            }
        }

        public override bool IsBackButtonAvailable => true;

        protected override void OnBackCommand()
        {
            base.OnBackCommand();

            NavigateToView<DashboardViewModel>();
        }

        public SlideHostViewModel Host
        {
            get { return _slideHostVm; }
            set
            {
                _slideHostVm = value;

                OnPropertyChanged("Host");
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

        public ICommand DeleteAllSlidesCommand
        {
            get
            {
                if (_deleteAllSlidesCommand == null)
                {
                    _deleteAllSlidesCommand = new RelayCommand(param => DeleteAllSlides(), param => true);
                }
                return _deleteAllSlidesCommand;
            }
        }


        public ICommand DeleteSlideCommand
        {
            get
            {
                if (_deleteSlideCommand == null)
                {
                    _deleteSlideCommand = new RelayCommand(param => DeleteSlide(param as ScreenSlide), param => param != null);
                }
                return _deleteSlideCommand;
            }
        }
        public ICommand UpSlideCommand
        {
            get
            {
                if (_upSlideCommand == null)
                {
                    _upSlideCommand = new RelayCommand(param => UpSlide(param as ScreenSlide), param => param != null);
                }
                return _upSlideCommand;
            }
        }
        public ICommand DownSlideCommand
        {
            get
            {
                if (_downSlideCommand == null)
                {
                    _downSlideCommand = new RelayCommand(param => DownSlide(param as ScreenSlide), param => param != null);
                }
                return _downSlideCommand;
            }
        }

        public ICommand AddSlideCommand
        {
            get
            {
                if (_addSlideCommand == null)
                {
                    _addSlideCommand = new RelayCommand(param => AddSlide(), param => true);
                }
                return _addSlideCommand;
            }
        }

        public ICommand EditSlideCommand
        {
            get
            {
                if (_editSlideCommand == null)
                {
                    _editSlideCommand = new RelayCommand(param => EditSlide(param as ScreenSlide), param => param != null);
                }
                return _editSlideCommand;
            }
        }

        private void OpenSlider()
        {
            _slideHostVm.InitData();
            _slider.ShowScreen(_slideHostVm);
        }

        private async void EditSlide(ScreenSlide slide)
        {
            var tmpSlide = slide.Clone() as ScreenSlide;

            var vm = new AddSlideViewModel(DiContainer)
            {
                Item = tmpSlide
            };

            var view = new AddSlideDialog
            {
                DataContext = vm
            };

            vm.InitData();

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                slide.Sync(tmpSlide);
            }
        }

        private async void AddSlide()
        {
            var vm = new AddSlideViewModel(DiContainer)
            {
                Item = new ScreenSlide {Duration = DataContext.Tournament.Settings.SliderMaxSecond}
            };

            var view = new AddSlideDialog
            {
                DataContext = vm
            };

            vm.InitData();

            var result = await DialogHost.Show(view, "RootDialog");

            if (result != null && (bool)result)
            {
                Slides.Add(vm.Item);

                _slideHostVm.InitData();
            }
        }

        private void UpSlide(ScreenSlide slide)
        {
            var i = Slides.IndexOf(slide);
            var j = i - 1;
            if (j >= 0)
            {
                Slides.Swap(i, j);
            }
        }

        private void DownSlide(ScreenSlide slide)
        {
            var i = Slides.IndexOf(slide);
            var j = i + 1;
            if (j < Slides.Count)
            {
                Slides.Swap(i, j);
            }
        }

        private void DeleteAllSlides()
        {
            if (Dialog.ShowMessageBox(this, "Вы уверены, что хотите удалить все слайды?", "Требуется подтверждение", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

            Slides = new ObservableCollection<ScreenSlide>();
            DataContext.Tournament.Slides = Slides;

        }

        private void DeleteSlide(ScreenSlide slide)
        {
            var slides = new ObservableCollection<ScreenSlide>(Slides);
            slides.Remove(slide);
            Slides = slides;
            DataContext.Tournament.Slides = slides;

            _slideHostVm.InitData();
        }

        private void InitDefaultSlides()
        {
            var groupBracketSlide = new GroupBracketSlide(DiContainer);
            foreach (var group in _groups)
            {
                var slide = new ScreenSlide
                {
                    Title = group.Name,
                    SlideType = groupBracketSlide.SlideType,
                    Duration = DataContext.Tournament.Settings.SliderMaxSecond
                };

                slide.NamedValues.Add("GroupID", group.ID);

                _slides.Add(slide);
            }

            DataContext.Tournament.Slides = _slides;
        }
    }
}