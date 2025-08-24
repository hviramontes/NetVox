using NetVox.Core.Interfaces;
using NetVox.Core.Models;
using System;
using System.Threading.Tasks;

namespace NetVox.Core.Services
{
    public class PduService : IPduService
    {
        private readonly INetworkService _network;
        private PduSettings _settings;

        public event Action<string> LogEvent;

        public PduSettings Settings
        {
            get => _settings;
            set => _settings = value;
        }

        public PduService(INetworkService networkService)
        {
            _network = networkService;
        }

        public async Task SendSignalPduAsync(byte[] audioData)
        {
            if (_network == null || _settings == null)
            {
                LogEvent?.Invoke("[PDU] Network or settings not initialized.");
                return;
            }

            var config = _network.CurrentConfig;
            var disVersion = _settings.Version;
            var codec = _settings.Codec;
            var frequency = (uint)config.FrequencyHz;

            byte[] modulation = DisModulationBuilder.Build(
                ModulationScheme.AmplitudeAM, // default scheme
                codec,
                frequency
            );

            // Construct minimal Signal PDU buffer
            int headerSize = 12;
            int signalPduHeaderSize = 32;
            int modulationSize = modulation.Length;
            int totalSize = headerSize + signalPduHeaderSize + modulationSize + audioData.Length;

            byte[] buffer = new byte[totalSize];

            // DIS Header
            buffer[0] = 6; // PDU Type: Signal
            buffer[1] = (byte)disVersion;
            buffer[2] = 0; // Protocol Family (0 = unspecified)
            buffer[3] = 0; // Timestamp etc.
            BitConverter.GetBytes((ushort)totalSize).CopyTo(buffer, 4);

            // Signal PDU fields (mocked)
            BitConverter.GetBytes((ushort)1).CopyTo(buffer, 12);  // Encoding scheme
            BitConverter.GetBytes((ushort)codec).CopyTo(buffer, 14);  // Codec
            BitConverter.GetBytes(frequency).CopyTo(buffer, 16);  // Frequency (uint)
            BitConverter.GetBytes((ushort)1).CopyTo(buffer, 20);  // Sample rate
            BitConverter.GetBytes((ushort)1).CopyTo(buffer, 22);  // Samples
            BitConverter.GetBytes((ushort)audioData.Length).CopyTo(buffer, 24);  // Data length

            // Copy modulation bytes into position
            Buffer.BlockCopy(modulation, 0, buffer, 32, modulation.Length);

            // Copy audio data to end
            Buffer.BlockCopy(audioData, 0, buffer, 32 + modulation.Length, audioData.Length);

            // Placeholder for SendAsync (you must implement this method in your NetworkService)
            await _network.SendBytesAsync(buffer);

            LogEvent?.Invoke($"[PDU] Signal PDU sent ({audioData.Length} bytes audio, {modulation.Length} bytes modulation)");
        }

        private byte[] BuildPdu(byte[] audioData)
        {
            // Placeholder modulation encoding - will be replaced by actual DIS-compliant builder in Milestone 10
            byte[] header = new byte[] { 0x01, 0x02, 0x03, 0x04 }; // dummy header
            byte[] pdu = new byte[header.Length + audioData.Length];
            Buffer.BlockCopy(header, 0, pdu, 0, header.Length);
            Buffer.BlockCopy(audioData, 0, pdu, header.Length, audioData.Length);
            return pdu;
        }
    }
}
