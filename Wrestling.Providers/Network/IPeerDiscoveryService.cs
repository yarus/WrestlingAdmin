using System;
using System.Collections.Generic;

namespace Wrestling.Providers.Network
{
    public interface IPeerDiscoveryService : IDisposable
    {
        event EventHandler<DiscoveredPeer> PeerUpserted;
        event EventHandler<DiscoveredPeer> PeerExpired;
        event EventHandler<string> DiagnosticMessage;

        IReadOnlyCollection<DiscoveredPeer> SnapshotPeers();

        void StartForTournament(
            int port,
            Guid tournamentId,
            string tournamentTitle,
            string nodeName,
            string httpUrl,
            string uncPath);

        void Stop();
    }
}
