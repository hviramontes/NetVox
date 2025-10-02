using System.Windows.Controls;

namespace NetVox.UI.Views
{
    public partial class RadioTransmitView : UserControl
    {
        public RadioTransmitView()
        {
            InitializeComponent();
        }

        public void SetArmed(bool armed)
        {
            TxtArmed.Text = armed ? "Armed" : "Stopped";
        }

        public void SetTx(bool isTransmitting)
        {
            TxtTx.Text = isTransmitting ? "Transmitting..." : "Idle";
        }

        public void SetMute(bool isMuted)
        {
            TxtMute.Text = isMuted ? "MUTED" : "Unmuted";
        }

        public void SetChannel(string channelDisplay)
        {
            TxtChannel.Text = string.IsNullOrWhiteSpace(channelDisplay) ? "—" : channelDisplay;
        }
    }
}
