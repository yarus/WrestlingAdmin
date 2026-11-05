using System;
using System.Text;
using Newtonsoft.Json;

namespace Wrestling.Providers.Network
{
    // Wire format for UDP peer discovery announcements. Serialized as JSON UTF-8
    // and broadcast on the LAN every 5 seconds.
    public sealed class PeerAdvertisement
    {
        public const int CurrentProto = 1;

        [JsonProperty("proto")]
        public int Proto { get; set; } = CurrentProto;

        [JsonProperty("instanceId")]
        public Guid InstanceId { get; set; }

        [JsonProperty("tournamentId")]
        public Guid TournamentId { get; set; }

        [JsonProperty("tournamentTitle")]
        public string TournamentTitle { get; set; }

        [JsonProperty("nodeName")]
        public string NodeName { get; set; }

        [JsonProperty("computerName")]
        public string ComputerName { get; set; }

        [JsonProperty("httpUrl")]
        public string HttpUrl { get; set; }

        [JsonProperty("appVersion")]
        public string AppVersion { get; set; }

        // Compact fingerprint of the sender's tournament state (groups +
        // bracket/match versions). Receivers compare against their own to
        // decide whether a pull is needed. Empty when the sender hasn't yet
        // computed it (first announce after open).
        [JsonProperty("stateHash")]
        public string StateHash { get; set; }

        [JsonProperty("activeMatch")]
        public ActiveMatchIdentity ActiveMatch { get; set; }

        // Null identifies older senders which do not advertise match presence.
        [JsonProperty("activityRevision")]
        public long? ActivityRevision { get; set; }

        [JsonProperty("sentAt")]
        public DateTime SentAt { get; set; }

        public byte[] ToBytes()
        {
            var json = JsonConvert.SerializeObject(this);
            return Encoding.UTF8.GetBytes(json);
        }

        // Returns null when the bytes are not a parseable UTF-8 JSON object —
        // the receive loop just skips the datagram.
        public static PeerAdvertisement TryFromBytes(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            try
            {
                var json = Encoding.UTF8.GetString(data);
                return JsonConvert.DeserializeObject<PeerAdvertisement>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
