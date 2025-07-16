using System;
using System.Threading.Tasks;

namespace NetVox.Core.Interfaces
{
    public interface IRadioService
    {
        /// <summary>Load a saved profile (channels, network, etc.) from disk.</summary>
        void LoadProfile(string filePath);

        /// <summary>Save the current profile back to disk.</summary>
        void SaveProfile(string filePath);

        /// <summary>Select the active radio channel (1–15).</summary>
        void SetChannel(int channelNumber);

        /// <summary>Begin transmitting audio as DIS PDUs.</summary>
        Task StartTransmitAsync();

        /// <summary>Stop transmitting audio.</summary>
        Task StopTransmitAsync();

        /// <summary>Fires when transmission actually starts.</summary>
        event EventHandler TransmitStarted;

        /// <summary>Fires when transmission actually stops.</summary>
        event EventHandler TransmitStopped;
    }
}
