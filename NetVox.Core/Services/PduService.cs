using System;
using System.Threading.Tasks;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;

namespace NetVox.Core.Services
{
    /// <summary>
    /// Stub implementation of IPduService. Logs to console instead of real DIS.
    /// </summary>
    public class PduService : IPduService
    {
        public PduSettings Settings { get; set; } = new PduSettings();

        public Task SendSignalPduAsync(byte[] audioData)
        {
            // Stub: just write info to debug/console
            Console.WriteLine($"[PDU] Sending {(audioData?.Length ?? 0)} bytes with version {Settings.Version}, types25={Settings.SendType25}, types26={Settings.SendType26}");
            return Task.CompletedTask;
        }
    }
}
