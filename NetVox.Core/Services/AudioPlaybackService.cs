// NetVox.Core/Services/AudioPlaybackService.cs
using System;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace NetVox.Core.Services
{
    /// <summary>
    /// PCM16 mono playback using a BufferedWaveProvider into a WASAPI output device.
    /// Supports selecting the output device by MMDevice.FriendlyName.
    /// Adds a controllable jitter buffer for smoothing RX.
    /// </summary>
    public sealed class AudioPlaybackService : IDisposable, NetVox.Core.Interfaces.IAudioPlaybackService
    {
        private IWavePlayer? _out;                       // Output (WasapiOut preferred; WaveOutEvent fallback)
        private MMDevice? _device;                       // Chosen output device (null = default)
        private BufferedWaveProvider? _buffer;           // PCM16 LE mono buffer
        private int _sampleRate;
        private string? _pendingDeviceFriendlyName;      // stored until init

        // Jitter control (in milliseconds)
        // _jitterMs is the target queued-audio we try to hover around; we drop backlog beyond a margin.
        private int _jitterMs = 120;                     // default if caller never sets it
        private const int MinJitterMs = 40;
        private const int MaxJitterMs = 1000;

        /// <summary>
        /// Selects the output device by FriendlyName. Pass null/empty to use the default device.
        /// Call before audio starts (or we’ll re-init on next EnsureFormat).
        /// </summary>
        public void SetOutputDeviceByName(string? friendlyName)
        {
            _pendingDeviceFriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? null : friendlyName;
            // If we’re already initialized, re-init on next EnsureFormat with the same sample rate
            if (_buffer != null)
            {
                var sr = _sampleRate > 0 ? _sampleRate : 8000;
                Reinit(sr);
            }
        }

        /// <summary>
        /// Configure desired jitter buffer size in milliseconds (clamped 40..1000).
        /// This influences the BufferedWaveProvider.BufferDuration and backlog trimming thresholds.
        /// </summary>
        public void SetJitterMs(int ms)
        {
            if (ms < MinJitterMs) ms = MinJitterMs;
            if (ms > MaxJitterMs) ms = MaxJitterMs;
            _jitterMs = ms;

            // If already initialized, apply immediately.
            if (_buffer != null)
            {
                _buffer.BufferDuration = ComputeBufferDuration(_jitterMs);
                // Optionally trim excess if we’re far above new target
                TryTrimBacklog();
            }
        }

        /// <summary>
        /// Ensure playback is initialized for the given sample rate (Hz).
        /// </summary>
        public void EnsureFormat(int sampleRate)
        {
            if (sampleRate <= 0) sampleRate = 8000;
            if (_out != null && _sampleRate == sampleRate) return;
            Reinit(sampleRate);
        }

        private void Reinit(int sampleRate)
        {
            Stop(); // disposes old _out and _buffer
            _sampleRate = sampleRate;

            // Resolve target device (may be null = default)
            _device = ResolveDevice(_pendingDeviceFriendlyName);

            var format = WaveFormat.CreateCustomFormat(
                WaveFormatEncoding.Pcm, _sampleRate, /*channels*/ 1,
                _sampleRate * 2, /*blockAlign*/ 2, /*bits*/ 16);

            _buffer = new BufferedWaveProvider(format)
            {
                DiscardOnBufferOverflow = true,
                ReadFully = false,
                BufferDuration = ComputeBufferDuration(_jitterMs)
            };

            // Try chain: WasapiOut(on device) → WasapiOut(default) → WaveOutEvent
            _out = TryCreateWasapiOut(_device) ??
                   TryCreateWasapiOut(null) ??
                   TryCreateWaveOutEvent();

            _out.Init(_buffer);
            _out.Play();
        }

        private static TimeSpan ComputeBufferDuration(int jitterMs)
        {
            // Keep some headroom above desired jitter to prevent constant underflow.
            // e.g., jitter=120ms → buffer ≈ 240ms (capped).
            int dur = jitterMs * 2;
            if (dur < 80) dur = 80;
            if (dur > 1000) dur = 1000;
            return TimeSpan.FromMilliseconds(dur);
        }

        private static IWavePlayer? TryCreateWasapiOut(MMDevice? device)
        {
            try
            {
                // Event-driven, low-latency
                if (device != null)
                    return new WasapiOut(device, AudioClientShareMode.Shared, true, 60);
                return new WasapiOut(AudioClientShareMode.Shared, true, 60);
            }
            catch
            {
                return null;
            }
        }

        private static IWavePlayer? TryCreateWaveOutEvent()
        {
            try
            {
                return new WaveOutEvent { DesiredLatency = 60 };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Queue PCM16 LE mono audio to the output buffer.
        /// </summary>
        public void EnqueuePcm16(byte[] buffer, int offset, int count)
        {
            if (buffer == null || count <= 0) return;
            if (_buffer == null) EnsureFormat(_sampleRate > 0 ? _sampleRate : 8000);

            var avgBps = _buffer!.WaveFormat.AverageBytesPerSecond;
            if (avgBps > 0)
            {
                // Current queued audio in ms
                var bufferedMs = (int)(_buffer.BufferedBytes * 1000L / avgBps);

                // Trim policy:
                // - If we exceed jitter target by a margin, drop the backlog (keep most recent)
                // - Also trim single giant writes to ~75% of jitter to avoid ballooning queue
                int dropThreshold = _jitterMs + 100; // margin above desired jitter
                if (bufferedMs > dropThreshold)
                {
                    _buffer.ClearBuffer();
                }

                int maxWriteMs = (int)(_jitterMs * 0.75);
                if (maxWriteMs < 20) maxWriteMs = 20;
                int maxBytesPerWrite = (int)((long)avgBps * maxWriteMs / 1000L);
                if (maxBytesPerWrite > 0 && count > maxBytesPerWrite)
                {
                    // Keep the latest tail; older audio would only add delay.
                    int skip = count - maxBytesPerWrite;
                    offset += skip;
                    count = maxBytesPerWrite;
                }
            }

            _buffer!.AddSamples(buffer, offset, count);
        }

        public void Start() => _out?.Play();

        public void Stop()
        {
            try { _out?.Stop(); } catch { /* driver quirks happen */ }
            try { _out?.Dispose(); } catch { }
            _out = null;
            _buffer = null;

            try { _device?.Dispose(); } catch { }
            _device = null;
        }

        public void Dispose() => Stop();

        private void TryTrimBacklog()
        {
            if (_buffer == null) return;
            var avgBps = _buffer.WaveFormat.AverageBytesPerSecond;
            if (avgBps <= 0) return;

            int bufferedMs = (int)(_buffer.BufferedBytes * 1000L / avgBps);
            int dropThreshold = _jitterMs + 100;
            if (bufferedMs > dropThreshold)
            {
                _buffer.ClearBuffer();
            }
        }

        private static MMDevice? ResolveDevice(string? friendlyName)
        {
            try
            {
                using var mm = new MMDeviceEnumerator();

                if (!string.IsNullOrWhiteSpace(friendlyName))
                {
                    // Try exact FriendlyName match first
                    foreach (var d in mm.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                    {
                        if (string.Equals(d.FriendlyName, friendlyName, StringComparison.OrdinalIgnoreCase))
                            return d;
                        d.Dispose();
                    }
                }

                // Fallback to default multimedia render device
                var def = mm.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return def;
            }
            catch
            {
                // Nothing we can do; return null and let WasapiOut(no device) use system default selection.
                return null;
            }
        }
    }
}
