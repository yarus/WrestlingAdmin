using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class TournamentInfo
    {
        [DataMember]
        public Guid? ID { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public DateTime? StartDate { get; set; }
        [DataMember]
        public string City { get; set; }
        [DataMember]
        public string Address { get; set; }
        [DataMember]
        public string Status { get; set; }
        [DataMember]
        public string MainJudge { get; set; }
        [DataMember]
        public string MainSecretary { get; set; }
        [DataMember]
        public GlobalSettingsInfo Settings { get; set; }
        [DataMember]
        public IEnumerable<AgeWeightGroupInfo> Groups { get; set; }
        [DataMember]
        public IEnumerable<TeamApplicationInfo> TeamApplications { get; set; }
        [DataMember]
        public IEnumerable<WrestlerInfo> Wrestlers { get; set; }
        [DataMember]
        public IEnumerable<MatInfo> Mats { get; set; }
        [DataMember]
        public IEnumerable<SlideChannelInfo> SlideChannels { get; set; }
        // Legacy flat-slide list kept on the DTO for backward-compat load only.
        // Pre-channel .wrt files serialized this; the adapter migrates it into
        // a single default channel on load and never writes it back on save.
        [DataMember]
        public IEnumerable<ScreenSlideInfo> Slides { get; set; }
        [DataMember]
        public string MainJudgeEmail { get; set; }
        [DataMember]
        public string MainJudgePhone { get; set; }
        [DataMember]
        public string MainSecretaryEmail { get; set; }
        [DataMember]
        public string MainSecretaryPhone { get; set; }
        [DataMember]
        public decimal? EntryFee { get; set; }
        [DataMember]
        public string Country { get; set; }
        [DataMember]
        public string HashTag { get; set; }
    }
}