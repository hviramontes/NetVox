using System;
using System.Threading.Tasks;
using NetVox.Core.Models;

namespace NetVox.Core.Interfaces
{
    public interface IPduService
    {
        /// <summary>Log messages for diagnostics</summary>
        event Action<string> LogEvent;

        PduSettings Settings { get; set; }
        Task SendSignalPduAsync(byte[] audioData);
    }
}
