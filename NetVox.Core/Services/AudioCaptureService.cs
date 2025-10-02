// File: NetVox.Core/Services/AudioCaptureService.cs
using System;
using NAudio.Wave;

namespace NetVox.Core.Services
{
    /// <summary>
    /// Captures microphone audio as 16-bit PCM mono and raises raw buffers.
    /// </summary>
    public sealed class AudioCaptureService : IDisposable
    {
        private WaveInEvent? _waveIn;
        private int _sampleRate;
        private bool _isStarted;

        /// <summary>
        /// Fired whenever a buffer of audio has been captured.
        /// Args: (buffer, bytesRecorded).
        /// </summary>
        public event Action<byte[], int>? BytesCaptured;

        /// <summary>Current sample rate in Hz (valid after Start).</summary>
        public int CurrentSampleRate => _sampleRate;

        /// <summary>Start capturing microphone at the given sample rate (Hz).</summary>
        public void Start(int sampleRate)
        {
            if (_isStarted)
            {
                if (_sampleRate == sampleRate) return;
                Stop();
            }

            _sampleRate = sampleRate <= 0 ? 44100 : sampleRate;

            _waveIn = new WaveInEvent
            {
                // 16-bit PCM mono
                WaveFormat = new WaveFormat(_sampleRate, 16, 1),

                // Smaller buffers => lower latency, more callbacks
                // Typical values: 10–40 ms
                BufferMilliseconds = 20,
                NumberOfBuffers = 4
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            _waveIn.StartRecording();
            _isStarted = true;
        }

        /// <summary>Stop capturing.</summary>
        public void Stop()
        {
            if (!_isStarted) return;

            try
            {
                if (_waveIn != null)
                {
                    _waveIn.DataAvailable -= OnDataAvailable;
                    _waveIn.RecordingStopped -= OnRecordingStopped;

                    _waveIn.StopRecording();
                    _waveIn.Dispose();
                    _waveIn = null;
                }
            }
            finally
            {
                _isStarted = false;
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded <= 0) return;
            // Raise a copy so downstream code can hold onto it safely
            var buf = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, buf, 0, e.BytesRecorded);
            BytesCaptured?.Invoke(buf, e.BytesRecorded);
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            // NAudio sometimes calls RecordingStopped on a worker thread
            // Ensure we’re fully torn down
            Stop();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
