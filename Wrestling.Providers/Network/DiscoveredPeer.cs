using System;
using System.Net;

namespace Wrestling.Providers.Network
{
    // Read model for peers known to the local instance. The registry mutates
    // instances in place on re-announce; the UI reads the current snapshot.
    public sealed class DiscoveredPeer
    {
        public Guid InstanceId { get; }
        public Guid TournamentId { get; }
        public string TournamentTitle { get; internal set; }
        public string NodeName { get; internal set; }
        public string HttpUrl { get; internal set; }
        public string UncPath { get; internal set; }
        public string AppVersion { get; internal set; }
        public DateTime LastSeenUtc { get; internal set; }
        public IPAddress SenderAddress { get; internal set; }

        internal DiscoveredPeer(Guid instanceId, Guid tournamentId)
        {
            InstanceId = instanceId;
            TournamentId = tournamentId;
        }
    }
}
