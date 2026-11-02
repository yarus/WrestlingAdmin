using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Wrestling.Entities;

namespace Wrestling.Providers.Network
{
    // Computes a compact fingerprint of the version-bearing state of a
    // tournament. Two peers with the same fingerprint hold equivalent state
    // for the purposes of import; a difference triggers a pull-on-divergence
    // in PeerSyncService. SHA256 is overkill for collision resistance but is
    // in-box. We truncate to 8 bytes (16 hex chars) to keep the UDP
    // advertisement compact.
    //
    // The canonicalization order (groups by ID, matches by BracketFullNumber)
    // is critical: two peers must compute the same hash for the same state
    // regardless of the in-memory ordering of their ObservableCollections.
    public static class PeerStateHasher
    {
        public static string Compute(Tournament tournament)
        {
            if (tournament == null) return string.Empty;

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                // Tournament-level meta version: bumped on Parts list edits
                // (create / rename / delete). Peers diverge on these so the
                // hash must reflect them.
                w.Write(tournament.MetaVersion);

                // Per-mat fields version: ActivePartID and any future mat-level
                // fields. Order by ID for cross-peer determinism.
                if (tournament.Mats != null)
                {
                    foreach (var mat in tournament.Mats.Where(m => m.ID.HasValue).OrderBy(m => m.ID.Value))
                    {
                        w.Write(mat.ID.Value.GetHashCode());
                        w.Write(mat.FieldsVersion);
                    }
                }

                if (tournament.Groups != null)
                {
                    foreach (var g in tournament.Groups.OrderBy(x => x.ID))
                    {
                        w.Write(g.ID.GetHashCode());
                        w.Write(g.FieldsVersion);
                        w.Write(g.BracketVersion);

                        if (g.Bracket?.Rounds != null)
                        {
                            var matches = g.Bracket.Rounds
                                .SelectMany(r => r.RoundMatches ?? Enumerable.Empty<WrestlingMatch>())
                                .Where(m => m != null)
                                .OrderBy(m => m.BracketFullNumber);
                            foreach (var m in matches)
                            {
                                w.Write(m.BracketFullNumber ?? string.Empty);
                                w.Write(m.SyncVersion);
                                WriteResult(w, m);
                            }
                        }
                    }
                }

                // Registration edits (including Level) are versioned by Timestamp.
                // They must trigger a pull even when the bracket did not change.
                if (tournament.Wrestlers != null)
                {
                    foreach (var wrestler in tournament.Wrestlers.OrderBy(x => x.ID))
                    {
                        w.Write(wrestler.ID.ToByteArray());
                        w.Write(wrestler.Timestamp?.ToUniversalTime().Ticks ?? 0L);
                        w.Write(wrestler.IsNoShow);
                        w.Write(wrestler.IsDisqualified);
                        // Places are a calculated cache, not an authored revision.
                        // Hash the exclusions on which that calculation depends.
                    }
                }

                w.Flush();
                ms.Position = 0;

                using (var sha = SHA256.Create())
                {
                    var full = sha.ComputeHash(ms);
                    var sb = new StringBuilder(16);
                    for (int i = 0; i < 8; i++) sb.Append(full[i].ToString("x2"));
                    return sb.ToString();
                }
            }
        }

        private static void WriteResult(BinaryWriter writer, WrestlingMatch match)
        {
            // Live scores/timers are local until approval. Hashing pending
            // match control would publish a new revision on every clock tick.
            writer.Write(match.IsMatchCompleted);
            if (!match.IsMatchCompleted)
            {
                writer.Write(match.PreviousResults.Count);
                foreach (var previous in match.PreviousResults) WriteResult(writer, previous);
                return;
            }
            writer.Write(match.WrestlerInRed?.ID.ToString() ?? "");
            writer.Write(match.WrestlerInBlue?.ID.ToString() ?? "");
            writer.Write((int?)match.WinType ?? -1);
            writer.Write(match.IsRedWon.HasValue);
            writer.Write(match.IsRedWon ?? false);
            writer.Write(match.PointsRed);
            writer.Write(match.PointsBlue);
            writer.Write(match.WarningsNumberRed);
            writer.Write(match.WarningsNumberBlue);
            writer.Write(match.LastSecondInMatch);
            writer.Write(match.Note ?? "");
            writer.Write(match.MatchActions.Count);
            foreach (var action in match.MatchActions)
            {
                writer.Write((int)action.Type);
                writer.Write(action.Points);
                writer.Write(action.WarningDelta.HasValue);
                if (action.WarningDelta.HasValue) writer.Write(action.WarningDelta.Value);
                writer.Write(action.IsForRed.HasValue);
                writer.Write(action.IsForRed ?? false);
                writer.Write(action.RoundNumber);
                writer.Write(action.SecondInRound);
                writer.Write(action.DateTime.ToUniversalTime().Ticks);
            }
            writer.Write(match.PreviousResults.Count);
            foreach (var previous in match.PreviousResults) WriteResult(writer, previous);
        }
    }
}
