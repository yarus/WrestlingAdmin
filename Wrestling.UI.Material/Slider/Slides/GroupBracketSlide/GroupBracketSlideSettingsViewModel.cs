using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.GroupBracketSlide
{
    public class GroupBracketSlideSettingsViewModel : ViewModelBase, ISliderSettingsControl
    {
        private ObservableCollection<Carpet> _carpets;
        private ObservableCollection<AgeWeightGroup> _groups;
        private AgeWeightGroup _selectedGroup;
        private Carpet _selectedCarpet;
        private ScreenSlide _item;

        private int _sliderOpacityValue;
        private string _sliderBackgroundImagePath;

        private ICommand _setSliderBackgroundCommand;

        public GroupBracketSlideSettingsViewModel(IDiContainer container) : base(container)
        {
        }

        public override void InitData()
        {
            base.InitData();

            Carpets = DataContext.Tournament.Carpets;

            Groups = DataContext.Tournament.Groups;
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

        public AgeWeightGroup SelectedGroup
        {
            get { return _selectedGroup; }
            set
            {
                _selectedGroup = value;

                _item.SetNamedValue("GroupID", _selectedGroup?.ID);
                
                OnPropertyChanged("SelectedGroup");
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

                OnPropertyChanged("SelectedCarpet");
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

        public ObservableCollection<AgeWeightGroup> Groups
        {
            get { return _groups; }
            set
            {
                _groups = value;
                OnPropertyChanged("Groups");
            }
        }

        public void InitContext(ScreenSlide slide)
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

            var groupID = _item.GetNamedValue("GroupID");
            if (groupID != null)
            {
                var groupGuid = new Guid(groupID.ToString());

                if (SelectedCarpet != null)
                {
                    SelectedGroup = SelectedCarpet.Groups.FirstOrDefault(g => g.ID == groupGuid);
                }
                else
                {
                    SelectedGroup = DataContext.Tournament.Groups.FirstOrDefault(g => g.ID == groupGuid);
                }
            }
            else
            {
                SelectedGroup = null;
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

        private void SetSliderBackground()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = "Открыть файл с изображением",
                InitialDirectory = string.IsNullOrEmpty(SliderBackgroundImagePath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : SliderBackgroundImagePath,
                Filter = "Изображения (*.jpg)|*.jpg|All Files (*.*)|*.*"
            };

            bool? success = Dialog.ShowOpenFileDialog(this, settings);
            if (success == true)
            {
                SliderBackgroundImagePath = settings.FileName;
            }
        }
    }
}