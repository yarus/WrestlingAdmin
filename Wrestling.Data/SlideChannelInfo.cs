using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class SlideChannelInfo
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public IEnumerable<ScreenSlideInfo> Slides { get; set; }
        [DataMember]
        public int SliderMaxSecond { get; set; }
    }
}
