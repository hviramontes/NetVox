using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;

namespace NetVox.Core.Services
{
    /// <summary>
    /// Formats and sends DIS Signal (Type 25) and Data (Type 26) PDUs over UDP,
    /// with support for 16-bit PCM, 8-bit PCM, and μ-law codecs.
    /// </summary>
    public class PduService : IPduService
    {
        private readonly INetworkService _networkService;

        public event Action<string> LogEvent;

        public PduSettings Settings { get; set; } = new PduSettings();

        // μ-law segment end points for encoding
        private static readonly int[] MuLawSegmentEnd = {
            0xFF, 0x1FF, 0x3FF, 0x7FF,
            0xFFF, 0x1FFF, 0x3FFF, 0x7FFF
        };

        public PduService(INetworkService networkService)
        {
            _networkService = networkService;
        }

        public async Task SendSignalPduAsync(byte[] audioData)
        {
            var cfg = _networkService.CurrentConfig;

            // Log start of send
            LogEvent?.Invoke($"Sending PDUs from {cfg.LocalIPAddress} to {cfg.DestinationIPAddress} ({cfg.Mode})");

            try
            {
                // Convert payload according to selected codec
                byte[] payload = audioData;
                switch (Settings.Codec)
                {
                    case CodecType.Pcm8:
                        payload = ConvertPcm16ToPcm8(audioData);
                        break;
                    case CodecType.MuLaw:
                        payload = ConvertPcm16ToMuLaw(audioData);
                        break;
                    case CodecType.Pcm16:
                    default:
                        break;
                }

                // Build the PDUs
                var pdu25 = PduBuilder.BuildSignalPdu(payload, Settings.Version);
                var pdu26 = PduBuilder.BuildDataPdu(payload, Settings.Version);

                using var client = new UdpClient();
                client.Client.Bind(new IPEndPoint(IPAddress.Parse(cfg.LocalIPAddress), 0));

                if (cfg.Mode == NetworkMode.Broadcast)
                    client.EnableBroadcast = true;
                else if (cfg.Mode == NetworkMode.Multicast)
                    client.JoinMulticastGroup(IPAddress.Parse(cfg.DestinationIPAddress));

                // Send both PDUs
                await client.SendAsync(pdu25, pdu25.Length, cfg.DestinationIPAddress, 3000);
                LogEvent?.Invoke($"Sent Signal PDU (Type 25), {pdu25.Length} bytes");

                await client.SendAsync(pdu26, pdu26.Length, cfg.DestinationIPAddress, 3000);
                LogEvent?.Invoke($"Sent Data   PDU (Type 26), {pdu26.Length} bytes");
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke($"Error sending PDUs: {ex.Message}");
            }
        }

        #region Codec conversion helpers

        private static byte[] ConvertPcm16ToPcm8(byte[] pcm16Data)
        {
            int sampleCount = pcm16Data.Length / 2;
            var pcm8 = new byte[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short sample16 = (short)(pcm16Data[2 * i] |
                                         (pcm16Data[2 * i + 1] << 8));
                int unsigned8 = (sample16 >> 8) + 128;
                pcm8[i] = (byte)unsigned8;
            }
            return pcm8;
        }

        private static byte[] ConvertPcm16ToMuLaw(byte[] pcm16Data)
        {
            int sampleCount = pcm16Data.Length / 2;
            var muLaw = new byte[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short sample16 = (short)(pcm16Data[2 * i] |
                                         (pcm16Data[2 * i + 1] << 8));
                muLaw[i] = MuLawEncode(sample16);
            }
            return muLaw;
        }

        private static byte MuLawEncode(short sample)
        {
            const int BIAS = 0x84; // 132

            int sign = (sample >> 8) & 0x80;
            if (sign != 0) sample = (short)-sample;
            if (sample > 0x7FFF) sample = 0x7FFF;
            sample += BIAS;

            int segment = FindMuLawSegment(sample);
            int mantissa = (sample >> (segment + 3)) & 0x0F;
            int muLawByte = ~(sign | (segment << 4) | mantissa);
            return (byte)muLawByte;
        }

        private static int FindMuLawSegment(int sample)
        {
            for (int i = 0; i < MuLawSegmentEnd.Length; i++)
            {
                if (sample <= MuLawSegmentEnd[i])
                    return i;
            }
            return MuLawSegmentEnd.Length - 1;
        }

        #endregion
    }
}
