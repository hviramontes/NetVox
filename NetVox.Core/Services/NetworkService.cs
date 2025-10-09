using NetVox.Core.Interfaces;
using NetVox.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetVox.Core.Services
{
    public class NetworkService : INetworkService
    {
        public NetworkConfig CurrentConfig { get; set; } = new();

        public async Task<IEnumerable<string>> GetAvailableLocalIPsAsync()
        {
            // Enumerate IPv4 addresses on up/up interfaces
            return await Task.Run(() =>
                NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ua => ua.Address.ToString())
                    .Distinct()
                    .ToList()
            );
        }

        // Preferred name used by PduService
        public Task SendAsync(byte[] data) => SendBytesAsync(data);

        // Legacy name kept for compatibility
        public async Task SendBytesAsync(byte[] data)
        {
            var cfg = CurrentConfig ?? throw new InvalidOperationException("Network configuration is missing.");

            // Prefer DestinationIPAddress (UI), but keep alias in sync
            var dest = string.IsNullOrWhiteSpace(cfg.DestinationIPAddress) ? cfg.DestinationIP : cfg.DestinationIPAddress;
            if (string.IsNullOrWhiteSpace(dest) || cfg.DestinationPort == 0)
                throw new InvalidOperationException("Network destination is not configured.");

            // Bind to the chosen local interface if provided
            UdpClient udpClient;
            if (!string.IsNullOrWhiteSpace(cfg.LocalIPAddress) && IPAddress.TryParse(cfg.LocalIPAddress, out var localIp))
            {
                udpClient = new UdpClient(new IPEndPoint(localIp, 0));
            }
            else
            {
                udpClient = new UdpClient(AddressFamily.InterNetwork);
            }

            using (udpClient)
            {
                var sock = udpClient.Client;

                // Detect mode from destination IP
                var mode = DetectMode(dest);
                cfg.Mode = mode; // status/telemetry; UI no longer sets it

                // TTL (apply both unicast and multicast)
                var ttl = Clamp(cfg.TimeToLive, 0, 255);
                try
                {
                    sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, ttl);
                    sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, ttl);
                }
                catch { /* some stacks may not support both; ignore */ }

                // Traffic Class / DSCP (IPv4 TOS). We’ll set it; if unsupported, ignore.
                var tclass = Clamp(cfg.MulticastTrafficClass, 0, 255);
                try
                {
                    sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.TypeOfService, tclass);
                }
                catch { /* not fatal */ }

                // Broadcast allow if target is broadcast-ish
                udpClient.EnableBroadcast = IsBroadcast(dest);

                // Send
                await udpClient.SendAsync(data, data.Length, dest, cfg.DestinationPort);
            }
        }

        // ---------- helpers ----------

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        private static NetworkMode DetectMode(string destination)
        {
            if (IPAddress.TryParse(destination, out var ip))
            {
                var b = ip.GetAddressBytes();
                // Multicast: 224.0.0.0 – 239.255.255.255
                if (b[0] >= 224 && b[0] <= 239) return NetworkMode.Multicast;

                // Limited broadcast 255.255.255.255 or common x.x.x.255
                if (ip.Equals(IPAddress.Broadcast)) return NetworkMode.Broadcast;
                if (b[3] == 255) return NetworkMode.Broadcast;

                return NetworkMode.Unicast;
            }
            // If it's a hostname, assume unicast; DNS resolution could be added later.
            return NetworkMode.Unicast;
        }

        private static bool IsBroadcast(string destination)
        {
            if (!IPAddress.TryParse(destination, out var ip)) return false;
            if (ip.Equals(IPAddress.Broadcast)) return true;

            // Heuristic: x.x.x.255 looks like subnet broadcast in many /24s.
            var b = ip.GetAddressBytes();
            return b.Length == 4 && b[3] == 255;
        }
    }
}
