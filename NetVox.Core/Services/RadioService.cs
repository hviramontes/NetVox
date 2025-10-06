// File: NetVox.Core/Services/RadioService.cs
using System;
using System.Threading.Tasks;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;

namespace NetVox.Core.Services
{
    /// <summary>
    /// Drives PTT lifecycle and forwards captured audio to the PDU layer.
    /// Responsibilities:
    ///  - Maintain current channel
    ///  - Start/Stop TX (PTT)
    ///  - Forward audio chunks to IPduService
    ///  - Fire TransmitStarted/Stopped events
    /// </summary>
    public sealed class RadioService : IRadioService
    {
        private readonly IPduService _pdu;
        private readonly AudioCaptureService _capture; // mic capture: 16-bit PCM, little-endian
        private readonly object _gate = new();

        private volatile bool _isTransmitting;
        private int _currentChannelNumber;

        public event EventHandler? TransmitStarted;
        public event EventHandler? TransmitStopped;

        public RadioService(AudioCaptureService capture, IPduService pdu)
        {
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            _pdu = pdu ?? throw new ArgumentNullException(nameof(pdu));

            // mic callback
            _capture.BytesCaptured += OnBytesCaptured;
        }

        /// <summary>Expose PDU settings so UI/app can read/write through the radio.</summary>
        public PduSettings Settings
        {
            get => _pdu.Settings;
            set => _pdu.Settings = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Set the logical channel (map to radio id/frequency elsewhere as needed).</summary>
        public void SetChannel(int channelNumber)
        {
            lock (_gate)
            {
                _currentChannelNumber = channelNumber;
                // Example mapping (if desired):
                // _pdu.Settings.RadioId = (ushort)channelNumber;
            }
        }

        /// <summary>Sync wrapper for UI: begin PTT.</summary>
        public void BeginTransmit() => _ = StartTransmitAsync();

        /// <summary>Sync wrapper for UI: end PTT.</summary>
        public void EndTransmit() => _ = StopTransmitAsync();

        /// <summary>Start transmitting (PTT ON).</summary>
        public async Task StartTransmitAsync()
        {
            lock (_gate)
            {
                if (_isTransmitting) return;
                _isTransmitting = true;
            }

            try
            {
                // Start mic at configured SR (mic always 16-bit PCM mono)
                _capture.Start(_pdu.Settings.SampleRate);

                // DIS Transmitter PDU: ON
                await _pdu.SendTransmitterPduAsync(true).ConfigureAwait(false);

                _pdu.Log($"[PTT] Transmit Started (ch={_currentChannelNumber}, sr={_pdu.Settings.SampleRate}, codec={_pdu.Settings.Codec})");
                TransmitStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _pdu.Log($"[PTT] Start failed: {ex.Message}");
                lock (_gate) { _isTransmitting = false; }
                try { _capture.Stop(); } catch { /* ignore */ }
                throw;
            }
        }

        /// <summary>Stop transmitting (PTT OFF).</summary>
        public async Task StopTransmitAsync()
        {
            bool wasTx;
            lock (_gate)
            {
                wasTx = _isTransmitting;
                _isTransmitting = false;
            }
            if (!wasTx) return;

            try
            {
                // Stop mic so no further buffers come in
                _capture.Stop();

                // Flush any partial audio the PDU framer was holding
                _pdu.FlushSignalHold();

                // DIS Transmitter PDU: OFF
                await _pdu.SendTransmitterPduAsync(false).ConfigureAwait(false);

                _pdu.Log("[PTT] Transmit Stopped");
                TransmitStopped?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _pdu.Log($"[PTT] Stop failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Mic callback: we always receive 16-bit PCM (little-endian) from NAudio.
        /// Forward the raw 16-bit source to PduService. PduService performs the correct
        /// on-wire conversion based on Settings.Codec (keeps PCM16 path untouched).
        /// </summary>
        private void OnBytesCaptured(byte[] buffer, int count)
        {
            if (buffer == null || count <= 0) return;
            if (!_isTransmitting) return;

            try
            {
                // Always forward source 16-bit PCM; PduService handles conversion (PCM16/PCM8).
                _ = _pdu.SendSignalPduAsync(buffer, 0, count);
            }
            catch (Exception ex)
            {
                _pdu.Log($"[TX] OnBytesCaptured error: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert 16-bit PCM (little-endian, mono) to 8-bit unsigned PCM.
        /// Mapping: -32768..32767  ->  0..255
        /// Fast path: (sample >> 8) + 128.
        /// NOTE: Left in place for future use; not used in the current TX path.
        /// </summary>
        private static byte[] ConvertPcm16LeToPcm8Unsigned(byte[] src, int count)
        {
            int samples16 = (count >> 1);
            var dst = new byte[samples16];

            int si = 0;
            for (int di = 0; di < samples16; di++)
            {
                short s = (short)(src[si] | (src[si + 1] << 8));
                si += 2;

                int u = (s >> 8) + 128;
                if ((uint)u > 255u) u = (u < 0) ? 0 : 255;
                dst[di] = (byte)u;
            }

            return dst;
        }

        // ===== Interface persistence hooks (void signatures per IRadioService) =====

        public void SaveProfile(string fileName)
        {
            _pdu.Log($"[Radio] SaveProfile('{fileName}') (no-op in RadioService)");
        }

        public void LoadProfile(string fileName)
        {
            _pdu.Log($"[Radio] LoadProfile('{fileName}') (no-op in RadioService)");
        }
    }
}
