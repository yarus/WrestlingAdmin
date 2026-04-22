using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Wrestling.Providers.Network
{
    // Enumerates IPv4 addresses on the local machine that look like the
    // tournament LAN — UP interfaces, not loopback, not APIPA. Consumers use
    // the first address to construct the announced http:// URL; settings UI
    // shows the full list for operator override.
    public static class LocalIpAddressProbe
    {
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
