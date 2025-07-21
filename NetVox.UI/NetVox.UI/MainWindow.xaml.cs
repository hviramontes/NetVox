using NetVox.Core.Interfaces;
using NetVox.Core.Models;
using NetVox.Core.Services;
using NetVox.Core.Utils;
using NetVox.Persistence.Repositories;
using System;
using System.Net;
using System.Windows;

namespace NetVox.UI
{
    public partial class MainWindow : Window
    {
        private readonly IRadioService _radio;
        private readonly IConfigRepository _repo;
        private readonly INetworkService _networkService;
        private readonly IPduService _pduService;
        private Profile _profile = new();

        public MainWindow()
        {
            InitializeComponent();

            // Instantiate in the correct order
            _repo = new JsonConfigRepository();
            _networkService = new NetworkService();
            _pduService = new PduService(_networkService);

            var audioCapture = new AudioCaptureService();
            _radio = new RadioService(audioCapture, _pduService);

            // Hook up UI button events
            BtnLoad.Click += BtnLoad_Click;
            BtnSave.Click += BtnSave_Click;
            BtnImportCnrSim.Click += BtnImportCnrSim_Click;
            BtnNetworkApply.Click += BtnNetworkApply_Click;
            BtnDisApply.Click += BtnDisApply_Click;

            // Pipe logs into diagnostics
            _pduService.LogEvent += msg => Dispatcher.Invoke(() =>
                LstLogs.Items.Add($"{DateTime.Now:HH:mm:ss} {msg}"));

            _radio.TransmitStarted += (_, _) => Dispatcher.Invoke(() =>
                LstLogs.Items.Add($"{DateTime.Now:HH:mm:ss} Transmit Started"));

            _radio.TransmitStopped += (_, _) => Dispatcher.Invoke(() =>
                LstLogs.Items.Add($"{DateTime.Now:HH:mm:ss} Transmit Stopped"));

            // Initialize UI fields
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Populate local IPs
            var ips = await _networkService.GetAvailableLocalIPsAsync();
            ComboLocalIP.ItemsSource = ips;
            ComboLocalIP.SelectedItem = _networkService.CurrentConfig.LocalIPAddress;

            // Populate Codec choices
            ComboCodec.ItemsSource = Enum.GetValues(typeof(CodecType));
            ComboCodec.SelectedItem = _pduService.Settings.Codec;

            // Populate DIS versions
            ComboVersion.ItemsSource = Enum.GetValues(typeof(DisVersion));
            ComboVersion.SelectedItem = _pduService.Settings.Version;
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
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

        private void BtnSave_Click(object sender, RoutedEventArgs e)
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

        private void BtnImportCnrSim_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select CNR-Sim config.xml",
                Filter = "XML Files (*.xml)|*.xml"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var imported = CnrSimImporter.LoadFromCnrSimXml(dlg.FileName);
                    _profile = imported;
                    TxtStatus.Text = $"Imported {imported.Channels.Count} channels from CNR-Sim";
                }
                catch (Exception ex)
                {
                    TxtStatus.Text = $"Import failed: {ex.Message}";
                }
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            TxtStatus.Text = "Starting PTT…";
            await _radio.StartTransmitAsync();
        }

        private void BtnNetworkApply_Click(object sender, RoutedEventArgs e)
        {
            _networkService.CurrentConfig.DestinationIPAddress = TxtDestinationIP.Text;

            // Auto‐detect mode: multicast if 224.0.0.0–239.255.255.255
            if (IPAddress.TryParse(TxtDestinationIP.Text, out var dest))
            {
                var first = dest.GetAddressBytes()[0];
                _networkService.CurrentConfig.Mode =
                    (first >= 224 && first <= 239)
                        ? NetworkMode.Multicast
                        : NetworkMode.Unicast;
            }
            else
            {
                _networkService.CurrentConfig.Mode = NetworkMode.Unicast;
            }

            var cfg = _networkService.CurrentConfig;
            TxtStatus.Text =
                $"Network applied: {cfg.LocalIPAddress} → {cfg.DestinationIPAddress} ({cfg.Mode})";
        }

        private void BtnDisApply_Click(object sender, RoutedEventArgs e)
        {
            if (ComboVersion.SelectedItem is DisVersion version)
                _pduService.Settings.Version = version;

            if (ComboCodec.SelectedItem is CodecType codec)
                _pduService.Settings.Codec = codec;

            TxtStatus.Text = $"DIS: v{_pduService.Settings.Version}, codec={_pduService.Settings.Codec}";
        }
    }
}
