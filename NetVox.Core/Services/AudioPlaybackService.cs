// NetVox.Core/Services/AudioPlaybackService.cs
using System;
using NAudio.Wave;

namespace NetVox.Core.Services
{
    /// <summary>Minimal PCM16 mono playback using a buffered provider.</summary>
    public sealed class AudioPlaybackService : IDisposable, NetVox.Core.Interfaces.IAudioPlaybackService
    {
        private WaveOutEvent? _out;
        private BufferedWaveProvider? _buffer;
        private int _sampleRate;

        public void EnsureFormat(int sampleRate)
        {
            if (sampleRate <= 0) sampleRate = 8000;
            if (_out != null && _sampleRate == sampleRate) return;

            Stop();

            _sampleRate = sampleRate;
            var format = WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, _sampleRate, 1, _sampleRate * 2, 2, 16);
            _buffer = new BufferedWaveProvider(format)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromMilliseconds(500)
            };

            _out = new WaveOutEvent
            {
                DesiredLatency = 100
            };
            _out.Init(_buffer);
            _out.Play();
        }

        public void EnqueuePcm16(byte[] buffer, int offset, int count)
        {
            if (buffer == null || count <= 0) return;
            if (_buffer == null) EnsureFormat(8000);
            _buffer!.AddSamples(buffer, offset, count);
        }

        public void Start() => _out?.Play();

        public void Stop()
        {
            try { _out?.Stop(); } catch { }
            _out?.Dispose();
            _out = null;
            _buffer = null;
        }

        public void Dispose() => Stop();
    }
}
