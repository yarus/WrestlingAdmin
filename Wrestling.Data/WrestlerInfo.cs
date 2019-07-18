using System;
using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class WrestlerInfo
    {
        [DataMember]
        public Guid ID { get; set; }

        [DataMember]
        public decimal? PaidAmount { get; set; }

        [DataMember]
        public Guid? TeamID { get; set; }

        [DataMember]
        public Guid? GroupID { get; set; }

        [DataMember]
        public string FirstName { get; set; }

        [DataMember]
        public string LastName { get; set; }

        [DataMember]
        public string MiddleName { get; set; }

        [DataMember]
        public DateTime? BirthDate { get; set; }

        [DataMember]
        public double? Weight { get; set; }

        [DataMember]
        public int? FinalPlace { get; set; }

        [DataMember]
        public bool IsSeedFixed { get; set; }

        [DataMember]
        public int? SeedNumber { get; set; }

        [DataMember]
        public bool IsFemale { get; set; }
        
        [DataMember]
        public bool IsEntryFeePaid { get; set; }
        [DataMember]
        public bool IsWeightApproved { get; set; }
        [DataMember]
        public string HashTag { get; set; }
        [DataMember]
        public string Level { get; set; }
    }
}