using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class AgeWeightGroupInfo
    {
        [DataMember]
        public Guid ID { get; set; }
        [DataMember]
        public IEnumerable<Guid> Wrestlers { get; set; }
        [DataMember]
        public GroupBracketInfo Bracket { get; set; }
        [DataMember]
        public int MaxRoundSecond { get; set; }
        [DataMember]
        public int MaxTimeoutSecond { get; set; }
        [DataMember]
        public int MaxActionSecond { get; set; }
        [DataMember]
        public string MatLabel { get; set; }
        [DataMember]
        public Guid? MatID { get; set; }
        [DataMember]
        public Guid? PartID { get; set; }
        [DataMember]
        public bool IsFemale { get; set; }
        [DataMember]
        public int? BirthYearMin { get; set; }
        [DataMember]
        public int? BirthYearMax { get; set; }
        [DataMember]
        public double? WeightMax { get; set; }
        [DataMember]
        public int FieldsVersion { get; set; }
        [DataMember]
        public int BracketVersion { get; set; }
    }
}