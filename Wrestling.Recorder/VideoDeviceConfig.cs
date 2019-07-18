using System.Collections.Generic;

namespace Wrestling.Recorder
{
    public class VideoDeviceConfig
    {
        public VideoDeviceConfig()
        {
            Resolutions = new List<VideoDeviceResolutionConfig>();
        }

        public string Name { get; set; }
        public string ID { get; set; }
        public List<VideoDeviceResolutionConfig> Resolutions { get; set; }
    }
}