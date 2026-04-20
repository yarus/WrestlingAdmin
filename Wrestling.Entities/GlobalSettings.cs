using System;

namespace Wrestling.Entities
{
    public class GlobalSettings : ObservableObject
    {
        public static string DefaultSliderImage = AppDomain.CurrentDomain.BaseDirectory + "Images\\SliderLogo.jpg";
        public static string DefaultStartGongSound = AppDomain.CurrentDomain.BaseDirectory + "Sounds\\SingleGongBeep.wav";
        public static string DefaultEndGongSound = AppDomain.CurrentDomain.BaseDirectory + "Sounds\\TripleGongBeep.wav";
        public static string DefaultVideosPath = AppDomain.CurrentDomain.BaseDirectory + "Videos";

        private bool _isTimerBackward;
        private bool _isSoundEnabled;
        private bool _isAutosaveEnabled;
        private int _autosaveMaxSecond;
        private int _sliderMaxSecond;
        private int _sliderOpacityValue;
        private string _sliderBackgroundImagePath;
        private string _startGongSoundPath;
        private string _endGongSoundPath;
        private int _maxRoundSecond;
        private int _maxTimeoutSecond;
        private int _maxActionSecond;
        private bool _isTournamentScoreInternational;

        private bool _isVideoRecordingEnabled;
        private string _videoStoragePath;
        private bool _isOverlayOlympic;

        private string _integrationUserName;
        private string _integrationPassword;

        private bool _isBackupEnabled;
        private int _maxBackupCount;
        private string _backupFolderPath;

        public GlobalSettings()
        {
            AutosaveMaxSecond = 30;
            SliderMaxSecond = 10;
            SliderOpacityValue = 25;
            SliderBackgroundImagePath = DefaultSliderImage;
            StartGongSoundPath = DefaultStartGongSound;
            EndGongSoundPath = DefaultEndGongSound;
            MaxRoundSecond = 180;
            MaxTimeoutSecond = 30;
            MaxActionSecond = 30;
            IsTournamentScoreInternational = true;
            IsOverlayOlympic = true;
            VideoStoragePath = DefaultVideosPath;
            IsBackupEnabled = true;
            MaxBackupCount = 10;
            BackupFolderPath = string.Empty;
        }

        public string IntegrationUserName
        {
            get { return _integrationUserName; }
            set
            {
                _integrationUserName = value;
                OnPropertyChanged("IntegrationUserName");
            }
        }

        public string IntegrationPassword
        {
            get { return _integrationPassword; }
            set
            {
                _integrationPassword = value;
                OnPropertyChanged("IntegrationPassword");
            }
        }

        public string VideoStoragePath
        {
            get { return _videoStoragePath; }
            set
            {
                _videoStoragePath = value;
                OnPropertyChanged("VideoStoragePath");
            }
        }
        
        public bool IsVideoRecordingEnabled
        {
            get { return _isVideoRecordingEnabled; }
            set
            {
                _isVideoRecordingEnabled = value;
                
                OnPropertyChanged("IsVideoRecordingEnabled");
            }
        }

        public bool IsTournamentScoreInternational
        {
            get { return _isTournamentScoreInternational; }
            set
            {
                _isTournamentScoreInternational = value;
                OnPropertyChanged("IsTournamentScoreInternational");
            }
        }

        public int MaxRoundSecond
        {
            get { return _maxRoundSecond; }
            set
            {
                _maxRoundSecond = value;
                OnPropertyChanged("MaxRoundSecond");
            }
        }

        public int MaxTimeoutSecond
        {
            get { return _maxTimeoutSecond; }
            set
            {
                _maxTimeoutSecond = value;
                OnPropertyChanged("MaxTimeoutSecond");
            }
        }

        public int MaxActionSecond
        {
            get { return _maxActionSecond; }
            set
            {
                _maxActionSecond = value;
                OnPropertyChanged("MaxActionSecond");
            }
        }

        public string StartGongSoundPath
        {
            get { return _startGongSoundPath; }
            set
            {
                _startGongSoundPath = value;
                OnPropertyChanged("StartGongSoundPath");
            }
        }

        public string EndGongSoundPath
        {
            get { return _endGongSoundPath; }
            set
            {
                _endGongSoundPath = value;
                OnPropertyChanged("EndGongSoundPath");
            }
        }

        public string SliderBackgroundImagePath
        {
            get { return _sliderBackgroundImagePath; }
            set
            {
                _sliderBackgroundImagePath = value;
                OnPropertyChanged("SliderBackgroundImagePath");
            }
        }

        public bool IsAutosaveEnabled
        {
            get { return _isAutosaveEnabled; }
            set
            {
                _isAutosaveEnabled = value;
                OnPropertyChanged("IsAutosaveEnabled");
            }
        }

        public double SliderOpacity => (double)_sliderOpacityValue/100;

        public int SliderOpacityValue
        {
            get { return _sliderOpacityValue; }
            set
            {
                _sliderOpacityValue = value;
                OnPropertyChanged("SliderOpacity");
                OnPropertyChanged("SliderOpacityValue");
            }
        }

        public int SliderMaxSecond
        {
            get { return _sliderMaxSecond; }
            set
            {
                _sliderMaxSecond = value;
                OnPropertyChanged("SliderMaxSecond");
            }
        }

        public int AutosaveMaxSecond
        {
            get { return _autosaveMaxSecond; }
            set
            {
                _autosaveMaxSecond = value;
                OnPropertyChanged("AutosaveMaxSecond");
            }
        }

        public bool IsTimerBackward
        {
            get { return _isTimerBackward; }
            set
            {
                _isTimerBackward = value;
                OnPropertyChanged("IsTimerBackward");
            }
        }

        public bool IsSoundEnabled
        {
            get { return _isSoundEnabled; }
            set
            {
                _isSoundEnabled = value;
                OnPropertyChanged("IsSoundEnabled");
            }
        }

        public bool IsOverlayOlympic {
            get { return _isOverlayOlympic; }
            set
            {
                _isOverlayOlympic = value;
                OnPropertyChanged("IsOverlayOlympic");
            }
        }

        public bool IsBackupEnabled
        {
            get { return _isBackupEnabled; }
            set
            {
                _isBackupEnabled = value;
                OnPropertyChanged("IsBackupEnabled");
            }
        }

        public int MaxBackupCount
        {
            get { return _maxBackupCount; }
            set
            {
                _maxBackupCount = value;
                OnPropertyChanged("MaxBackupCount");
            }
        }

        public string BackupFolderPath
        {
            get { return _backupFolderPath; }
            set
            {
                _backupFolderPath = value;
                OnPropertyChanged("BackupFolderPath");
            }
        }
    }
}