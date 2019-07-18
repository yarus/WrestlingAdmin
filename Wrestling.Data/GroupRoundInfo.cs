using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class GroupRoundInfo
    {
        [DataMember]
        public int RoundNumber { get; set; }
        [DataMember]
        public string RoundName { get; set; }
        [DataMember]
        public string RoundType { get; set; }
        [DataMember]
        public List<TournamentMatchInfo> RoundMatches { get; set; }
    }
}