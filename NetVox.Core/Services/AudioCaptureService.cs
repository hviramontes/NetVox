using System;
using System.Linq;
using NAudio.Wave;

namespace NetVox.Core.Services
{
    /// <summary>
    /// Captures audio from the default microphone in 16-bit PCM.
    /// Raises AudioAvailable events with raw PCM buffers.
    /// </summary>
    public class AudioCaptureService
    {
        private readonly WaveInEvent _waveIn;

        /// <summary>
        /// Fired whenever a new buffer of audio is captured.
        /// </summary>
        public event EventHandler<byte[]> AudioAvailable;

        public AudioCaptureService(int deviceNumber = 0, int sampleRate = 8000, int channels = 1)
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                WaveFormat = new WaveFormat(sampleRate, 16, channels)
            };
            _waveIn.DataAvailable += OnDataAvailable;
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            // Copy only the recorded bytes and raise the event
            var buffer = e.Buffer.Take(e.BytesRecorded).ToArray();
            AudioAvailable?.Invoke(this, buffer);
        }

        /// <summary>Start capturing audio.</summary>
        public void Start() => _waveIn.StartRecording();

        /// <summary>Stop capturing audio.</summary>
        public void Stop() => _waveIn.StopRecording();
    }
}
