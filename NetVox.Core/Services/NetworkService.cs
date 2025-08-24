using NetVox.Core.Interfaces;
using NetVox.Core.Models;
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
            // Quickly enumerate IPv4 addresses on up/up interfaces
            return await Task.Run(() =>
                NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ip => ip.Address.ToString())
                    .Distinct()
                    .ToList()
            );
        }
        public async Task SendBytesAsync(byte[] data)
        {
            using var udpClient = new System.Net.Sockets.UdpClient();
            var config = CurrentConfig;

            if (string.IsNullOrWhiteSpace(config.DestinationIP) || config.DestinationPort == 0)
                throw new InvalidOperationException("Network destination is not configured.");

            await udpClient.SendAsync(data, data.Length, config.DestinationIP, config.DestinationPort);
        }


    }
}
