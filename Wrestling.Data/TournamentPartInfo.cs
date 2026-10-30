using System;
using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class TournamentPartInfo
    {
        [DataMember]
        public Guid ID { get; set; }
        [DataMember]
        public string Name { get; set; }
    }
}
