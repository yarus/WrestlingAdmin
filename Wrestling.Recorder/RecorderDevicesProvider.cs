using System.Collections.Generic;
using System.Linq;
using Accord.DirectSound;
using Accord.Video.DirectShow;

namespace Wrestling.Recorder
{
    public static class RecorderDevicesProvider
    {
        private static List<AudioDeviceInformation> _audioDevices;

        private static List<VideoDeviceConfig> _videoDevices;

        public static List<AudioDeviceInformation> AudioDevices
        {
            get
            {
                if (_audioDevices == null)
                {
                    var devices = new List<AudioDeviceInfo>(new AudioDeviceCollection(AudioDeviceCategory.Capture));

                    _audioDevices = new List<AudioDeviceInformation>();

                    foreach (var info in devices)
                    {
                        _audioDevices.Add(new AudioDeviceInformation
                        {
                            ID = info.Guid,
                            Name = info.Description
                        });
                    }
                }

                return _audioDevices;
            }
        }

        public static List<VideoDeviceConfig> VideoDevices
        {
            get
            {
                if (_videoDevices == null)
                {
                    _videoDevices = new List<VideoDeviceConfig>();

                    LoadVideoDeviceConfigs(_videoDevices);
                }

                return _videoDevices;
            }
        }

        private static void LoadVideoDeviceConfigs(IList<VideoDeviceConfig> devices)
        {
            var devicesInfo = (from FilterInfo filterInfo in new FilterInfoCollection(FilterCategory.VideoInputDevice)
                               select new VideoDeviceInformation { DisplayName = filterInfo.Name, UsbId = filterInfo.MonikerString }).ToList();

            foreach (var info in devicesInfo)
            {
                var deviceConfig = new VideoDeviceConfig
                {
                    Name = info.DisplayName,
                    ID = info.UsbId
                };

                var device = new VideoCaptureDevice(deviceConfig.ID);
                foreach (var cap in device.VideoCapabilities)
                {
                    deviceConfig.Resolutions.Add(new VideoDeviceResolutionConfig
                    {
                        Height = cap.FrameSize.Height,
                        Width = cap.FrameSize.Width,
                        AverageFrameRate = cap.AverageFrameRate
                    });
                }

                device.SignalToStop();
                device.WaitForStop();

                devices.Add(deviceConfig);
            }
        }
    }
}