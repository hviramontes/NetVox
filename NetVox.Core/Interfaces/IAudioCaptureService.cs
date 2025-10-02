// NetVox.Core/Interfaces/IAudioCaptureService.cs
using System;

namespace NetVox.Core.Interfaces
{
    /// <summary>Event args carrying captured PCM audio.</summary>
    public sealed class AudioDataEventArgs : EventArgs
    {
        public AudioDataEventArgs(byte[] buffer, int offset, int count)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Offset = offset;
            Count = count;
        }
        public byte[] Buffer { get; }
        public int Offset { get; }
        public int Count { get; }
    }

    /// <summary>Abstraction for mic capture.</summary>
    public interface IAudioCaptureService : IDisposable
    {
        /// <summary>Raised when new audio bytes are available (for event-driven capture).</summary>
        event EventHandler<AudioDataEventArgs>? DataAvailable;

        /// <summary>True if the implementation prefers pull via TryRead.</summary>
        bool SupportsPull { get; }

        /// <summary>Begin capturing PCM audio (mono required for DIS here).</summary>
        void Start(int sampleRate, int bitsPerSample, int channels);

        /// <summary>Stop capturing.</summary>
        void Stop();

        /// <summary>Pull bytes from internal ring. Returns bytes copied (0 if none).</summary>
        int TryRead(byte[] buffer, int offset, int count);
    }
}
