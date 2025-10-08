using System.Linq;
using System.Windows;
using System.Windows.Input;
using NAudio.CoreAudioApi;

namespace NetVox.UI.Views
{
    public partial class ModeChooserWindow : Window
    {
        public enum ModeChoice { Easy, Advanced }

        public ModeChoice? SelectedMode { get; private set; }
        public string? SelectedInputDevice { get; private set; }
        public string? SelectedOutputDevice { get; private set; }

        public ModeChooserWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Populate audio device dropdowns
            try
            {
                using var mm = new MMDeviceEnumerator();

                // Outputs
                var outputs = mm.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                                .Select(d => d.FriendlyName)
                                .Distinct()
                                .OrderBy(n => n)
                                .ToList();
                if (outputs.Count == 0) outputs.Add("Default Output Device");
                ComboOutput.ItemsSource = outputs;

                var defOut = mm.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)?.FriendlyName;
                if (!string.IsNullOrWhiteSpace(defOut) && outputs.Contains(defOut))
                    ComboOutput.SelectedItem = defOut;
                else
                    ComboOutput.SelectedIndex = 0;

                // Inputs
                var inputs = mm.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                               .Select(d => d.FriendlyName)
                               .Distinct()
                               .OrderBy(n => n)
                               .ToList();
                if (inputs.Count == 0) inputs.Add("Default Input Device");
                ComboInput.ItemsSource = inputs;

                var defIn = mm.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia)?.FriendlyName;
                if (!string.IsNullOrWhiteSpace(defIn) && inputs.Contains(defIn))
                    ComboInput.SelectedItem = defIn;
                else
                    ComboInput.SelectedIndex = 0;
            }
            catch
            {
                // If enumeration fails, the XAML still shows empty combos; that's fine.
            }

            // Default guidance text
            TxtGuidance.Text =
                "What is Easy Mode?\n" +
                "• No setup required — it auto-detects your network and uses safe defaults.\n" +
                "• Audio is 16-bit PCM @ 44.1 kHz for best clarity.\n" +
                "• Broadcasts on your local network (.255) so teammates hear you right away.\n\n" +
                "Prefer full control? Hover ‘Advanced Mode’ to see what it unlocks.";
        }

        private void EasyMode_Click(object sender, RoutedEventArgs e)
        {
            SelectedMode = ModeChoice.Easy;
            SelectedOutputDevice = ComboOutput.SelectedItem?.ToString();
            SelectedInputDevice = ComboInput.SelectedItem?.ToString();
            DialogResult = true;
            Close();
        }

        private void AdvancedMode_Click(object sender, RoutedEventArgs e)
        {
            SelectedMode = ModeChoice.Advanced;
            SelectedOutputDevice = ComboOutput.SelectedItem?.ToString();
            SelectedInputDevice = ComboInput.SelectedItem?.ToString();
            DialogResult = true;
            Close();
        }

        private void BtnEasyMode_MouseEnter(object sender, MouseEventArgs e)
        {
            TxtGuidance.Text =
                "Easy Mode: For quick voice comms without setup.\n" +
                "• Uses preconfigured channels\n" +
                "• 16-bit PCM @ 44.1 kHz\n" +
                "• Auto-broadcasts to your LAN (.255)\n" +
                "Start here if you don’t want to touch any settings.";
        }

        private void BtnAdvancedMode_MouseEnter(object sender, MouseEventArgs e)
        {
            TxtGuidance.Text =
                "Advanced Mode: Full control over everything.\n" +
                "• Network/DIS parameters\n" +
                "• Audio codecs and sample rates\n" +
                "• Channels, keybinds, and compatibility options\n" +
                "Choose this if you know what you need to configure.";
        }

        private void ModeButtons_MouseLeave(object sender, MouseEventArgs e)
        {
            TxtGuidance.Text =
                "Pick a mode to continue.\n\n" +
                "Easy Mode: zero-setup, preconfigured channels, 16-bit PCM @ 44.1k, LAN broadcast.\n" +
                "Advanced Mode: full access to network, audio, channel, and DIS settings.";
        }
    }
}
