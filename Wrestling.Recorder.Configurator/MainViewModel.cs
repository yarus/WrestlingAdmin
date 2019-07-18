using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Accord.Video.DirectShow;
using MvvmDialogs;
using Wrestling.Recorder.Configurator.Annotations;
using Wrestling.Recorder.DataAccess;
using Wrestling.UI.Utils;

namespace Wrestling.Recorder.Configurator
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private const string DEFAULT_CONFIG_FILENAME = "CamConfig.json";
        private const string DEFAULT_TEST_VIDEO_FILENAME = "test.avi";

        #region Private Fields

        private bool _isPlaying;
        private bool _isRecording;
        private readonly ICamView _view;
        
        private string _videoDeviceId;
        private string _outputPath;

        private ICommand _reloadConfigCommand;
        private ICommand _saveConfigCommand;
        private ICommand _openConfigCommand;

        private ICommand _startPlayingCommand;
        private ICommand _stopCommand;
        private ICommand _startRecordingCommand;
        private ICommand _stopRecordingCommand;

        private VideoCaptureDevice _videoSource;
        private IRecorder _camRecorder;

        private VideoDeviceConfig _videoDeviceConfig;
        private VideoDeviceResolutionConfig _selectedResolution;
        private ObservableCollection<VideoDeviceConfig> _videoDevices;
        private ObservableCollection<VideoDeviceResolutionConfig> _videoResolutions;
        private ObservableCollection<AudioDeviceInformation> _audioDevices;
        private AudioDeviceInformation _selectedAudioDevice;
        private ObservableCollection<string> _videoPresets;
        private string _selectedPreset;
        private string _selectedVBitrate;
        private int _selectedVQuality;
        private string _selectedABitrate;

        private readonly IRecorderConfigurationDataAccess _dataAccess;

        #endregion

        public MainViewModel(ICamView view, IRecorderConfigurationDataAccess dataAccess)
        {
            _view = view;
            _dataAccess = dataAccess;

            InitControlsWithConfig(GetConfig());
        }

        public void StopPlaying()
        {
            _view.StopPlaying();

            if (IsRecording)
            {
                _camRecorder.StopRecording();
                IsRecording = false;
            }

            if (_videoSource != null)
            {
                _videoSource.SignalToStop();
                _videoSource.WaitForStop();
            }

            IsPlaying = false;
        }

        #region Command Bindings
        
        public ICommand StartPlayingCommand
        {
            get
            {
                if (_startPlayingCommand == null)
                {
                    _startPlayingCommand = new RelayCommand(
                        param => StartPlaying(),
                        param => true
                    );
                }
                return _startPlayingCommand;
            }
        }

        public ICommand StartRecordingCommand
        {
            get
            {
                if (_startRecordingCommand == null)
                {
                    _startRecordingCommand = new RelayCommand(
                        param => StartRecording(),
                        param => true
                    );
                }
                return _startRecordingCommand;
            }
        }

        public ICommand StopRecordingCommand
        {
            get
            {
                if (_stopRecordingCommand == null)
                {
                    _stopRecordingCommand = new RelayCommand(
                        param => StopRecording(),
                        param => true
                    );
                }
                return _stopRecordingCommand;
            }
        }

        public ICommand StopCommand
        {
            get
            {
                if (_stopCommand == null)
                {
                    _stopCommand = new RelayCommand(
                        param => StopPlaying(),
                        param => true
                    );
                }
                return _stopCommand;
            }
        }

        public ICommand SaveConfigCommand
        {
            get
            {
                if (_saveConfigCommand == null)
                {
                    _saveConfigCommand = new RelayCommand(
                        param => SaveConfig(),
                        param => true
                    );
                }
                return _saveConfigCommand;
            }
        }

        public ICommand OpenConfigCommand
        {
            get
            {
                if (_openConfigCommand == null)
                {
                    _openConfigCommand = new RelayCommand(
                        param => OpenConfig(),
                        param => true
                    );
                }
                return _openConfigCommand;
            }
        }

        public ICommand ReloadConfigCommand
        {
            get
            {
                if (_reloadConfigCommand == null)
                {
                    _reloadConfigCommand = new RelayCommand(
                        param => ReloadConfig(),
                        param => true
                    );
                }
                return _reloadConfigCommand;
            }
        }

        #endregion

        #region Binding Properties

        public bool IsPlaying
        {
            get { return _isPlaying; }
            set
            {
                _isPlaying = value;
                OnPropertyChanged();
            }
        }

        public bool IsRecording
        {
            get { return _isRecording; }
            set
            {
                _isRecording = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<AudioDeviceInformation> AudioDevices
        {
            get
            {
                if (_audioDevices == null)
                {
                    _audioDevices = new ObservableCollection<AudioDeviceInformation>(RecorderDevicesProvider.AudioDevices);
                }

                return _audioDevices;
            }
            set
            {
                _audioDevices = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> VideoPresets
        {
            get
            {
                if (_videoPresets == null)
                {
                    _videoPresets = new ObservableCollection<string>
                    {
                        "ultrafast",
                        "superfast",
                        "veryfast",
                        "faster",
                        "fast",
                        "medium",
                        "slow",
                        "slower",
                        "veryslow"
                    };

                    if (string.IsNullOrEmpty(SelectedPreset))
                    {
                        SelectedPreset = "medium";
                    }
                }

                return _videoPresets;
            }
            set
            {
                _videoPresets = value;
                OnPropertyChanged();
            }
        }

        public string SelectedVBitrate
        {
            get { return _selectedVBitrate; }
            set
            {
                _selectedVBitrate = value;

                OnPropertyChanged();
            }
        }

        public int SelectedVQuality
        {
            get { return _selectedVQuality; }
            set
            {
                _selectedVQuality = value;

                OnPropertyChanged();
            }
        }

        public string SelectedABitrate
        {
            get { return _selectedABitrate; }
            set
            {
                _selectedABitrate = value;

                OnPropertyChanged();
            }
        }

        public string SelectedPreset
        {
            get { return _selectedPreset; }
            set
            {
                _selectedPreset = value;

                OnPropertyChanged();
            }
        }

        public AudioDeviceInformation SelectedAudioDevice
        {
            get { return _selectedAudioDevice; }
            set
            {
                _selectedAudioDevice = value;

                OnPropertyChanged();
            }
        }

        public VideoDeviceResolutionConfig SelectedResolution
        {
            get { return _selectedResolution; }
            set
            {
                _selectedResolution = value;

                OnPropertyChanged();
            }
        }

        public ObservableCollection<VideoDeviceConfig> VideoDevices
        {
            get
            {
                if (_videoDevices == null)
                {
                    _videoDevices = new ObservableCollection<VideoDeviceConfig>(RecorderDevicesProvider.VideoDevices);

                    if (!string.IsNullOrEmpty(_videoDeviceId))
                    {
                        var device = _videoDevices.FirstOrDefault(d => d.ID == _videoDeviceId);
                        if (device != null)
                        {
                            SelectedVideoDevice = device;
                        }
                    }
                }
                return _videoDevices;
            }
            set
            {
                _videoDevices = value;
                OnPropertyChanged();
            }
        }


        public ObservableCollection<VideoDeviceResolutionConfig> VideoResolutions
        {
            get { return _videoResolutions; }
            set
            {
                _videoResolutions = value;
                OnPropertyChanged();
            }
        }

        public VideoDeviceConfig SelectedVideoDevice
        {
            get { return _videoDeviceConfig; }
            set
            {
                _videoDeviceConfig = value;

                VideoResolutions = new ObservableCollection<VideoDeviceResolutionConfig>(_videoDeviceConfig.Resolutions);

                if (VideoResolutions.Count > 0)
                {
                    SelectedResolution = VideoResolutions[0];
                }
                else
                {
                    SelectedResolution = null;
                }

                _videoDeviceId = _videoDeviceConfig.ID;

                OnPropertyChanged();
            }
        }

        #endregion

        #region Private Methods

        private void ReloadConfig()
        {
            InitControlsWithConfig(GetConfig());

            new DialogService().ShowMessageBox(this, $"Конфигурация загружена из файла {DEFAULT_CONFIG_FILENAME}!", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenConfig()
        {
            Process.Start("notepad.exe", Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, DEFAULT_CONFIG_FILENAME));
        }

        private RecorderConfiguration GetConfig()
        {
            RecorderConfiguration result = null;

            try
            {
                var config = _dataAccess.LoadFromFile(DEFAULT_CONFIG_FILENAME);
                result = config;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            if (result == null)
            {
                result = new RecorderConfiguration();
            }

            return result;
        }

        private void OpenRecording()
        {
            if (File.Exists(_outputPath))
            {
                Process.Start(_outputPath);
            }
        }

        private void StopRecording()
        {
            _camRecorder.StopRecording();
            IsRecording = false;

            OpenRecording();
        }

        private void SaveConfig()
        {
            try
            {
                _dataAccess.SaveToFile(GenerateConfig(), DEFAULT_CONFIG_FILENAME);
                new DialogService().ShowMessageBox(this, $"Конфигурация сохранена в файл {DEFAULT_CONFIG_FILENAME}", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                new DialogService().ShowMessageBox(this, "При сохранении конфигурации произошла ошибка!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartPlaying()
        {
            if (IsPlaying || SelectedVideoDevice == null) return;

            _videoSource = new VideoCaptureDevice(SelectedVideoDevice.ID);
            _videoSource.VideoResolution = SelectedResolution != null ? 
                _videoSource.VideoCapabilities.First(x => x.FrameSize.Width == SelectedResolution.Width && x.FrameSize.Height == SelectedResolution.Height && x.AverageFrameRate == SelectedResolution.AverageFrameRate) 
                : _videoSource.VideoCapabilities.First(x => x.FrameSize.Width == 640 && x.FrameSize.Height == 480);

            _view.StartPlaying(_videoSource);

            IsPlaying = true;
        }

        private void StartRecording()
        {
            if (IsRecording || SelectedVideoDevice == null) return;

            if (IsPlaying) StopPlaying();

            _outputPath = GetOutputFilename();

            _camRecorder = CamRecorder.StartRecording(GenerateConfig(), _outputPath);

            IsRecording = true;
        }

        private string GetOutputFilename()
        {
            return Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, DEFAULT_TEST_VIDEO_FILENAME);
        }

        private void InitControlsWithConfig(RecorderConfiguration config)
        {
            if (!string.IsNullOrEmpty(config.VideoDeviceID))
            {
                var device = VideoDevices.FirstOrDefault(d => d.ID == config.VideoDeviceID);
                if (device != null)
                {
                    SelectedVideoDevice = device;

                    if (config.VideoHeight.HasValue && config.VideoWidth.HasValue && config.VideoFrameRate.HasValue)
                    {
                        var res = VideoResolutions.FirstOrDefault(r => r.Height == config.VideoHeight.Value && r.Width == config.VideoWidth.Value && r.AverageFrameRate == config.VideoFrameRate.Value);
                        if (res != null)
                        {
                            SelectedResolution = res;
                        }
                    }
                }
            }

            if (config.AudioDeviceID.HasValue)
            {
                var device = AudioDevices.FirstOrDefault(a => a.ID == config.AudioDeviceID.Value);
                if (device != null)
                {
                    SelectedAudioDevice = device;
                }
            }

            if (!string.IsNullOrEmpty(config.Preset))
            {
                SelectedPreset = config.Preset;
            }

            SelectedVBitrate = config.VBitrate;
            SelectedVQuality = config.VQuality;
            SelectedABitrate = config.ABitrate;
        }

        private RecorderConfiguration GenerateConfig()
        {
            return new RecorderConfiguration
            {
                VideoDeviceID = SelectedVideoDevice != null ? SelectedVideoDevice.ID : string.Empty,
                VideoFrameRate = SelectedResolution?.AverageFrameRate,
                VideoHeight = SelectedResolution?.Height,
                VideoWidth = SelectedResolution?.Width,
                AudioDeviceID = SelectedAudioDevice?.ID,
                Preset = SelectedPreset,
                VBitrate = SelectedVBitrate,
                VQuality = SelectedVQuality,
                ABitrate = SelectedABitrate
            };
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}