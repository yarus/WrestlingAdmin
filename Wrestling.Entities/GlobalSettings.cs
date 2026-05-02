using System;

namespace Wrestling.Entities
{
    public class GlobalSettings : ObservableObject
    {
        public static string DefaultSliderImage = AppDomain.CurrentDomain.BaseDirectory + "Images\\SliderLogo.jpg";
        public static string DefaultStartGongSound = AppDomain.CurrentDomain.BaseDirectory + "Sounds\\SingleGongBeep.wav";
        public static string DefaultEndGongSound = AppDomain.CurrentDomain.BaseDirectory + "Sounds\\TripleGongBeep.wav";

        private bool _isTimerBackward;
        private bool _isSoundEnabled;
        private bool _isAutosaveEnabled;
        private int _sliderMaxSecond;
        private int _sliderOpacityValue;
        private string _sliderBackgroundImagePath;
        private string _startGongSoundPath;
        private string _endGongSoundPath;
        private int _maxRoundSecond;
        private int _maxTimeoutSecond;
        private int _maxActionSecond;

        private bool _isOverlayOlympic;

        private string _integrationUserName;
        private string _integrationPassword;

        private bool _isBackupEnabled;
        private int _maxBackupCount;
        private string _backupFolderPath;

        private int _discoveryPort;
        private bool _isHttpServerEnabled;
        private int _httpServerPort;
        private string _nodeName;
        private string _selfUncPath;
        private string _announceIpOverride;

        private string _signatureFooterImagePath;

        public GlobalSettings()
        {
            SliderMaxSecond = 10;
            SliderOpacityValue = 25;
            SliderBackgroundImagePath = DefaultSliderImage;
            StartGongSoundPath = DefaultStartGongSound;
            EndGongSoundPath = DefaultEndGongSound;
            MaxRoundSecond = 180;
            MaxTimeoutSecond = 30;
            MaxActionSecond = 30;
            IsOverlayOlympic = true;
            IsBackupEnabled = true;
            MaxBackupCount = 10;
            BackupFolderPath = string.Empty;
            DiscoveryPort = 24565;
            IsHttpServerEnabled = true;
            HttpServerPort = 24566;
            // Default NodeName to the machine's host name so a fresh tournament
            // is immediately discoverable on the LAN without operator setup.
            // The user can override later from the Settings screen.
            NodeName = SafeMachineName();
            SelfUncPath = string.Empty;
            AnnounceIpOverride = string.Empty;
        }

        private static string SafeMachineName()
        {
            try
            {
                var name = Environment.MachineName;
                return string.IsNullOrWhiteSpace(name) ? string.Empty : name;
            }
            catch
            {
                return string.Empty;
            }
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

        public int DiscoveryPort
        {
            get { return _discoveryPort; }
            set
            {
                _discoveryPort = value;
                OnPropertyChanged("DiscoveryPort");
            }
        }

        public bool IsHttpServerEnabled
        {
            get { return _isHttpServerEnabled; }
            set
            {
                _isHttpServerEnabled = value;
                OnPropertyChanged("IsHttpServerEnabled");
            }
        }

        public int HttpServerPort
        {
            get { return _httpServerPort; }
            set
            {
                _httpServerPort = value;
                OnPropertyChanged("HttpServerPort");
            }
        }

        public string NodeName
        {
            get { return _nodeName; }
            set
            {
                _nodeName = value;
                OnPropertyChanged("NodeName");
            }
        }

        public string SelfUncPath
        {
            get { return _selfUncPath; }
            set
            {
                _selfUncPath = value;
                OnPropertyChanged("SelfUncPath");
            }
        }

        // Manual override for the IP advertised in the HTTP URL announced to
        // peers. Empty string means "auto-pick the first private-range IPv4
        // via LocalIpAddressProbe.PickDefault()" — preserves legacy behavior.
        // Operators set this when a laptop has multiple NICs and the auto
        // pick lands on the wrong subnet (e.g. a hotspot interface instead
        // of the tournament LAN).
        public string AnnounceIpOverride
        {
            get { return _announceIpOverride; }
            set
            {
                _announceIpOverride = value;
                OnPropertyChanged("AnnounceIpOverride");
            }
        }

        // Absolute path to a stamp+signatures image overlaid on the bottom of
        // every printed/exported protocol. Empty/null = no overlay (default).
        public string SignatureFooterImagePath
        {
            get { return _signatureFooterImagePath; }
            set
            {
                _signatureFooterImagePath = value;
                OnPropertyChanged("SignatureFooterImagePath");
            }
        }
    }
}