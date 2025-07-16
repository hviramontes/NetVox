using System;
using System.Threading.Tasks;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;

namespace NetVox.Core.Services
{
    /// <summary>
    /// A no-op RadioService that simply raises TransmitStarted/Stopped events.
    /// We’ll plug in the real audio/dis logic later.
    /// </summary>
    public class StubRadioService : IRadioService
    {
        private Profile? _currentProfile;
        private int _currentChannel;

        public event EventHandler? TransmitStarted;
        public event EventHandler? TransmitStopped;

        public void LoadProfile(string filePath)
        {
            // stub: pretend we loaded something
            _currentProfile = new Profile
            {
                Channels = new System.Collections.Generic.List<ChannelConfig>()
            };
        }

        public void SaveProfile(string filePath)
        {
            // stub: no-op
        }

        public void SetChannel(int channelNumber)
        {
            // Validate between 1 and 15
            if (channelNumber < 1 || channelNumber > 15)
                throw new ArgumentOutOfRangeException(nameof(channelNumber));
            _currentChannel = channelNumber;
        }

        public Task StartTransmitAsync()
        {
            // Immediately raise the event
            TransmitStarted?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task StopTransmitAsync()
        {
            TransmitStopped?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}
