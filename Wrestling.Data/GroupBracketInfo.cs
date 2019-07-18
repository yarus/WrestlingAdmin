using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class GroupBracketInfo
    {
        [DataMember]
        public string BracketTypeLabel { get; set; }
        [DataMember]
        public string BracketTypeCode { get; set; }
        [DataMember]
        public int WrestlersCount { get; set; }
        [DataMember]
        public List<GroupRoundInfo> Rounds { get; set; }
        [DataMember]
        public int MatchesCount { get; set; }
        [DataMember]
        public int CompletedMatchesCount { get; set; }
    }
}