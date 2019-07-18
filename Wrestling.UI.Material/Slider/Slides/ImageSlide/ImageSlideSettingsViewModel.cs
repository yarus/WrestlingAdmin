using System;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.ImageSlide
{
    public class ImageSlideSettingsViewModel : ViewModelBase, ISliderSettingsControl
    {
        private ScreenSlide _item;
        
        private string _imagePath;

        private ICommand _setImagePathCommand;

        public ImageSlideSettingsViewModel(IDiContainer container) : base(container)
        {
        }
        
        public string ImagePath
        {
            get { return _imagePath; }
            set
            {
                _imagePath = value;

                _item.SetNamedValue("ImagePath", _imagePath);

                OnPropertyChanged("ImagePath");
            }
        }
        
        public void InitContext(ScreenSlide slide)
        {
            InitData();

            _item = slide;

            if (slide == null) return;
            
            var path = _item.GetNamedValue("ImagePath");
            if (path != null)
            {
                ImagePath = path.ToString();
            }
        }

        public ICommand SetImagePathCommand
        {
            get
            {
                if (_setImagePathCommand == null)
                {
                    _setImagePathCommand = new RelayCommand(
                        param => SetImagePath(),
                        param => true
                    );
                }
                return _setImagePathCommand;
            }
        }

        private void SetImagePath()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = "Открыть файл с изображением",
                InitialDirectory = string.IsNullOrEmpty(ImagePath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : ImagePath,
                Filter = "Изображения (*.jpg)|*.jpg|All Files (*.*)|*.*"
            };

            bool? success = Dialog.ShowOpenFileDialog(this, settings);
            if (success == true)
            {
                ImagePath = settings.FileName;
            }
        }
    }
}