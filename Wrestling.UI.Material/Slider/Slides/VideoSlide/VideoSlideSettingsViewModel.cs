using System;
using System.Windows.Input;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using Wrestling.Entities;
using Wrestling.UI.Material.Model;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Slider.Slides.VideoSlide
{
    public class VideoSlideSettingsViewModel : ViewModelBase, ISliderSettingsControl
    {
        private ScreenSlide _item;
        
        private string _videoPath;

        private ICommand _setVideoPathCommand;

        public VideoSlideSettingsViewModel(IDiContainer container) : base(container)
        {
        }
        
        public string VideoPath
        {
            get { return _videoPath; }
            set
            {
                _videoPath = value;

                _item.SetNamedValue("VideoPath", _videoPath);

                OnPropertyChanged("VideoPath");
            }
        }
        
        public void InitContext(ScreenSlide slide)
        {
            InitData();

            _item = slide;

            if (slide == null) return;
            
            var path = _item.GetNamedValue("VideoPath");
            if (path != null)
            {
                VideoPath = path.ToString();
            }
        }

        public ICommand SetVideoPathCommand
        {
            get
            {
                if (_setVideoPathCommand == null)
                {
                    _setVideoPathCommand = new RelayCommand(
                        param => SetVideoPath(),
                        param => true
                    );
                }
                return _setVideoPathCommand;
            }
        }

        private void SetVideoPath()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = "Открыть видео файл",
                InitialDirectory = string.IsNullOrEmpty(VideoPath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : VideoPath,
                Filter = "Video (*.avi,*.mp4)|*.avi;*.mp4|All Files (*.*)|*.*"
            };

            bool? success = Dialog.ShowOpenFileDialog(this, settings);
            if (success == true)
            {
                VideoPath = settings.FileName;
            }
        }
    }
}