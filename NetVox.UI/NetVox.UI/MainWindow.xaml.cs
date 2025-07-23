using NetVox.Core.Interfaces;
using NetVox.Core.Models;
using NetVox.Core.Services;
using NetVox.Core.Utils;
using NetVox.Persistence.Repositories;
using NetVox.UI.Views;
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
        private Views.ChannelManagementView _channelView = new();

        public MainWindow()
        {
            InitializeComponent();

            BtnChannelManagement.Click += (_, _) =>
            {
                _channelView.LoadChannels(_profile);
                MainContent.Content = _channelView;
            };

            // Initialize services
            _repo = new JsonConfigRepository();
            _networkService = new NetworkService();
            _pduService = new PduService(_networkService);

            var audioCapture = new AudioCaptureService();
            _radio = new RadioService(audioCapture, _pduService);

            // Hook up service event logging
            _pduService.LogEvent += msg => Dispatcher.Invoke(() =>
                System.Diagnostics.Debug.WriteLine($"[LOG] {msg}"));

            _radio.TransmitStarted += (_, _) => Dispatcher.Invoke(() =>
                System.Diagnostics.Debug.WriteLine("[PTT] Transmit Started"));

            _radio.TransmitStopped += (_, _) => Dispatcher.Invoke(() =>
                System.Diagnostics.Debug.WriteLine("[PTT] Transmit Stopped"));

            // Set initial screen
            Loaded += (_, _) => LoadInitialView();
        }

        private void LoadInitialView()
        {
            MainContent.Content = new RadioProfileView();
        }

        private void SaveProfileToDisk()
        {
            string dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NetVox");
            string path = System.IO.Path.Combine(dir, "default.json");

            try
            {
                _profile.Channels = _channelView.GetCurrentChannels();
                _repo.SaveProfile(_profile, path);
                System.Diagnostics.Debug.WriteLine($"Saved profile to {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save failed: {ex.Message}");
            }
        }

    }
}
