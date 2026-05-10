using System;
using System.Runtime.Serialization;

namespace Wrestling.Data
{
    [DataContract]
    public class MatchActionInfo
    {
        [DataMember]
        public DateTime DateTime { get; set; }
        [DataMember]
        public int RoundNumber { get; set; }
        [DataMember]
        public int SecondInRound { get; set; }

        // Free-form Russian description. Kept in the schema so a .wrt written
        // by the new app stays readable by an older client that didn't know
        // about Type. New code populates this from MatchActionDescriber based
        // on Type+IsForRed+Points; legacy files predating Type rely on this
        // field for type inference on load (LegacyMatchActionTypeInferrer).
        [DataMember]
        public string Text { get; set; }

        [DataMember]
        public bool? IsForRed { get; set; }
        [DataMember]
        public int Points { get; set; }

        // Discriminator added 2026-05-10. Stored as string for forward
        // compatibility (new variants can be inserted without renumbering an
        // int enum). Missing/empty on legacy files → adapter infers from Text.
        [DataMember]
        public string Type { get; set; }
    }
}
