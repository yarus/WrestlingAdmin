using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class GlobalSettingsInfo
    {
        public GlobalSettingsInfo()
        {
            // Safe defaults so old .wrt files (missing these fields) deserialize
            // into sensible values. Newtonsoft.Json invokes this constructor
            // before overlaying JSON properties, so explicit "IsBackupEnabled":
            // false in a saved file still wins.
            IsBackupEnabled = true;
            MaxBackupCount = 10;
        }

        [DataMember]
        public bool IsTimerBackward { get; set; }
        [DataMember]
        public bool IsSoundEnabled { get; set; }
        [DataMember]
        public bool IsAutosaveEnabled { get; set; }
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
        [DataMember]
        public bool IsBackupEnabled { get; set; }
        [DataMember]
        public int MaxBackupCount { get; set; }
        [DataMember]
        public string BackupFolderPath { get; set; }
    }
}