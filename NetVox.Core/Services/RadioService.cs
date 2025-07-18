using System;
using System.Threading.Tasks;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;

namespace NetVox.Core.Services
{
    /// <summary>
    /// Captures audio and sends it as DIS Signal PDUs.
    /// </summary>
    public class RadioService : IRadioService
    {
        private readonly AudioCaptureService _capture;
        private readonly IPduService _pduService;

        public event EventHandler TransmitStarted;
        public event EventHandler TransmitStopped;

        public RadioService(AudioCaptureService captureService, IPduService pduService)
        {
            _capture = captureService;
            _pduService = pduService;

            // Every time audio is available, send it as a PDU
            _capture.AudioAvailable += async (_, buffer) =>
            {
                await _pduService.SendSignalPduAsync(buffer);
            };
        }

        public void LoadProfile(string filePath)
        {
            // No-op or implement if you like
        }

        public void SaveProfile(string filePath)
        {
            // No-op or implement if you like
        }

        public void SetChannel(int channelNumber)
        {
            // No-op for now
        }

        public Task StartTransmitAsync()
        {
            _capture.Start();
            TransmitStarted?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task StopTransmitAsync()
        {
            _capture.Stop();
            TransmitStopped?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}
