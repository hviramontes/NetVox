// File: NetVox.Core/Services/AudioCaptureService.cs
using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace NetVox.Core.Services
{
    /// <summary>
    /// Captures microphone audio as 16-bit PCM mono via WASAPI and raises raw buffers.
    /// Allows selecting the input device by MMDevice.FriendlyName.
    /// </summary>
    public sealed class AudioCaptureService : IDisposable
    {
        private IWaveIn? _capture;                       // Capture (WasapiCapture preferred; WaveInEvent fallback)
        private MMDevice? _device;                       // chosen input device (null = default)
        private int _sampleRate = 44100;
        private bool _isStarted;
        private string? _pendingDeviceFriendlyName;      // store desired device until (re)init

        /// <summary>
        /// Fired whenever a buffer of audio has been captured. Args: (buffer, bytesRecorded).
        /// </summary>
        public event Action<byte[], int>? BytesCaptured;

        /// <summary>Raised when the capture device cannot initialize or stops with an error.</summary>
        public event Action<string>? ErrorOccurred;

        /// <summary>Current sample rate in Hz (valid after Start).</summary>
        public int CurrentSampleRate => _sampleRate;

        /// <summary>
        /// Select the input device by FriendlyName. Pass null/empty to use the default device.
        /// If already started, this will reinitialize on the next Start or immediately if running.
        /// </summary>
        public void SetInputDeviceByName(string? friendlyName)
        {
            _pendingDeviceFriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? null : friendlyName;

            // If already running, reinit live with current sample rate
            if (_isStarted)
            {
                var sr = _sampleRate > 0 ? _sampleRate : 44100;
                Stop();
                Start(sr);
            }
        }

        /// <summary>Start capturing microphone at the given sample rate (Hz).</summary>
        public void Start(int sampleRate)
        {
            if (sampleRate <= 0) sampleRate = 44100;

            // If already started with same rate, do nothing
            if (_isStarted && _sampleRate == sampleRate) return;

            Stop(); // clean slate
            _sampleRate = sampleRate;

            // Resolve desired capture device (default if not found)
            _device = ResolveDevice(_pendingDeviceFriendlyName);

            // Try chain: WasapiCapture(on device) → WasapiCapture(default) → WaveInEvent(safe)
            _capture = TryCreateWasapiCapture(_device) ??
                       TryCreateWasapiCapture(null) ??
                       TryCreateWaveInEvent(_sampleRate, safe: true);

            if (_capture == null)
            {
                ErrorOccurred?.Invoke("Microphone not available or driver failed to initialize.");
                _isStarted = false;
                return;
            }

            _capture.WaveFormat = new WaveFormat(_sampleRate, 16, 1);

            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;

            try
            {
                _capture.StartRecording();
                _isStarted = true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Microphone start failed: {ex.Message}");
                try
                {
                    _capture.DataAvailable -= OnDataAvailable;
                    _capture.RecordingStopped -= OnRecordingStopped;
                    _capture.Dispose();
                }
                catch { /* ignored */ }
                _capture = null;
                _isStarted = false;
            }
        }

        private static WasapiCapture? TryCreateWasapiCapture(MMDevice? device)
        {
            try
            {
                return device != null ? new WasapiCapture(device) : new WasapiCapture();
            }
            catch
            {
                return null;
            }
        }

        private static IWaveIn TryCreateWaveInEvent(int sampleRate)
        {
            // Legacy fallback; works on many stubborn drivers
            var w = new WaveInEvent
            {
                WaveFormat = new WaveFormat(sampleRate, 16, 1),
                BufferMilliseconds = 10,
                NumberOfBuffers = 4
            };

            return w;
        }

        private static IWaveIn? TryCreateWaveInEvent(int sampleRate, bool safe)
        {
            try { return TryCreateWaveInEvent(sampleRate); }
            catch { return null; }
        }

        /// <summary>Stop capturing.</summary>
        public void Stop()
        {
            if (!_isStarted && _capture == null) return;

            try
            {
                if (_capture != null)
                {
                    _capture.DataAvailable -= OnDataAvailable;
                    _capture.RecordingStopped -= OnRecordingStopped;
                    try { _capture.StopRecording(); } catch { /* driver quirks happen */ }
                    _capture.Dispose();
                    _capture = null;
                }
            }
            finally
            {
                _isStarted = false;
            }

            try { _device?.Dispose(); } catch { }
            _device = null;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded <= 0) return;

            // Raise a copy so downstream code owns its buffer
            var buf = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, buf, 0, e.BytesRecorded);
            BytesCaptured?.Invoke(buf, e.BytesRecorded);
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            // Bubble up the driver exception if present
            if (e?.Exception != null)
            {
                ErrorOccurred?.Invoke($"Microphone stopped: {e.Exception.Message}");
            }

            // Ensure full teardown
            Stop();
        }

        public void Dispose()
        {
            Stop();
        }

        private static MMDevice? ResolveDevice(string? friendlyName)
        {
            try
            {
                using var mm = new MMDeviceEnumerator();

                if (!string.IsNullOrWhiteSpace(friendlyName))
                {
                    foreach (var d in mm.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                    {
                        if (string.Equals(d.FriendlyName, friendlyName, StringComparison.OrdinalIgnoreCase))
                            return d; // ownership returned to caller; do NOT dispose here
                        d.Dispose();
                    }
                }

                // Fallback to default multimedia capture device
                return mm.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            }
            catch
            {
                // Could not resolve; returning null lets WasapiCapture() pick system default
                return null;
            }
        }
    }
}
