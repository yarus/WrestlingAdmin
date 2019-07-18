using System;
using System.Runtime.Serialization;

namespace Wrestling.Recorder
{
    [DataContract]
    public class RecorderConfiguration
    {
        [DataMember]
        public string VideoDeviceID { get; set; }
        [DataMember]
        public int? VideoHeight { get; set; }
        [DataMember]
        public int? VideoWidth { get; set; }
        [DataMember]
        public int? VideoFrameRate { get; set; }
        /*ultrafast
        superfast
        veryfast
        faster
        fast
        medium – default preset
        slow
        slower
        veryslow */
        [DataMember]
        public string Preset { get; set; } = "medium";
        [DataMember]
        public Guid? AudioDeviceID { get; set; }
        [DataMember]
        public int VQuality { get; set; } = 1; //Качество видео 1..30. 1 - крутое, 30 - самое не очень
        [DataMember]
        public String VBitrate { get; set; } = "8000K"; //Больше 8000К для FullHD не нужно
        [DataMember]
        public String ABitrate { get; set; } = "96K"; //Битрейт аудио
        [DataMember]
        public String AFrequency { get; set; } = "22050"; //Максимальная частота дискретизации
        [DataMember]
        public String VCodec { get; set; } = "mpeg4";
        [DataMember]
        public String ACodec { get; set; } = "libfdk_aac"; //aac
    }
}