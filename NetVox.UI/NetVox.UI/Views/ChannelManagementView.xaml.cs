using NetVox.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NetVox.UI.Views
{
    public partial class ChannelManagementView : UserControl
    {
        private readonly ObservableCollection<ChannelConfig> _channels;

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

        private void BtnAddChannel_Click(object? sender, RoutedEventArgs e)
        {
            int nextChannel = GetNextAvailableChannelNumber();      // allow 0, find first free non-negative
            long nextFrequency = GetNextAvailableFrequencyHz();     // 25 kHz steps, starting at 30 MHz

            _channels.Add(new ChannelConfig
            {
                ChannelNumber = nextChannel,
                Name = $"CHAN{nextChannel}",
                FrequencyHz = nextFrequency,
                BandwidthHz = 44_000
            });

            UpdateStatusBar($"Added channel #{nextChannel} at {nextFrequency:N0} Hz");
        }

        private void BtnDeleteChannel_Click(object? sender, RoutedEventArgs e)
        {
            if (GridChannels.SelectedItem is ChannelConfig selected)
            {
                _channels.Remove(selected);
                UpdateStatusBar($"Deleted channel #{selected.ChannelNumber}");
            }
        }

        /// <summary>
        /// Load channels from a profile WITHOUT renumbering them.
        /// Only falls back if values are missing/invalid.
        /// </summary>
        public void LoadChannels(Profile profile)
        {
            if (profile == null) return;

            _channels.Clear();

            foreach (var chan in profile.Channels ?? Enumerable.Empty<ChannelConfig>())
            {
                // Keep zero. Only replace if negative.
                int channelNumber = chan.ChannelNumber >= 0 ? chan.ChannelNumber : GetNextAvailableChannelNumber();
                long frequencyHz = chan.FrequencyHz > 0 ? chan.FrequencyHz : GetNextAvailableFrequencyHz();
                int bandwidthHz = chan.BandwidthHz > 0 ? chan.BandwidthHz : 44_000;
                string name = string.IsNullOrWhiteSpace(chan.Name) ? $"CHAN{channelNumber}" : chan.Name;

                _channels.Add(new ChannelConfig
                {
                    ChannelNumber = channelNumber,
                    Name = name,
                    FrequencyHz = frequencyHz,
                    BandwidthHz = bandwidthHz
                });
            }

            UpdateStatusBar($"Loaded {_channels.Count} channels");
        }

        /// <summary>Write the current grid back to the profile.</summary>
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

        public List<ChannelConfig> GetCurrentChannels() => new List<ChannelConfig>(_channels);

        private void BtnSave_Click(object? sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                SaveChannels(main.GetProfile());
                main.SaveProfileToDisk();
                UpdateStatusBar($"Saved {_channels.Count} channels");
            }
        }

        private void GridChannels_RowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
            {
                SaveChannels(main.GetProfile());
                main.SaveProfileToDisk();
                UpdateStatusBar("Changes saved");
            }
        }

        private int GetNextAvailableChannelNumber()
        {
            HashSet<int> used = _channels.Select(c => c.ChannelNumber).ToHashSet();
            int n = 0; // allow zero; pick the smallest non-negative integer not used
            while (used.Contains(n)) n++;
            return n;
        }

        private long GetNextAvailableFrequencyHz()
        {
            HashSet<long> used = _channels.Select(c => c.FrequencyHz).ToHashSet();
            long f = 30_000_000L; // 30 MHz baseline
            while (used.Contains(f)) f += 25_000L; // 25 kHz steps
            return f;
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
