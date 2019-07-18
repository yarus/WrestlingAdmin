using System;
using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class MatchActionInfo
    {
        [DataMember]
        public DateTime DateTime { get; set; }
        [DataMember]
        public int RoundNumber { get; set; }
        [DataMember]
        public int SecondInRound { get; set; }
        [DataMember]
        public string Text { get; set; }
        [DataMember]
        public bool? IsForRed { get; set; }
        [DataMember]
        public int Points { get; set; }
    }
}