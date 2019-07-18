using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class TournamentMatchInfo
    {
        [DataMember]
        public int BracketNumber { get; set; }
        [DataMember]
        public int RoundNumber { get; set; }
        [DataMember]
        public string RoundName { get; set; }
        [DataMember]
        public int MatchNumber { get; set; }
        [DataMember]
        public Guid? WrestlerInRed { get; set; }
        [DataMember]
        public Guid? WrestlerInBlue { get; set; }
        [DataMember]
        public string NextMatchBracketFullNumber { get; set; }
        [DataMember]
        public int PointsRed { get; set; }
        [DataMember]
        public int PointsBlue { get; set; }
        [DataMember]
        public bool? IsRedWon { get; set; }
        [DataMember]
        public int LastSecondInMatch { get; set; }
        [DataMember]
        public DateTime? StartDateTime { get; set; }
        [DataMember]
        public string Note { get; set; }
        [DataMember]
        public string Status { get; set; }
        [DataMember]
        public string WinType { get; set; }
        [DataMember]
        public int MaxRoundSecond { get; set; }
        [DataMember]
        public int MaxTimeoutSecond { get; set; }
        [DataMember]
        public int MaxActionSecond { get; set; }
        [DataMember]
        public IEnumerable<MatchActionInfo> MatchActions { get; set; }
        [DataMember]
        public int WarningsNumberBlue { get; set; }
        [DataMember]
        public int WarningsNumberRed { get; set; }
    }
}