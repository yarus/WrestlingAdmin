using System;
using System.Windows.Input;
using System.Collections.ObjectModel;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;
using Wrestling.UI.Utils.Localization;
using System.Linq;

namespace Wrestling.UI.Material.Slider.Slides.UpcomingMatchesSlide
{
    public class UpcomingMatchesSlideSettingsViewModel : ViewModelBase, ISliderSettingsControl
    {
        private ScreenSlide _item;

        private ObservableCollection<Carpet> _carpets;
        private ObservableCollection<AgeWeightGroup> _groups;
        private int _sliderOpacityValue;
        private int _showMatchesCount;
        private string _sliderBackgroundImagePath;
        private Carpet _selectedCarpet;

        private ICommand _setSliderBackgroundCommand;

        // Auto-title state — see GroupBracketSlideSettingsViewModel for the full
        // rationale. Title auto-fills from the slide type + carpet name when
        // the user hasn't typed a custom title.
        private bool _isInitializing;
        private string _lastAutoTitle;

        public UpcomingMatchesSlideSettingsViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            Carpets = DataContext.Tournament.Carpets;
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

        public Carpet SelectedCarpet
        {
            get { return _selectedCarpet; }
            set
            {
                _selectedCarpet = value;

                _item.SetNamedValue("CarpetID", _selectedCarpet?.ID);

                if (_selectedCarpet == null)
                {
                    Groups = DataContext.Tournament.Groups;
                }
                else
                {
                    Groups = _selectedCarpet.Groups;
                }

                if (!_isInitializing)
                {
                    UpdateAutoTitle(BuildAutoTitle(_selectedCarpet?.Name));
                }

                OnPropertyChanged("SelectedCarpet");
            }
        }

        private static string BuildAutoTitle(string carpetName)
        {
            if (string.IsNullOrEmpty(carpetName)) return null;
            var format = LocalizationService.Instance?.T("SlideAutoTitle_Upcoming");
            if (string.IsNullOrEmpty(format) || format == "SlideAutoTitle_Upcoming") format = "Ближайшие Поединки - {0}";
            return string.Format(format, carpetName);
        }

        private void UpdateAutoTitle(string newAutoTitle)
        {
            if (_item == null || string.IsNullOrEmpty(newAutoTitle)) return;

            if (string.IsNullOrWhiteSpace(_item.Title) || _item.Title == _lastAutoTitle)
            {
                _item.Title = newAutoTitle;
                _lastAutoTitle = newAutoTitle;
            }
        }

        public ObservableCollection<AgeWeightGroup> Groups
        {
            get { return _groups; }
            set
            {
                _groups = value;
                OnPropertyChanged("Groups");
            }
        }

        public ObservableCollection<Carpet> Carpets
        {
            get { return _carpets; }
            set
            {
                _carpets = value;
                OnPropertyChanged("Carpets");
            }
        }

        public string SliderBackgroundImagePath
        {
            get { return _sliderBackgroundImagePath; }
            set
            {
                _sliderBackgroundImagePath = value;

                _item.SetNamedValue("SliderBackgroundImagePath", _sliderBackgroundImagePath);

                OnPropertyChanged("SliderBackgroundImagePath");
            }
        }

        public double SliderOpacity => (double)_sliderOpacityValue / 100;

        public int SliderOpacityValue
        {
            get { return _sliderOpacityValue; }
            set
            {
                _sliderOpacityValue = value;

                _item.SetNamedValue("SliderOpacityValue", _sliderOpacityValue);

                OnPropertyChanged("SliderOpacity");
                OnPropertyChanged("SliderOpacityValue");
            }
        }

        public int ShowMatchesCount
        {
            get { return _showMatchesCount; }
            set
            {
                _showMatchesCount = value;

                _item.SetNamedValue("ShowMatchesCount", _showMatchesCount);
            }
        }

        public void InitContext(ScreenSlide slide)
        {
            _isInitializing = true;
            try
            {
                InitContextCore(slide);
            }
            finally
            {
                _lastAutoTitle = BuildAutoTitle(_selectedCarpet?.Name);
                _isInitializing = false;
            }
        }

        private void InitContextCore(ScreenSlide slide)
        {
            InitData();

            _item = slide;

            if (slide == null) return;

            var carpetID = _item.GetNamedValue("CarpetID");
            if (carpetID != null)
            {
                var carpetGuid = new Guid(carpetID.ToString());
                SelectedCarpet = DataContext.Tournament.Carpets.FirstOrDefault(c => c.ID == carpetGuid);
            }
            else
            {
                SelectedCarpet = null;
            }

            var showMatchesCount = _item.GetNamedValue("ShowMatchesCount");
            if (showMatchesCount != null)
            {

                var carpetGuid = new Guid(carpetID.ToString());
                ShowMatchesCount = Convert.ToInt32(showMatchesCount);
            }
            else
            {
                ShowMatchesCount = 4;
            }

            var opacity = _item.GetNamedValue("SliderOpacityValue");
            if (opacity != null)
            {
                SliderOpacityValue = Convert.ToInt32(opacity);
            }
            else
            {
                SliderOpacityValue = DataContext.Tournament.Settings.SliderOpacityValue;
            }

            var background = _item.GetNamedValue("SliderBackgroundImagePath");
            if (background != null)
            {
                SliderBackgroundImagePath = background.ToString();
            }
            else
            {
                SliderBackgroundImagePath = DataContext.Tournament.Settings.SliderBackgroundImagePath;
            }
        }

        private static string T(string key, string fallback)
        {
            var value = LocalizationService.Instance?.T(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }

        private void SetSliderBackground()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = T("OpenImage_DialogTitle", "Открыть файл с изображением"),
                InitialDirectory = string.IsNullOrEmpty(SliderBackgroundImagePath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : SliderBackgroundImagePath,
                Filter = T("ImageFilter", "Изображения (*.jpg)|*.jpg|All Files (*.*)|*.*")
            };

            bool? success = Dialog.ShowOpenFileDialog(this, settings);
            if (success == true)
            {
                _item.SetNamedValue("SliderBackgroundImagePath", settings.FileName);
                SliderBackgroundImagePath = settings.FileName;
            }
        }
    }
}