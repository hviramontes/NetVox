using NetVox.Core.Input;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;
using NetVox.Core.Services;
using NetVox.Core.Utils;
using NetVox.Persistence.Repositories;
using NetVox.UI.Views;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using static NetVox.UI.Views.KeyboardSettingsView;
using NetVox.Core.Models;

namespace NetVox.UI
{
    public partial class MainWindow : Window
    {
        private GlobalKeyboardListener _keyboardListener;
        private string _pttKeyName;
        private string _muteKeyName;
        private string _channelUpKeyName;
        private string _channelDownKeyName;
        private bool _isMuted = false;
        private int _currentChannelIndex = 0;

        private readonly IRadioService _radio;
        private readonly IConfigRepository _repo;
        private readonly INetworkService _networkService;
        private readonly IPduService _pduService;
        private Profile _profile;
        private Views.ChannelManagementView _channelView = new();
        private Views.KeyboardSettingsView _keyboardView = new();

        public MainWindow()
        {
            InitializeComponent();

            // Ensure NetVox folder exists
            string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NetVox");
            Directory.CreateDirectory(basePath);

            // Load keybinds from file (or create blank file)
            string keybindsPath = Path.Combine(basePath, "keybinds.json");
            if (!File.Exists(keybindsPath))
            {
                var empty = new KeybindConfig();
                var json = JsonSerializer.Serialize(empty, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(keybindsPath, json);
            }

            try
            {
                var json = File.ReadAllText(keybindsPath);
                var keybindConfig = JsonSerializer.Deserialize<KeybindConfig>(json);
                _pttKeyName = keybindConfig?.PttKey;
                _muteKeyName = keybindConfig?.MuteKey;
                _channelUpKeyName = keybindConfig?.ChannelUpKey;
                _channelDownKeyName = keybindConfig?.ChannelDownKey;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load keybinds: " + ex.Message);
            }

            // Start keyboard listener
            _keyboardListener = new GlobalKeyboardListener();
            _keyboardListener.KeyDown += OnGlobalKeyDown;
            _keyboardListener.KeyUp += OnGlobalKeyUp;
            _keyboardListener.Start();

            // Load profile or create empty one
            string profilePath = Path.Combine(basePath, "default.json");

            _repo = new JsonConfigRepository();
            if (!File.Exists(profilePath))
            {
                _profile = new Profile
                {
                    Channels = new System.Collections.Generic.List<ChannelConfig>()
                };
                _repo.SaveProfile(_profile, profilePath);
            }

            else
            {
                try
                {
                    _profile = _repo.LoadProfile(profilePath);
                }
                catch
                {
                    _profile = new Profile
                    {
                        Channels = new System.Collections.Generic.List<ChannelConfig>()
                    };
                }

            }

            BtnChannelManagement.Click += (_, _) =>
            {
                _channelView.LoadChannels(_profile);
                MainContent.Content = _channelView;
            };

            BtnKeyboardSettings.Click += (_, _) =>
            {
                MainContent.Content = _keyboardView;
            };

            // Initialize services
            _networkService = new NetworkService();
            _pduService = new PduService(_networkService);

            var audioCapture = new AudioCaptureService();
            _radio = new RadioService(audioCapture, _pduService);

            _pduService.LogEvent += msg => Dispatcher.Invoke(() =>
                System.Diagnostics.Debug.WriteLine($"[LOG] {msg}"));

            _radio.TransmitStarted += (_, _) => Dispatcher.Invoke(() =>
                System.Diagnostics.Debug.WriteLine("[PTT] Transmit Started"));

            _radio.TransmitStopped += (_, _) => Dispatcher.Invoke(() =>
                System.Diagnostics.Debug.WriteLine("[PTT] Transmit Stopped"));

            Loaded += (_, _) =>
            {
                LoadInitialView();
            };
        }

        private void LoadInitialView()
        {
            MainContent.Content = new RadioProfileView();

            if (_profile?.Channels != null && _profile.Channels.Count > 0)
            {
                _radio.SetChannel(_profile.Channels[_currentChannelIndex].ChannelNumber);
                TxtStatus.Text = $"Ready – Channel {_profile.Channels[_currentChannelIndex].ChannelNumber}: {_profile.Channels[_currentChannelIndex].Name}";
            }
            else
            {
                TxtStatus.Text = "Ready – No channels loaded";
            }
        }


        public void SaveProfileToDisk()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NetVox");
            string path = Path.Combine(dir, "default.json");

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

        public Profile GetProfile()
        {
            return _profile;
        }

        private void OnGlobalKeyDown(Key key)
        {
            string keyName = key.ToString();

            if (keyName.Equals(_muteKeyName, StringComparison.OrdinalIgnoreCase))
            {
                _isMuted = !_isMuted;

                Dispatcher.Invoke(() =>
                {
                    TxtStatus.Text = _isMuted ? "MUTED" : ChannelStatus();
                });

                return;
            }

            if (keyName.Equals(_channelUpKeyName, StringComparison.OrdinalIgnoreCase))
            {
                if (_profile.Channels.Count == 0) return;

                _currentChannelIndex = (_currentChannelIndex + 1) % _profile.Channels.Count;
                var channel = _profile.Channels[_currentChannelIndex];
                _radio.SetChannel(channel.ChannelNumber);

                Dispatcher.Invoke(() =>
                {
                    TxtStatus.Text = ChannelStatus();
                });

                return;
            }

            if (keyName.Equals(_channelDownKeyName, StringComparison.OrdinalIgnoreCase))
            {
                if (_profile.Channels.Count == 0) return;

                _currentChannelIndex--;
                if (_currentChannelIndex < 0)
                    _currentChannelIndex = _profile.Channels.Count - 1;

                var channel = _profile.Channels[_currentChannelIndex];
                _radio.SetChannel(channel.ChannelNumber);

                Dispatcher.Invoke(() =>
                {
                    TxtStatus.Text = ChannelStatus();
                });

                return;
            }

            if (keyName.Equals(_pttKeyName, StringComparison.OrdinalIgnoreCase))
            {
                if (_isMuted || _profile.Channels.Count == 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtStatus.Text = "Cannot transmit: No channel selected or muted";
                    });
                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    _radio.BeginTransmit();
                    TxtStatus.Text = "Transmitting...";
                });
            }
        }

        private void OnGlobalKeyUp(Key key)
        {
            if (key.ToString().Equals(_pttKeyName, StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.Invoke(() =>
                {
                    _radio.EndTransmit();
                    TxtStatus.Text = _isMuted ? "MUTED" : ChannelStatus();
                });
            }
        }

        private string ChannelStatus()
        {
            if (_profile.Channels.Count == 0) return "Ready";
            var channel = _profile.Channels[_currentChannelIndex];
            return $"Ready – Channel {channel.ChannelNumber}: {channel.Name}";
        }
    }
}
