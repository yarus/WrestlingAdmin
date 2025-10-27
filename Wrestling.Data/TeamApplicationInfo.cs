using System;
using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class TeamApplicationInfo
    {
        [DataMember]
        public Guid ID { get; set; }
        [DataMember]
        public string FullName { get; set; }
        [DataMember]
        public string ShortName { get; set; }
        [DataMember]
        public string HashTag { get; set; }
        [DataMember]
        public string MainCoach { get; set; }
        [DataMember]
        public string Representative { get; set; }
        [DataMember]
        public string Country { get; set; }
        [DataMember]
        public string City { get; set; }
        [DataMember]
        public string FullAddress { get; set; }
        [DataMember]
        public string PhoneNumber { get; set; }
        [DataMember]
        public string Email { get; set; }
        [DataMember]
        public string EmblemPath { get; set; }
    }
}
