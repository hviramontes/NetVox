// NetVox.Core/Interfaces/IAudioPlaybackService.cs
using System;

namespace NetVox.Core.Interfaces
{
    public interface IAudioPlaybackService : IDisposable
    {
        /// <summary>Ensure the playback output device is created for this sample rate.</summary>
        void EnsureFormat(int sampleRate);

        /// <summary>Enqueue little-endian PCM16 mono samples for playback.</summary>
        void EnqueuePcm16(byte[] buffer, int offset, int count);

        /// <summary>Start playback (no-op if already started).</summary>
        void Start();

        /// <summary>Stop playback and flush buffers.</summary>
        void Stop();
    }
}
