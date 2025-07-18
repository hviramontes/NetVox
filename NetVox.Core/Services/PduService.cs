using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;

namespace NetVox.Core.Services
{
    /// <summary>
    /// Formats and sends DIS Signal (Type 25) and Data (Type 26) PDUs over UDP.
    /// </summary>
    public class PduService : IPduService
    {
        private readonly INetworkService _networkService;

        public PduSettings Settings { get; set; } = new PduSettings();

        public PduService(INetworkService networkService)
        {
            _networkService = networkService;
        }

        public async Task SendSignalPduAsync(byte[] audioData)
        {
            var cfg = _networkService.CurrentConfig;
            // Always send both Type 25 (Signal) and Type 26 (Data)
            var pdu25 = PduBuilder.BuildSignalPdu(audioData, Settings.Version);
            var pdu26 = PduBuilder.BuildDataPdu(audioData, Settings.Version);

            using var client = new UdpClient();
            // Bind to local IP
            client.Client.Bind(new IPEndPoint(IPAddress.Parse(cfg.LocalIPAddress), 0));

            // Configure for broadcast or multicast
            if (cfg.Mode == NetworkMode.Broadcast)
                client.EnableBroadcast = true;
            else if (cfg.Mode == NetworkMode.Multicast)
                client.JoinMulticastGroup(IPAddress.Parse(cfg.DestinationIPAddress));

            // Send both PDUs on port 3000
            await client.SendAsync(pdu25, pdu25.Length, cfg.DestinationIPAddress, 3000);
            await client.SendAsync(pdu26, pdu26.Length, cfg.DestinationIPAddress, 3000);
        }
    }
}
