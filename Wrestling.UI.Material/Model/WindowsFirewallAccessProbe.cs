using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Wrestling.UI.Material.Model
{
    internal enum FirewallAccess { Allowed, NotAllowed, Unknown }

    // Read-only, conservative check of inbound Windows Firewall rules. Scoped
    // rules and policy overrides cannot establish LAN reachability: report Unknown.
    internal static class WindowsFirewallAccessProbe
    {
        internal sealed record Rule(int Profiles, int Protocol, string Ports, bool Allow, bool Restricted);

        internal static FirewallAccess Check(int discoveryPort, int? httpPort)
            => CheckApplication(Environment.ProcessPath, discoveryPort, httpPort);

        internal static FirewallAccess CheckApplication(string path, int discoveryPort, int? httpPort)
        {
            object policyObject = null;
            object rulesObject = null;
            try
            {
                if (string.IsNullOrEmpty(path)) return FirewallAccess.Unknown;
                policyObject = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2", true));
                dynamic policy = policyObject;
                int profiles = policy.CurrentProfileTypes;
                if (profiles == 0 || (int)policy.LocalPolicyModifyState != 0) return FirewallAccess.Unknown;

                var rules = new List<Rule>();
                rulesObject = policy.Rules;
                foreach (dynamic rule in (System.Collections.IEnumerable)rulesObject)
                {
                    try
                    {
                        if (!(bool)rule.Enabled || (int)rule.Direction != 1) continue;
                        string app = rule.ApplicationName;
                        if (!string.IsNullOrEmpty(app) && !string.Equals(
                            Environment.ExpandEnvironmentVariables(app).Trim('"'), path, StringComparison.OrdinalIgnoreCase)) continue;
                        int protocol = rule.Protocol;
                        if (protocol != 256 && protocol != 17 && protocol != 6) continue;
                        // A service-specific rule cannot apply to this desktop app.
                        if (!string.IsNullOrEmpty((string)rule.ServiceName)) continue;
                        // Packaged-app rules are not permissions for this unpackaged exe.
                        if (!string.IsNullOrEmpty((string)rule.LocalAppPackageId)) continue;
                        bool restricted = !IsAny((string)rule.LocalAddresses) || !IsAny((string)rule.RemoteAddresses)
                            || (string)rule.InterfaceTypes != "All" || rule.Interfaces != null;
                        if (protocol != 256) restricted |= !IsAny((string)rule.RemotePorts);
                        restricted |= (bool)rule.EdgeTraversal;
                        restricted |= (int)rule.SecureFlags != 0
                            || !string.IsNullOrEmpty((string)rule.LocalUserAuthorizedList)
                            || !string.IsNullOrEmpty((string)rule.RemoteUserAuthorizedList)
                            || !string.IsNullOrEmpty((string)rule.RemoteMachineAuthorizedList)
                            || !string.IsNullOrEmpty((string)rule.LocalUserOwner);
                        rules.Add(new Rule((int)rule.Profiles, protocol,
                            protocol == 256 ? "*" : (string)rule.LocalPorts, (int)rule.Action == 1, restricted));
                    }
                    finally { Marshal.ReleaseComObject(rule); }
                }

                var results = new List<FirewallAccess>();
                foreach (int profile in new[] { 1, 2, 4 })
                {
                    if ((profiles & profile) == 0) continue;
                    // Excluded interfaces and multiple active profiles may differ.
                    if (policy.ExcludedInterfaces[profile] != null) return FirewallAccess.Unknown;
                    bool enabled = policy.FirewallEnabled[profile];
                    bool blockAll = policy.BlockAllInboundTraffic[profile];
                    bool defaultAllow = (int)policy.DefaultInboundAction[profile] == 1;
                    var udp = Evaluate(rules, profile, 17, discoveryPort, enabled, blockAll, defaultAllow);
                    var tcp = FirewallAccess.Allowed;
                    if (httpPort.HasValue)
                        tcp = Evaluate(rules, profile, 6, httpPort.Value, enabled, blockAll, defaultAllow);
                    results.Add(CombinePorts(udp, tcp));
                }
                return Combine(results);
            }
            catch (Exception ex) when (ex is COMException || ex is UnauthorizedAccessException
                || ex is System.Security.SecurityException || ex is TypeLoadException
                || ex is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                return FirewallAccess.Unknown;
            }
            finally
            {
                if (rulesObject != null) Marshal.ReleaseComObject(rulesObject);
                if (policyObject != null) Marshal.ReleaseComObject(policyObject);
            }
        }

        internal static FirewallAccess Combine(IEnumerable<FirewallAccess> results)
        {
            var distinct = results.Distinct().ToArray();
            return distinct.Length == 1 ? distinct[0] : FirewallAccess.Unknown;
        }

        internal static FirewallAccess CombinePorts(FirewallAccess udp, FirewallAccess tcp)
        {
            if (udp == FirewallAccess.NotAllowed || tcp == FirewallAccess.NotAllowed) return FirewallAccess.NotAllowed;
            return udp == FirewallAccess.Unknown || tcp == FirewallAccess.Unknown ? FirewallAccess.Unknown : FirewallAccess.Allowed;
        }

        internal static FirewallAccess Evaluate(IEnumerable<Rule> rules, int profile, int protocol, int port,
            bool enabled = true, bool blockAll = false, bool defaultAllow = false)
        {
            if (!enabled) return FirewallAccess.Allowed;
            if (blockAll) return FirewallAccess.NotAllowed;
            bool allowed = defaultAllow, uncertainAllow = false, uncertainBlock = false;
            foreach (var rule in rules)
            {
                if ((rule.Profiles & profile) == 0 || (rule.Protocol != 256 && rule.Protocol != protocol)) continue;
                bool? matches = MatchesPort(rule.Ports, port);
                if (matches == false) continue;
                bool restricted = rule.Restricted || !matches.HasValue;
                if (!rule.Allow && !restricted) return FirewallAccess.NotAllowed;
                if (!rule.Allow) uncertainBlock = true;
                else if (restricted) uncertainAllow = true;
                else allowed = true;
            }
            if (uncertainBlock || (!allowed && uncertainAllow)) return FirewallAccess.Unknown;
            return allowed ? FirewallAccess.Allowed : FirewallAccess.NotAllowed;
        }

        private static bool IsAny(string value) => string.IsNullOrEmpty(value) || value == "*";

        private static bool? MatchesPort(string ports, int port)
        {
            if (IsAny(ports)) return true;
            bool unknown = false;
            foreach (var item in ports.Split(','))
            {
                var range = item.Trim().Split('-');
                if (range.Length == 1 && int.TryParse(range[0], out int single))
                {
                    if (single == port) return true;
                }
                else if (range.Length == 2 && int.TryParse(range[0], out int first) && int.TryParse(range[1], out int last))
                {
                    if (port >= first && port <= last) return true;
                }
                else unknown = true;
            }
            return unknown ? null : false;
        }
    }
}
