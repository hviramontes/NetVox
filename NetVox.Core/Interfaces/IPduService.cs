// File: NetVox.Core/Interfaces/IPduService.cs
using System;
using System.Threading.Tasks;
using NetVox.Core.Models;

namespace NetVox.Core.Interfaces
{
    /// <summary>
    /// Builds and sends DIS PDUs (Signal, Transmitter, Receiver, etc.).
    /// Also owns the TX hold buffer used to frame audio into Signal PDUs.
    /// </summary>
    public interface IPduService
    {
        /// <summary>Mutable DIS/PDU settings (exercise ID, codec, sample rate, IDs, etc.).</summary>
        PduSettings Settings { get; set; }

        /// <summary>Raised for diagnostic logging.</summary>
        event Action<string>? LogEvent;

        /// <summary>
        /// Append an audio chunk to the TX hold buffer and emit one or more Signal PDUs as needed.
        /// The chunk format must match <see cref="PduSettings.Codec"/> and <see cref="PduSettings.SampleRate"/>.
        /// </summary>
        Task SendSignalPduAsync(byte[] src, int offset, int count);

        /// <summary>
        /// Convenience overload for whole-buffer sends (equivalent to offset 0, count src.Length).
        /// </summary>
        Task SendSignalPduAsync(byte[] src);

        /// <summary>
        /// Flush any remaining bytes in the TX hold buffer into a final Signal PDU (if any),
        /// then clear the buffer. Call this when PTT is released.
        /// </summary>
        void FlushSignalHold();

        /// <summary>Convenience logger that raises <see cref="LogEvent"/>.</summary>
        void Log(string message);

        /// <summary>Send a Transmitter (Type 25) PDU reflecting current state.</summary>
        Task SendTransmitterPduAsync(bool isOn);

        /// <summary>Send a Receiver (Type 27) PDU (minimal/stub ok).</summary>
        Task SendReceiverPduAsync(bool isOn);
    }
}
