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
                                w.Write(m.Version);
                            }
                        }
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
    }
}
