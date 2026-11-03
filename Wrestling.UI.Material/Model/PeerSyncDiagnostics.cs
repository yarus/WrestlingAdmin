using System;
using System.Collections.Generic;

namespace Wrestling.UI.Material.Model
{
    public enum PeerSyncPhase { Downloading, Applying, Synchronized, NoNewData, AwaitingSavedFile, Conflict, FileUnavailable, TournamentMismatch, Error, SaveFailed, LocalAhead }

    // Session-scoped operation results shared by the engine and status UI.
    // All production callers run on the dispatcher, like the tournament graph.
    public sealed class PeerSyncDiagnostics
    {
        private readonly Dictionary<Guid, PeerSyncDiagnostic> _peers = new();
        private readonly object _lock = new();
        public event Action Changed;
        public PeerSyncDiagnostic Get(Guid peer) { lock (_lock) return _peers.TryGetValue(peer, out var diagnostic) ? diagnostic : null; }
        public void Clear() { lock (_lock) _peers.Clear(); Changed?.Invoke(); }
        public void Remove(Guid peer) { lock (_lock) _peers.Remove(peer); Changed?.Invoke(); }

        public void Report(Guid peer, string address, PeerSyncPhase phase, DateTime now, int imported = 0,
            string detail = null, IReadOnlyList<ImportConflict> conflicts = null)
        {
            lock (_lock)
            {
                var previous = Get(peer);
                var success = phase is PeerSyncPhase.Synchronized or PeerSyncPhase.NoNewData or PeerSyncPhase.LocalAhead;
                _peers[peer] = new PeerSyncDiagnostic(address, phase, now,
                    success ? now : previous?.LastSuccessUtc, imported, detail, conflicts ?? Array.Empty<ImportConflict>(), null);
            }
            Changed?.Invoke();
        }

        public void SetRetry(Guid peer, DateTime retry)
        {
            lock (_lock)
                if (_peers.TryGetValue(peer, out var state)) _peers[peer] = state with { RetryAtUtc = retry };
            Changed?.Invoke();
        }
    }

    public sealed record PeerSyncDiagnostic(string Address, PeerSyncPhase Phase, DateTime LastAttemptUtc,
        DateTime? LastSuccessUtc, int ImportedCount, string Detail, IReadOnlyList<ImportConflict> Conflicts, DateTime? RetryAtUtc);
}
