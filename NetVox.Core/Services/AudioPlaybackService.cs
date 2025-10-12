// File: NetVox.Core/Services/AudioPlaybackService.cs
using System;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace NetVox.Core.Services
{
    /// <summary>
    /// PCM16 mono playback using a BufferedWaveProvider into a WASAPI output device.
    /// Supports selecting the output device by MMDevice.FriendlyName, with WaveOutEvent fallback.
    /// Tuned for low-latency comms.
    /// </summary>
    public sealed class AudioPlaybackService : IDisposable, NetVox.Core.Interfaces.IAudioPlaybackService
    {
        private IWavePlayer? _out;                       // Output (WasapiOut preferred; WaveOutEvent fallback)
        private MMDevice? _device;                       // Chosen output device (null = default)
        private BufferedWaveProvider? _buffer;           // PCM16 LE mono buffer
        private int _sampleRate;
        private string? _pendingDeviceFriendlyName;      // stored until init

        /// <summary>Raised when the output device cannot initialize or playback fails.</summary>
        public event Action<string>? ErrorOccurred;

        /// <summary>
        /// Select the output device by FriendlyName. Pass null/empty to use the default device.
        /// Call before audio starts (or we’ll re-init on next EnsureFormat).
        /// </summary>
        public void SetOutputDeviceByName(string? friendlyName)
        {
            _pendingDeviceFriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? null : friendlyName;
            // If we’re already initialized, re-init with the current sample rate
            if (_buffer != null)
            {
                var sr = _sampleRate > 0 ? _sampleRate : 8000;
                Reinit(sr);
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
                BufferDuration = TimeSpan.FromMilliseconds(150),
                ReadFully = false
            };

            // Try chain: WasapiOut(on device) → WasapiOut(default) → WaveOutEvent
            _out = TryCreateWasapiOut(_device) ??
                   TryCreateWasapiOut(null) ??
                   TryCreateWaveOutEvent();

            if (_out == null)
            {
                ErrorOccurred?.Invoke("Speaker not available or driver failed to initialize.");
                return; // no viable output path; fail inert, keep UI alive
            }

            try
            {
                _out.Init(_buffer);
                _out.Play();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Speaker start failed: {ex.Message}");
                try { _out.Dispose(); } catch { }
                _out = null;
            }
        }

        private static IWavePlayer? TryCreateWasapiOut(MMDevice? device)
        {
            try
            {
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
            if (_buffer == null)
            {
                ErrorOccurred?.Invoke("Speaker buffer not initialized.");
                return;
            }

            // Drop backlog if queued audio exceeds ~220 ms to keep UI and PTT snappy
            var avgBps = _buffer.WaveFormat.AverageBytesPerSecond;
            if (avgBps > 0)
            {
                var bufferedMs = (int)(_buffer.BufferedBytes * 1000L / avgBps);
                if (bufferedMs > 220)
                    _buffer.ClearBuffer(); // prefer low-latency over glitch-free backlog

                // Prevent single writes larger than ~120 ms from ballooning the queue
                var maxBytesPerWrite = (int)(avgBps * 0.12); // ≈120 ms
                if (maxBytesPerWrite > 0 && count > maxBytesPerWrite)
                {
                    // keep the most recent tail; older audio is useless for comms
                    offset = offset + (count - maxBytesPerWrite);
                    count = maxBytesPerWrite;
                }
            }

            _buffer.AddSamples(buffer, offset, count);
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
                            return d; // ownership transfers; do not dispose here
                        d.Dispose();
                    }
                }

                // Fallback to default multimedia render device
                return mm.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch
            {
                // Nothing we can do; returning null lets WasapiOut(no device) pick system default.
                return null;
            }
        }
    }
}
