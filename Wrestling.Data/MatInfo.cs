using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class MatInfo
    {
        [DataMember]
        public Guid ID { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public IEnumerable<Guid> Groups { get; set; }
        [DataMember]
        public Guid? ActivePartID { get; set; }
        [DataMember]
        public int FieldsVersion { get; set; }
    }
}