using System.Threading.Tasks;
using NetVox.Core.Models;

namespace NetVox.Core.Interfaces
{
    /// <summary>
    /// Handles building and sending DIS Signal PDUs based on audio data.
    /// </summary>
    public interface IPduService
    {
        /// <summary>
        /// Current PDU settings (version, types to send).
        /// </summary>
        PduSettings Settings { get; set; }

        /// <summary>
        /// Sends a block of audio as a DIS Signal PDU.
        /// </summary>
        Task SendSignalPduAsync(byte[] audioData);
    }
}
