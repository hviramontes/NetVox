using System;
using System.Linq;
using System.Windows;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;
using NetVox.Core.Services;
using NetVox.Persistence.Repositories;
using NetVox.Core.Interfaces;
using NetVox.Core.Services;


namespace NetVox.UI
{
    public partial class MainWindow : Window
    {
        private readonly IRadioService _radio;
        private readonly IConfigRepository _repo;
        private Profile _profile = new();
        private readonly INetworkService _networkService;


        public MainWindow()
        {
            InitializeComponent();

            // Instantiate stub services
            _radio = new StubRadioService();
            _repo = new JsonConfigRepository();
            _networkService = new NetworkService();


            // Populate channel combo (1–15)
            for (int i = 1; i <= 15; i++)
                ChannelCombo.Items.Add(i);
            ChannelCombo.SelectedIndex = 0;

            // Hook up events
            BtnLoad.Click += BtnLoad_Click;
            BtnSave.Click += BtnSave_Click;
            BtnStart.Click += BtnStart_Click;
            BtnStop.Click += BtnStop_Click;
            ChannelCombo.SelectionChanged += ChannelCombo_SelectionChanged;
            _radio.TransmitStarted += (_, _) => Dispatcher.Invoke(() => TxtStatus.Text = "PTT Started");
            _radio.TransmitStopped += (_, _) => Dispatcher.Invoke(() => TxtStatus.Text = "PTT Stopped");
            BtnNetwork.Click += BtnNetwork_Click;

        }

        private void BtnNetwork_Click(object sender, RoutedEventArgs e)
        {
            var win = new NetworkSettingsWindow(_networkService);
            win.Owner = this;
            win.ShowDialog();

            // Update status with the chosen settings
            var cfg = _networkService.CurrentConfig;
            TxtStatus.Text = $"Network: {cfg.LocalIPAddress} → {cfg.DestinationIPAddress} ({cfg.Mode})";
        }


        private void ChannelCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            int channel = (int)ChannelCombo.SelectedItem;
            try
            {
                _radio.SetChannel(channel);
                TxtStatus.Text = $"Channel set to {channel}";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error: {ex.Message}";
            }
        }

        private void BtnLoad_Click(object? sender, RoutedEventArgs e)
        {
            // Hard-code a path for now; we’ll add dialogs later
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NetVox", "default.json");
            try
            {
                _profile = _repo.LoadProfile(path);
                TxtStatus.Text = $"Loaded profile with {_profile.Channels?.Count ?? 0} channels";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Load error: {ex.Message}";
            }
        }

        private void BtnSave_Click(object? sender, RoutedEventArgs e)
        {
            string dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NetVox");
            string path = System.IO.Path.Combine(dir, "default.json");
            try
            {
                _repo.SaveProfile(_profile, path);
                TxtStatus.Text = $"Saved profile to {path}";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Save error: {ex.Message}";
            }
        }

        private async void BtnStart_Click(object? sender, RoutedEventArgs e)
        {
            TxtStatus.Text = "Starting PTT…";
            await _radio.StartTransmitAsync();
        }

        private async void BtnStop_Click(object? sender, RoutedEventArgs e)
        {
            TxtStatus.Text = "Stopping PTT…";
            await _radio.StopTransmitAsync();
        }
    }
}
