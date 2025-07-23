using NetVox.Core.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace NetVox.UI.Views
{
    public partial class ChannelManagementView : UserControl
    {
        private ObservableCollection<ChannelConfig> _channels;

        public ChannelManagementView()
        {
            InitializeComponent();

            _channels = new ObservableCollection<ChannelConfig>();
            GridChannels.ItemsSource = _channels;

            BtnAddChannel.Click += BtnAddChannel_Click;
            BtnDeleteChannel.Click += BtnDeleteChannel_Click;
        }

        private void BtnAddChannel_Click(object sender, RoutedEventArgs e)
        {
            int nextChannel = _channels.Count > 0 ? _channels[^1].ChannelNumber + 1 : 0;

            _channels.Add(new ChannelConfig
            {
                ChannelNumber = nextChannel,
                Name = $"CHAN{nextChannel}",
                FrequencyHz = 30_000_000 + nextChannel * 25_000,
                BandwidthHz = 44_000
            });
        }

        private void BtnDeleteChannel_Click(object sender, RoutedEventArgs e)
        {
            if (GridChannels.SelectedItem is ChannelConfig selected)
            {
                _channels.Remove(selected);
            }
        }

        public void LoadChannels(Profile profile)
        {
            _channels.Clear();

            if (profile?.Channels == null || profile.Channels.Count == 0)
                return;

            foreach (var chan in profile.Channels)
            {
                _channels.Add(new ChannelConfig
                {
                    ChannelNumber = chan.ChannelNumber,
                    Name = chan.Name,
                    FrequencyHz = chan.FrequencyHz,
                    BandwidthHz = chan.BandwidthHz
                });
            }
        }


        public void SaveChannels(Profile profile)
        {
            profile.Channels.Clear();
            foreach (var chan in _channels)
            {
                profile.Channels.Add(new ChannelConfig
                {
                    ChannelNumber = chan.ChannelNumber,
                    Name = chan.Name,
                    FrequencyHz = chan.FrequencyHz,
                    BandwidthHz = chan.BandwidthHz
                });
            }
        }

        public List<ChannelConfig> GetCurrentChannels()
        {
            return new List<ChannelConfig>(_channels);
        }

    }
}
