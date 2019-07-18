using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class ScreenSlideInfo
    {
        [DataMember]
        public string Title { get; set; }
        [DataMember]
        public string SlideType { get; set; }
        [DataMember]
        public int Duration { get; set; }
        [DataMember]
        public Dictionary<string, object> NamedValues { get; set; }
    }
}