using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class GlobalSettingsInfo
    {
        [DataMember]
        public bool IsTimerBackward { get; set; }
        [DataMember]
        public bool IsSoundEnabled { get; set; }
        [DataMember]
        public bool IsAutosaveEnabled { get; set; }
        [DataMember]
        public int AutosaveMaxSecond { get; set; }
        [DataMember]
        public int SliderMaxSecond { get; set; }
        [DataMember]
        public int SliderOpacityValue { get; set; }
        [DataMember]
        public string SliderBackgroundImagePath { get; set; }
        [DataMember]
        public string StartGongSoundPath { get; set; }
        [DataMember]
        public string EndGongSoundPath { get; set; }
        [DataMember]
        public int MaxRoundSecond { get; set; }
        [DataMember]
        public int MaxTimeoutSecond { get; set; }
        [DataMember]
        public int MaxActionSecond { get; set; }
        [DataMember]
        public bool IsTournamentScoreInternational { get; set; }
        [DataMember]
        public bool IsOverlayOlympic { get; set; }
    }
}