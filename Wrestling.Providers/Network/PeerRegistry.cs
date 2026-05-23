using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Wrestling.Providers.Network
{
    // Pure peer-aggregation logic: accepts parsed advertisements, maintains
    // the live set of peers keyed by InstanceId, and raises events when a peer
    // first appears, re-announces, or times out. No sockets here — the service
    // wraps this with UdpClient + timers. Thread-safe (all state under _lock).
    public sealed class PeerRegistry
    {
        private readonly object _lock = new object();
        private readonly Dictionary<Guid, DiscoveredPeer> _byInstance = new Dictionary<Guid, DiscoveredPeer>();
        private readonly TimeSpan _expiry;

        private Guid _selfInstanceId;
        private Guid _currentTournamentId;

        public event EventHandler<DiscoveredPeer> PeerUpserted;
        public event EventHandler<DiscoveredPeer> PeerExpired;

        public PeerRegistry() : this(TimeSpan.FromSeconds(15)) { }

        public PeerRegistry(TimeSpan expiry)
        {
            _expiry = expiry;
        }

        public void SetContext(Guid selfInstanceId, Guid currentTournamentId)
        {
            List<DiscoveredPeer> cleared;
            lock (_lock)
            {
                _selfInstanceId = selfInstanceId;
                _currentTournamentId = currentTournamentId;
                cleared = _byInstance.Values.ToList();
                _byInstance.Clear();
            }
            foreach (var p in cleared)
            {
                PeerExpired?.Invoke(this, p);
            }
        }

        public IReadOnlyCollection<DiscoveredPeer> Snapshot()
        {
            lock (_lock)
            {
                return _byInstance.Values.ToList();
            }
        }

        // Returns a short reason string when the packet is dropped (useful for
        // diagnostics / logs), or null when the peer was accepted / upserted.
        public string Ingest(PeerAdvertisement ad, IPAddress sender, DateTime now)
        {
            if (ad == null) return "null advertisement";
            if (ad.Proto != PeerAdvertisement.CurrentProto) return "unknown proto " + ad.Proto;
            if (ad.InstanceId == Guid.Empty) return "empty instanceId";

            DiscoveredPeer upserted;
            lock (_lock)
            {
                if (ad.InstanceId == _selfInstanceId) return "self";
                if (_currentTournamentId != Guid.Empty && ad.TournamentId != _currentTournamentId)
                    return "tournament mismatch";

                if (!_byInstance.TryGetValue(ad.InstanceId, out var peer))
                {
                    peer = new DiscoveredPeer(ad.InstanceId, ad.TournamentId);
                    _byInstance[ad.InstanceId] = peer;
                }
                peer.TournamentTitle = ad.TournamentTitle;
                peer.NodeName = ad.NodeName;
                peer.HttpUrl = ad.HttpUrl;
                peer.AppVersion = ad.AppVersion;
                peer.StateHash = ad.StateHash ?? string.Empty;
                peer.LastSeenUtc = now;
                peer.SenderAddress = sender;
                upserted = peer;
            }

            PeerUpserted?.Invoke(this, upserted);
            return null;
        }

        public void Tick(DateTime now)
        {
            List<DiscoveredPeer> expired;
            lock (_lock)
            {
                expired = _byInstance.Values.Where(p => now - p.LastSeenUtc > _expiry).ToList();
                foreach (var p in expired)
                {
                    _byInstance.Remove(p.InstanceId);
                }
            }
            foreach (var p in expired)
            {
                PeerExpired?.Invoke(this, p);
            }
        }

        public void Clear()
        {
            List<DiscoveredPeer> cleared;
            lock (_lock)
            {
                cleared = _byInstance.Values.ToList();
                _byInstance.Clear();
            }
            foreach (var p in cleared)
            {
                PeerExpired?.Invoke(this, p);
            }
        }
    }
}
