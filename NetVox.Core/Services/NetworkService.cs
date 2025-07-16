using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;

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
    }
}
