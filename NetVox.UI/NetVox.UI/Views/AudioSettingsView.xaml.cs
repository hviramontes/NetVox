// NetVox.UI/Views/AudioSettingsView.xaml.cs
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;
using NAudio.CoreAudioApi;

namespace NetVox.UI.Views
{
    public partial class AudioSettingsView : UserControl
    {
        private readonly IPduService _pduService;

        /// <summary>Raised after Apply with a short status string.</summary>
        public event Action<string>? Applied;

        public AudioSettingsView(IPduService pduService)
        {
            InitializeComponent();
            _pduService = pduService;

            BtnApply.Click += BtnApply_Click;
        }

        private void AudioSettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            // ---- Encoding options (labels only; we map to CodecType below)
            var encodings = new[]
            {
                (Value: CodecType.Pcm16, Label: "16-bit PCM"),
                (Value: CodecType.Pcm8,  Label: "8-bit PCM"),
                (Value: CodecType.MuLaw, Label: "μ-law")
            };
            ComboEncoding.ItemsSource = encodings.Select(x => x.Label).ToList();

            // Select encoding from current settings
            var settings = _pduService.Settings ?? new PduSettings();
            var currentCodec = settings.Codec;
            var encIdx = Array.FindIndex(encodings, x => x.Value == currentCodec);
            ComboEncoding.SelectedIndex = encIdx >= 0 ? encIdx : 0;

            // ---- Sample rates
            var sampleRates = new[] { 8000, 16000, 32000, 44100, 48000 };
            ComboPlaybackRate.ItemsSource = sampleRates;
            ComboCaptureRate.ItemsSource = sampleRates;

            // Use the persisted DIS sample rate for capture (what we put into Signal PDU header)
            var persistedHz = settings.SampleRate <= 0 ? 8000 : settings.SampleRate;
            if (!sampleRates.Contains(persistedHz)) persistedHz = 8000;

            ComboCaptureRate.SelectedItem = persistedHz;

            // For playback (RX path) just mirror capture unless user had something selected already.
            if (ComboPlaybackRate.SelectedItem == null)
                ComboPlaybackRate.SelectedItem = persistedHz;

            try
            {
                using var mm = new MMDeviceEnumerator();

                // Outputs (Render)
                var outputs = mm.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active)
                                .Select(d => d.FriendlyName)
                                .Distinct()
                                .OrderBy(n => n)
                                .ToList();
                if (outputs.Count == 0) outputs.Add("Default Output Device");
                ComboOutputDevice.ItemsSource = outputs;

                // Prefer the OS default output if it’s present
                try
                {
                    var defOut = mm.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render,
                                                            NAudio.CoreAudioApi.Role.Multimedia)?.FriendlyName;
                    if (!string.IsNullOrWhiteSpace(defOut) && outputs.Contains(defOut))
                        ComboOutputDevice.SelectedItem = defOut;
                }
                catch { /* ignore — will fall back below */ }

                // Fallback to first item if nothing selected
                if (ComboOutputDevice.SelectedIndex < 0)
                    ComboOutputDevice.SelectedIndex = 0;

                // Inputs (Capture)
                var inputs = mm.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.DeviceState.Active)
                               .Select(d => d.FriendlyName)
                               .Distinct()
                               .OrderBy(n => n)
                               .ToList();
                if (inputs.Count == 0) inputs.Add("Default Input Device");
                ComboInputDevice.ItemsSource = inputs;

                // Prefer the OS default input if it’s present
                try
                {
                    var defIn = mm.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Capture,
                                                           NAudio.CoreAudioApi.Role.Multimedia)?.FriendlyName;
                    if (!string.IsNullOrWhiteSpace(defIn) && inputs.Contains(defIn))
                        ComboInputDevice.SelectedItem = defIn;
                }
                catch { /* ignore — will fall back below */ }

                // Fallback to first item if nothing selected
                if (ComboInputDevice.SelectedIndex < 0)
                    ComboInputDevice.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Audio Enum] " + ex.Message);

                // Fallback placeholders so the UI doesn't look empty if enumeration fails
                if (ComboOutputDevice.Items.Count == 0)
                {
                    ComboOutputDevice.ItemsSource = new[] { "Default Output Device" };
                    ComboOutputDevice.SelectedIndex = 0;
                }
                if (ComboInputDevice.Items.Count == 0)
                {
                    ComboInputDevice.ItemsSource = new[] { "Default Input Device" };
                    ComboInputDevice.SelectedIndex = 0;
                }
            }

            // Jitter default if empty
            if (string.IsNullOrWhiteSpace(TxtJitterMs.Text))
                TxtJitterMs.Text = "60";

            // Simple inline tips (only writes to the existing TxtAudioTips if it exists)
            void Tip(Control c, string text)
            {
                c.GotKeyboardFocus += (_, __) =>
                {
                    if (TxtAudioTips != null) TxtAudioTips.Text = text;
                };
            }
            Tip(ComboEncoding, "Encoding controls how samples are represented (16-bit PCM recommended).");
            Tip(ComboCaptureRate, "Capture sample rate used for DIS Signal PDUs.");
            Tip(ComboPlaybackRate, "Playback sample rate for received audio.");
            Tip(TxtJitterMs, "Jitter buffer in milliseconds (40–120 ms typical).");
        }

        private void BtnApply_Click(object? sender, RoutedEventArgs e)
        {
            // Validate jitter (even if you don’t store it yet)
            if (!int.TryParse(TxtJitterMs.Text?.Trim(),
                              NumberStyles.Integer,
                              CultureInfo.InvariantCulture,
                              out var jitterMs) || jitterMs < 0 || jitterMs > 5000)
            {
                MessageBox.Show("Jitter buffer must be a number between 0 and 5000 ms.",
                                "Validation",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            // Map label -> codec enum
            var selectedEnc = (ComboEncoding.SelectedItem as string) ?? "16-bit PCM";
            var codec = selectedEnc.StartsWith("16", StringComparison.Ordinal) ? CodecType.Pcm16
                      : selectedEnc.StartsWith("8", StringComparison.Ordinal) ? CodecType.Pcm8
                      : CodecType.MuLaw;

            // Sample rate for DIS header (capture rate)
            var capHz = 8000;
            if (ComboCaptureRate.SelectedItem is int hz) capHz = hz;
            else if (int.TryParse(ComboCaptureRate.Text, out var parsedHz)) capHz = parsedHz;

            // Persist into DIS/PDU settings (this is what PduService uses)
            var s = _pduService.Settings ?? new PduSettings();
            s.Codec = codec;
            s.SampleRate = capHz;
            _pduService.Settings = s;

            // Show what would be applied (devices/duplex are not persisted here)
            var outDev = ComboOutputDevice.SelectedItem?.ToString() ?? "Default Output Device";
            var inDev = ComboInputDevice.SelectedItem?.ToString() ?? "Default Input Device";
            var playHz = ComboPlaybackRate.SelectedItem?.ToString() ?? ComboPlaybackRate.Text ?? capHz.ToString(CultureInfo.InvariantCulture);
            var duplex = (ChkFullDuplex?.IsChecked == true) ? "FullDuplex" : "HalfDuplex";

            Applied?.Invoke($"Audio: {codec}, DIS SR={capHz} Hz, Jitter {jitterMs} ms, {duplex}, Out {outDev} @ {playHz} Hz, In {inDev} @ {capHz} Hz");
        }
    }
}
