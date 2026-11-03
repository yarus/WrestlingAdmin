using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Wrestling.Providers.Network
{
    // Enumerates IPv4 addresses on the local machine that look like the
    // tournament LAN — UP interfaces, not loopback, not APIPA. Automatic
    // discovery announces from each interface with its own HTTP address.
    public static class LocalIpAddressProbe
    {
        public sealed record LanAddress(IPAddress Address, IPAddress BroadcastAddress, string NetworkName);

        public static IReadOnlyList<LanAddress> EnumerateLanInterfaces()
        {
            var result = new List<LanAddress>();
            NetworkInterface[] interfaces;
            try { interfaces = NetworkInterface.GetAllNetworkInterfaces(); }
            catch (NetworkInformationException) { return result; }
            foreach (var nic in interfaces)
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                IPInterfaceProperties properties;
                try { properties = nic.GetIPProperties(); }
                catch (NetworkInformationException) { continue; }
                foreach (var unicast in properties.UnicastAddresses)
                {
                    var address = unicast.Address;
                    if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address)) continue;
                    var bytes = address.GetAddressBytes();
                    if (bytes[0] == 169 && bytes[1] == 254 || address.Equals(IPAddress.Any)) continue;
                    var mask = unicast.IPv4Mask?.GetAddressBytes();
                    if (mask == null || mask.Length != 4) continue;
                    var broadcast = bytes.Select((b, i) => (byte)(b | (~mask[i] & 0xff))).ToArray();
                    result.Add(new LanAddress(address, new IPAddress(broadcast), nic.Name));
                }
            }
            return result.DistinctBy(x => x.Address).ToList();
        }

        public static IList<IPAddress> EnumerateLanAddresses()
        {
            var addresses = new List<IPAddress>();

            NetworkInterface[] nics;
            try
            {
                nics = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch
            {
                return addresses;
            }

            foreach (var nic in nics)
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                IPInterfaceProperties props;
                try { props = nic.GetIPProperties(); }
                catch { continue; }

                foreach (var unicast in props.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var bytes = unicast.Address.GetAddressBytes();
                    if (bytes.Length != 4) continue;
                    // Drop APIPA (169.254/16) — it means "no DHCP", won't help.
                    if (bytes[0] == 169 && bytes[1] == 254) continue;
                    addresses.Add(unicast.Address);
                }
            }

            // Private-range addresses are most likely to be the tournament LAN,
            // so surface them first. Public IPs on corporate machines or VPNs
            // are kept in the tail of the list for operator override.
            addresses.Sort((a, b) => Priority(a).CompareTo(Priority(b)));
            return addresses;
        }

        public static IPAddress PickDefault()
        {
            var list = EnumerateLanAddresses();
            return list.Count > 0 ? list[0] : IPAddress.Loopback;
        }

        // Legacy helper for callers that need one address. Discovery itself
        // uses EnumerateLanInterfaces and does not persist or consult overrides.
        public static IPAddress PickAnnounceAddress(string overrideValue)
        {
            if (!string.IsNullOrWhiteSpace(overrideValue) &&
                IPAddress.TryParse(overrideValue.Trim(), out var parsed) &&
                !IPAddress.IsLoopback(parsed))
            {
                foreach (var ip in EnumerateLanAddresses())
                {
                    if (ip.Equals(parsed)) return parsed;
                }
            }
            return PickDefault();
        }

        private static int Priority(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();
            if (bytes[0] == 192 && bytes[1] == 168) return 0;
            if (bytes[0] == 10) return 1;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return 2;
            return 10;
        }
    }
}
