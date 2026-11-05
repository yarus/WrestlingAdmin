using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using Wrestling.Entities;
using Wrestling.Providers.Network;
using Wrestling.UI.Material.Tournament;

namespace Wrestling.UI.Material.Model
{
    // All mutable state belongs to the UI thread. Incoming peer objects are mutable,
    // so capture their values before dispatching (never enqueue the peer itself).
    public sealed class MatchActivityService : IDisposable
    {
        private sealed record Presence(Guid TournamentId, ActiveMatchIdentity Match, long Revision,
            string Name, string ComputerName, bool Connected, DiscoveredPeer Connection);

        private readonly IPeerDiscoveryService _discovery;
        private readonly IDataContext _context;
        private readonly Dispatcher _dispatcher;
        private readonly Action<string> _notify;
        private readonly Dictionary<Guid, Presence> _peers = new Dictionary<Guid, Presence>();
        private ActiveMatchIdentity _localMatch;
        private bool _disposed;

        public event Action Changed;

        public MatchActivityService(IPeerDiscoveryService discovery, IDataContext context,
            Dispatcher dispatcher = null, Action<string> notify = null)
        {
            _discovery = discovery;
            _context = context;
            _dispatcher = dispatcher;
            _notify = notify;
            discovery.PeerUpserted += OnPeerUpserted;
            discovery.PeerExpired += OnPeerExpired;
            context.TournamentChanged += OnTournamentChanged;
            foreach (var peer in discovery.SnapshotPeers()) OnPeerUpserted(this, peer);
        }

        private ActiveMatchIdentity Identify(WrestlingMatch match)
        {
            if (match == null || match.IsMatchCompleted) return null;
            var group = _context.Tournament?.Groups.FirstOrDefault(g => g.ID == match.GroupID);
            // Detached / standalone match-control windows must never claim a tournament slot.
            if (group?.Bracket == null || !group.Bracket.Rounds.SelectMany(r => r.RoundMatches).Contains(match)) return null;
            return new ActiveMatchIdentity(match.GroupID, group.BracketVersion, match.BracketFullNumber);
        }

        public void Open(WrestlingMatch match)
        {
            _localMatch = Identify(match);
            _discovery.SetActiveMatch(_localMatch);
        }

        public void Release()
        {
            _localMatch = null;
            _discovery.SetActiveMatch(null);
        }

        public MatchActivityStatus GetStatus(WrestlingMatch match)
        {
            var key = Identify(match);
            if (key == null) return MatchActivityStatus.Empty;
            var owners = _peers.Where(p => p.Value.TournamentId == _context.Tournament.ID && p.Value.Match == key)
                .OrderBy(p => p.Value.Name, StringComparer.CurrentCulture).ThenBy(p => p.Key).ToList();
            if (owners.Count == 0) return MatchActivityStatus.Empty;
            var names = owners.Select(p => owners.Count(q => q.Value.Name == p.Value.Name) > 1
                ? $"{p.Value.Name} ({p.Value.ComputerName}, {p.Key.ToString().Substring(0, 8)})" : p.Value.Name);
            return new MatchActivityStatus(string.Join(", ", names), owners.Any(p => !p.Value.Connected));
        }

        private void OnPeerUpserted(object sender, DiscoveredPeer peer)
        {
            if (!peer.ActivityRevision.HasValue) return;
            var instance = peer.InstanceId;
            var presence = new Presence(peer.TournamentId, peer.ActiveMatch, peer.ActivityRevision.Value,
                string.IsNullOrWhiteSpace(peer.NodeName) ? peer.ComputerName : peer.NodeName, peer.ComputerName, true, peer);
            var tournament = _context.Tournament;
            RunUi(() =>
            {
                if (!ReferenceEquals(tournament, _context.Tournament) || presence.TournamentId != _context.Tournament?.ID) return;
                _peers.TryGetValue(instance, out var previous);
                // Keep the revision even after expiry so a late datagram cannot resurrect a released bout.
                if (previous != null && presence.Revision < previous.Revision) return;
                if (previous == presence) return;
                _peers[instance] = presence;
                Changed?.Invoke();
                if (_localMatch != null && presence.Match == _localMatch && previous?.Match != _localMatch)
                    _notify?.Invoke(string.Format(TournamentViewModelBase.T("MatchActivity_Concurrent",
                        "Эта схватка также открыта на ковре «{0}»."), presence.Name));
            });
        }

        private void OnPeerExpired(object sender, DiscoveredPeer peer)
        {
            var instance = peer.InstanceId;
            var tournament = _context.Tournament;
            RunUi(() =>
            {
                if (!ReferenceEquals(tournament, _context.Tournament)) return;
                if (_peers.TryGetValue(instance, out var previous) && previous.Connected && ReferenceEquals(previous.Connection, peer))
                {
                    _peers[instance] = previous with { Connected = false };
                    Changed?.Invoke();
                }
            });
        }

        private void OnTournamentChanged(object sender, Entities.Tournament tournament)
        {
            Release();
            _peers.Clear();
            Changed?.Invoke();
        }

        private void RunUi(Action action)
        {
            void Apply() { if (!_disposed) action(); }
            if (_dispatcher == null || _dispatcher.CheckAccess()) Apply();
            else if (!_dispatcher.HasShutdownStarted) _dispatcher.BeginInvoke((Action)Apply);
        }

        public void Dispose()
        {
            Release();
            _disposed = true;
            _discovery.PeerUpserted -= OnPeerUpserted;
            _discovery.PeerExpired -= OnPeerExpired;
            _context.TournamentChanged -= OnTournamentChanged;
        }
    }

    public sealed record MatchActivityStatus(string Label, bool ConnectionLost)
    {
        public static readonly MatchActivityStatus Empty = new MatchActivityStatus(string.Empty, false);
        public bool IsOccupied => !string.IsNullOrEmpty(Label);
        public string ToolTip => string.Format(TournamentViewModelBase.T("MatchActivity_ToolTip",
            "Схватка открыта на ковре «{0}»."), Label) + (ConnectionLost
                ? " " + TournamentViewModelBase.T("MatchActivity_ConnectionLost", "Связь потеряна — возможно, схватка продолжается.") : string.Empty);
    }
}
