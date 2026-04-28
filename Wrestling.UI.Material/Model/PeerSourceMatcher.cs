using System;
using System.Collections.Generic;

namespace Wrestling.UI.Material.Model
{
    // Helpers for deciding whether two ImportSources entries point at the same
    // physical peer. Two strings are "same host" when at least one of their
    // candidates resolves to the same network host — e.g. an HTTP-only entry
    // and a packed "http+unc" entry that both target 192.168.88.247.
    //
    // Background: a peer that re-announces with a newly-configured SelfUncPath
    // produces a packed source string that is byte-different from the older
    // HTTP-only string an operator may have already accepted. Without this
    // matcher, AddDiscoveredPeer would treat the two as independent entries
    // and the operator ends up polling the same laptop twice through two
    // ListView lines.
    public static class PeerSourceMatcher
    {
        public static bool SameHost(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            if (a == b) return true;
            var hostsA = ExtractHosts(a);
            if (hostsA.Count == 0) return false;
            var hostsB = ExtractHosts(b);
            if (hostsB.Count == 0) return false;
            foreach (var h in hostsA)
            {
                if (hostsB.Contains(h)) return true;
            }
            return false;
        }

        public static ISet<string> ExtractHosts(string source)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(source)) return result;

            var candidates = source.Split(TournamentImporter.SourceAlternativesSeparator);
            foreach (var raw in candidates)
            {
                var c = raw?.Trim();
                if (string.IsNullOrEmpty(c)) continue;
                var host = ExtractHost(c);
                if (!string.IsNullOrEmpty(host)) result.Add(host);
            }
            return result;
        }

        private static string ExtractHost(string candidate)
        {
            if (candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
                {
                    return string.IsNullOrEmpty(uri.Host) ? null : uri.Host.ToLowerInvariant();
                }
                return null;
            }

            // UNC: \\host\share\... — accept forward slashes too because
            // Windows treats them interchangeably.
            if (candidate.Length >= 2 &&
                (candidate[0] == '\\' || candidate[0] == '/') &&
                (candidate[1] == '\\' || candidate[1] == '/'))
            {
                var rest = candidate.Substring(2);
                int sep = rest.IndexOfAny(new[] { '\\', '/' });
                var host = sep < 0 ? rest : rest.Substring(0, sep);
                return string.IsNullOrEmpty(host) ? null : host.ToLowerInvariant();
            }

            return null;
        }
    }
}
