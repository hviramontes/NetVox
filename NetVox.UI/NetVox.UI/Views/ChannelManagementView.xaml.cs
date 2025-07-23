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
            GridChannels.RowEditEnding += GridChannels_RowEditEnding;


            BtnAddChannel.Click += BtnAddChannel_Click;
            BtnDeleteChannel.Click += BtnDeleteChannel_Click;
            BtnSave.Click += BtnSave_Click;


        }

        private void BtnAddChannel_Click(object sender, RoutedEventArgs e)
        {
            int nextChannel = 0;
            int nextFrequency = 30_000_000;

            // Get used channel numbers and frequencies
            var usedChannels = _channels.Select(c => c.ChannelNumber).ToHashSet();
            var usedFrequencies = _channels.Select(c => c.FrequencyHz).ToHashSet();

            // Find next unused channel number
            while (usedChannels.Contains(nextChannel))
                nextChannel++;

            // Find next unused frequency in 25 kHz steps
            while (usedFrequencies.Contains(nextFrequency))
                nextFrequency += 25_000;

            _channels.Add(new ChannelConfig
            {
                ChannelNumber = nextChannel,
                Name = $"CHAN{nextChannel}",
                FrequencyHz = nextFrequency,
                BandwidthHz = 44_000
            });

        }


        private void BtnDeleteChannel_Click(object sender, RoutedEventArgs e)
        {
            if (GridChannels.SelectedItem is ChannelConfig selected)
            {
                _channels.Remove(selected);
                UpdateStatusBar($"Deleted channel #{selected.ChannelNumber}");
            }
        }

        public void LoadChannels(Profile profile)
        {
            _channels.Clear();

            var usedChannelNumbers = new HashSet<int>();
            var usedFrequencies = new HashSet<int>();

            foreach (var chan in profile.Channels)
            {
                // Auto-assign next unused channel number
                int nextChannel = 0;
                while (usedChannelNumbers.Contains(nextChannel))
                    nextChannel++;

                // Auto-assign next unused frequency in 25kHz steps
                int nextFrequency = 30_000_000;
                while (usedFrequencies.Contains(nextFrequency))
                    nextFrequency += 25_000;

                _channels.Add(new ChannelConfig
                {
                    ChannelNumber = nextChannel,
                    Name = $"CHAN{nextChannel}",
                    FrequencyHz = nextFrequency,
                    BandwidthHz = chan.BandwidthHz > 0 ? chan.BandwidthHz : 44_000
                });

                usedChannelNumbers.Add(nextChannel);
                usedFrequencies.Add(nextFrequency);
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

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                SaveChannels(main.GetProfile());
                main.SaveProfileToDisk();
                UpdateStatusBar($"Saved {_channels.Count} channels");

            }

        }

        private void GridChannels_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (App.Current.MainWindow is MainWindow main)
            {
                main.SaveProfileToDisk();
            }
        }

        private void UpdateStatusBar(string message)
        {
            if (Application.Current.MainWindow is MainWindow main)
            {
                main.TxtStatus.Text = message;
            }
        }



    }
}
