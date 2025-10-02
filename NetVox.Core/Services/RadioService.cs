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
        private readonly AudioCaptureService _capture; // adjust to your actual capture interface if needed
        private readonly object _gate = new();

        private volatile bool _isTransmitting;
        private int _currentChannelNumber;

        public event EventHandler? TransmitStarted;
        public event EventHandler? TransmitStopped;

        public RadioService(AudioCaptureService capture, IPduService pdu)
        {
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            _pdu = pdu ?? throw new ArgumentNullException(nameof(pdu));

            // Hook capture callback (adjust the event name/signature if your capture differs)
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
                // Example if you map channel -> radio id:
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
                // Start capture for the configured sample rate
                _capture.Start(_pdu.Settings.SampleRate);

                // DIS Transmitter PDU: ON
                await _pdu.SendTransmitterPduAsync(isOn: true).ConfigureAwait(false);

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
                // Stop capture so no further bytes arrive
                _capture.Stop();

                // Flush any partial audio still buffered for a final Signal PDU
                _pdu.FlushSignalHold();

                // DIS Transmitter PDU: OFF
                await _pdu.SendTransmitterPduAsync(isOn: false).ConfigureAwait(false);

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
        /// Called when the capture layer produces a chunk of audio.
        /// Forwards to the PDU layer if PTT is active.
        /// </summary>
        private void OnBytesCaptured(byte[] buffer, int count)
        {
            if (buffer == null || count <= 0) return;
            if (!_isTransmitting) return;

            // Let PDU layer frame and send Signal PDUs
            _ = _pdu.SendSignalPduAsync(buffer, 0, count);
        }

        // ===== Interface persistence hooks (void signatures per IRadioService) =====

        public void SaveProfile(string fileName)
        {
            // If your app persists via a repository elsewhere, call it from here.
            // This no-op implementation simply logs to avoid breaking builds.
            _pdu.Log($"[Radio] SaveProfile('{fileName}') (no-op in RadioService)");
        }

        public void LoadProfile(string fileName)
        {
            // Likewise, wire up to your repository if needed.
            _pdu.Log($"[Radio] LoadProfile('{fileName}') (no-op in RadioService)");
        }
    }
}
