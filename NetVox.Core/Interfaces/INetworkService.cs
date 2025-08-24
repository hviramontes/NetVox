using System.Collections.Generic;
using System.Threading.Tasks;
using NetVox.Core.Models;

namespace NetVox.Core.Interfaces
{
    public interface INetworkService
    {
        /// <summary>
        /// Enumerate available IPv4 addresses on active NICs.
        /// </summary>
        Task<IEnumerable<string>> GetAvailableLocalIPsAsync();

        /// <summary>
        /// Get or set the current network configuration.
        /// </summary>
        NetworkConfig CurrentConfig { get; set; }

        /// <summary>
        /// Sends raw PDU bytes over UDP.
        /// </summary>
        Task SendBytesAsync(byte[] data);
    }
}
