using NetVox.Core.Input;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;
using NetVox.Core.Services;
using NetVox.Persistence.Repositories;
using NetVox.UI.Views;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading; // NEW

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

        // Hotkeys are ignored unless armed
        private bool _radioArmed = false;

        private readonly IRadioService _radio;
        private readonly IConfigRepository _repo;
        private readonly INetworkService _networkService;
        private readonly IPduService _pduService;

        // NEW: playback + RX
        private readonly IAudioPlaybackService _playback;
        private readonly SignalRxService _rx;

        // NEW: blink timer for RX banner
        private DispatcherTimer _rxBlink;

        private Profile _profile;
        private readonly ChannelManagementView _channelView = new();
        private readonly KeyboardSettingsView _keyboardView = new();
        private NetworkSettingsView _networkView;
        private DisSettingsView _disView;
        private AudioSettingsView _audioView; // NEW
        private CompatibilityImportView _compatView; // NEW
        private RadioInterfaceWindow _radioUi; // NEW

        // Live keybind reload
        private FileSystemWatcher _keybindsWatcher;
        private string _basePath;
        private string _keybindsPath;

        public MainWindow()
        {
            InitializeComponent();

            // NEW: initialize RX blink timer (180 ms pulse)
            _rxBlink = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _rxBlink.Tick += (_, __) =>
            {
                _radioUi?.SetReceiving(false);
                _rxBlink.Stop();
            };

            // Ensure NetVox folder exists
            _basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NetVox");
            Directory.CreateDirectory(_basePath);

            // Load keybinds from file (or create blank file)
            _keybindsPath = Path.Combine(_basePath, "keybinds.json");
            if (!File.Exists(_keybindsPath))
            {
                var empty = new KeybindConfig();
                var jsonNew = JsonSerializer.Serialize(empty, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_keybindsPath, jsonNew);
            }

            LoadKeybindsFromDisk();

            // Start keyboard listener (we'll ignore events until armed)
            _keyboardListener = new GlobalKeyboardListener();
            _keyboardListener.KeyDown += OnGlobalKeyDown;
            _keyboardListener.KeyUp += OnGlobalKeyUp;
            _keyboardListener.Start();

            // Load profile or create empty one
            const string profileFileName = "default.json";
            string fullProfilePath = Path.Combine(_basePath, profileFileName);

            _repo = new JsonConfigRepository();
            if (!File.Exists(fullProfilePath))
            {
                // First run: create a profile WITH default channels
                _profile = new Profile
                {
                    Channels = BuildDefaultChannels(),
                    Network = new NetworkConfig(),
                    Dis = new PduSettings()
                };

                // Save so the user sees defaults next run too
                _repo.SaveProfile(_profile, profileFileName);
            }
            else
            {
                try
                {
                    _profile = _repo.LoadProfile(profileFileName) ?? new Profile
                    {
                        Channels = new System.Collections.Generic.List<ChannelConfig>(),
                        Network = new NetworkConfig(),
                        Dis = new PduSettings()
                    };

                    // null safety for older saves
                    _profile.Network ??= new NetworkConfig();
                    _profile.Dis ??= new PduSettings();

                    // If an existing profile has 0 channels, seed defaults (non-destructive)
                    if (_profile.Channels == null || _profile.Channels.Count == 0)
                    {
                        _profile.Channels = BuildDefaultChannels();
                        _repo.SaveProfile(_profile, profileFileName);
                    }
                }
                catch
                {
                    // On load failure, fall back to defaults so user still gets a working profile
                    _profile = new Profile
                    {
                        Channels = BuildDefaultChannels(),
                        Network = new NetworkConfig(),
                        Dis = new PduSettings()
                    };
                    _repo.SaveProfile(_profile, profileFileName);
                }
            }


            // Initialize services
            _networkService = new NetworkService();
            // Apply persisted network settings from profile to live service
            _networkService.CurrentConfig = _profile.Network ?? new NetworkConfig();

            _pduService = new PduService(_networkService);
            // Apply persisted DIS settings from profile to live PDU service
            _pduService.Settings = _profile.Dis ?? new PduSettings();

            var audioCapture = new AudioCaptureService();
            _radio = new RadioService(audioCapture, _pduService);

            // NEW: create playback + RX listener
            _playback = new AudioPlaybackService();
            _rx = new SignalRxService(_networkService, _playback);

            // NEW: blink RX banner when a Signal PDU arrives
            _rx.PacketReceived += sr =>
            {
                Dispatcher.Invoke(() =>
                {
                    _radioUi?.SetReceiving(true);
                    _radioUi?.SetReceivingChannel(CurrentChannelDisplay());
                    _rxBlink.Stop();
                    _rxBlink.Start();
                });
            };

            _pduService.LogEvent += msg => Dispatcher.Invoke(() =>
                System.Diagnostics.Debug.WriteLine($"[LOG] {msg}"));

            _radio.TransmitStarted += (_, _) => Dispatcher.Invoke(() =>
                System.Diagnostics.Debug.WriteLine("[PTT] Transmit Started"));

            _radio.TransmitStopped += async (_, _) =>
            {
                // Flush any leftover audio so PDU staging buffer doesn’t keep 200–700 bytes hanging around
                await _pduService.SendSignalPduAsync(Array.Empty<byte>());
                Dispatcher.Invoke(() =>
                    System.Diagnostics.Debug.WriteLine("[PTT] Transmit Stopped"));
            };

            // Nav buttons
            BtnChannelManagement.Click += (_, _) =>
            {
                _channelView.LoadChannels(_profile);

                // Subscribe once to the Channels view's "Restore Defaults" request
                _channelView.RestoreDefaultsRequested -= OnRestoreDefaultsRequested;
                _channelView.RestoreDefaultsRequested += OnRestoreDefaultsRequested;

                MainContent.Content = _channelView;
            };


            BtnKeyboardSettings.Click += (_, _) =>
            {
                MainContent.Content = _keyboardView;
            };

            BtnRadioProfile.Click += (_, _) =>
            {
                // Navigate back to the Radio Profile view
                MainContent.Content = new RadioProfileView();
                TxtStatus.Text = HasChannels() ? ChannelStatus() : "Ready – No channels loaded";
            };

            BtnCompatibility.Click += (_, _) =>
            {
                if (_compatView == null)
                    _compatView = new CompatibilityImportView(_networkService, _pduService, _repo, _profile);
                MainContent.Content = _compatView;
                TxtStatus.Text = "Compatibility / Import";
            };

            // Network Settings
            BtnNetworkSettings.Click += (_, _) =>
            {
                if (_networkView == null)
                {
                    _networkView = new NetworkSettingsView(_networkService);
                    _networkView.Applied += msg =>
                    {
                        TxtStatus.Text = msg;

                        // Persist current network config into the profile and save
                        _profile.Network = _networkService.CurrentConfig;
                        _repo.SaveProfile(_profile, "default.json"); // repo expects just the file name
                    };
                }
                MainContent.Content = _networkView;
            };

            // DIS Settings
            BtnDisSettings.Click += (_, _) =>
            {
                if (_disView == null)
                {
                    _disView = new DisSettingsView(_pduService);
                    _disView.Applied += msg =>
                    {
                        TxtStatus.Text = msg;

                        // Persist current DIS settings into the profile and save
                        _profile.Dis = _pduService.Settings;
                        _repo.SaveProfile(_profile, "default.json");
                    };
                }
                MainContent.Content = _disView;
            };

            // Audio Settings — NEW
            BtnAudioSettings.Click += (_, _) =>
            {
                if (_audioView == null)
                {
                    _audioView = new AudioSettingsView(_pduService);
                    _audioView.Applied += msg =>
                    {
                        TxtStatus.Text = msg;

                        // Persist codec/SR changes via DIS/PDU settings
                        _profile.Dis = _pduService.Settings;
                        _repo.SaveProfile(_profile, "default.json");
                    };
                }
                MainContent.Content = _audioView;
            };

            // Start/Stop radio gating
            BtnStartRadio.Click += (_, _) => StartRadio();
            BtnStopRadio.Click += (_, _) => StopRadio();

            // Watch keybind file and live-reload when it changes
            SetupKeybindsWatcher();

            Loaded += (_, _) =>
            {
                LoadInitialView();
            };
        }

        private void LoadInitialView()
        {
            MainContent.Content = new RadioProfileView();

            if (HasChannels())
            {
                // Clamp index just in case
                if (_currentChannelIndex < 0 || _currentChannelIndex >= _profile.Channels.Count)
                    _currentChannelIndex = 0;

                var ch = _profile.Channels[_currentChannelIndex];
                _radio.SetChannel(ch.ChannelNumber);

                // Push channel freq/bw into live DIS settings
                _pduService.Settings.FrequencyHz = (int)ch.FrequencyHz;
                _pduService.Settings.BandwidthHz = ch.BandwidthHz;

                TxtStatus.Text = ChannelStatus();
            }
            else
            {
                TxtStatus.Text = "Ready – No channels loaded";
            }
        }

        // ===== Start/Stop radio gating =====

        private void StartRadio()
        {
            _radioArmed = true;
            BtnStartRadio.IsEnabled = false;
            BtnStopRadio.IsEnabled = true;

            // Create radio UI if needed
            if (_radioUi == null)
            {
                _radioUi = new RadioInterfaceWindow();

                // Hook UI events to radio actions
                _radioUi.PttPressed += () =>
                {
                    if (_isMuted || !HasChannels()) return;
                    _radio.BeginTransmit();
                    _radioUi.SetTransmitting(true);
                    // During TX, RX banner hides inside the UI control
                    TxtStatus.Text = "Transmitting...";
                };

                _radioUi.PttReleased += () =>
                {
                    _radio.EndTransmit();
                    _radioUi.SetTransmitting(false);
                    TxtStatus.Text = _isMuted ? "MUTED" : ChannelStatus();
                };

                _radioUi.ChannelUpClicked += () =>
                {
                    if (!HasChannels()) return;
                    _currentChannelIndex = (_currentChannelIndex + 1) % _profile.Channels.Count;
                    var ch = _profile.Channels[_currentChannelIndex];
                    _radio.SetChannel(ch.ChannelNumber);

                    // Push channel freq/bw into live DIS settings
                    _pduService.Settings.FrequencyHz = (int)ch.FrequencyHz;
                    _pduService.Settings.BandwidthHz = ch.BandwidthHz;

                    _radioUi.SetChannel(CurrentChannelDisplay());
                    _radioUi.SetReceivingChannel(CurrentChannelDisplay()); // keep RX banner text in sync
                    TxtStatus.Text = ChannelStatus();
                };

                _radioUi.ChannelDownClicked += () =>
                {
                    if (!HasChannels()) return;
                    _currentChannelIndex--;
                    if (_currentChannelIndex < 0) _currentChannelIndex = _profile.Channels.Count - 1;
                    var ch = _profile.Channels[_currentChannelIndex];
                    _radio.SetChannel(ch.ChannelNumber);

                    // Push channel freq/bw into live DIS settings
                    _pduService.Settings.FrequencyHz = (int)ch.FrequencyHz;
                    _pduService.Settings.BandwidthHz = ch.BandwidthHz;

                    _radioUi.SetChannel(CurrentChannelDisplay());
                    _radioUi.SetReceivingChannel(CurrentChannelDisplay()); // keep RX banner text in sync
                    TxtStatus.Text = ChannelStatus();
                };

                _radioUi.MuteToggled += isMuted =>
                {
                    _isMuted = isMuted;
                    if (_isMuted)
                    {
                        // Stop any active TX when muting
                        try { _radio.EndTransmit(); } catch { }
                    }
                    _radioUi.SetMute(_isMuted);
                    _radioUi.SetTransmitting(false);
                    TxtStatus.Text = _isMuted ? "MUTED" : ChannelStatus();
                };

                _radioUi.StopRequested += () => StopRadio();

                // If the user closes the radio window, disarm and show main
                _radioUi.Closed += (_, __) =>
                {
                    _radioArmed = false;
                    BtnStartRadio.IsEnabled = true;
                    BtnStopRadio.IsEnabled = false;
                    _radioUi = null;
                    this.Show();
                    TxtStatus.Text = "Radio stopped";
                };
            }

            // Initialize UI state
            _radioUi.SetArmed(true);
            _radioUi.SetMute(_isMuted);
            _radioUi.SetTransmitting(false);
            _radioUi.SetChannel(CurrentChannelDisplay());
            _radioUi.SetReceiving(false);                          // hide RX banner initially
            _radioUi.SetReceivingChannel(CurrentChannelDisplay()); // prime banner text

            // Ensure a channel is applied
            if (HasChannels())
            {
                if (_currentChannelIndex < 0 || _currentChannelIndex >= _profile.Channels.Count)
                    _currentChannelIndex = 0;

                var ch = _profile.Channels[_currentChannelIndex];
                _radio.SetChannel(ch.ChannelNumber);

                // Push channel freq/bw into live DIS settings
                _pduService.Settings.FrequencyHz = (int)ch.FrequencyHz;
                _pduService.Settings.BandwidthHz = ch.BandwidthHz;

                TxtStatus.Text = ChannelStatus();
            }
            else
            {
                TxtStatus.Text = "Radio armed – No channels loaded";
            }

            // NEW: start RX listening on configured port/IP (joins multicast if needed)
            _rx.Start();

            // Show radio window and hide main shell
            _radioUi.Owner = this;
            _radioUi.Show();
            this.Hide();
        }

        private void StopRadio()
        {
            // Stop RX first to close socket
            try { _rx.Stop(); } catch { /* ignore */ }

            // If transmitting, stop
            try { _radio.EndTransmit(); } catch { /* ignore */ }

            _radioArmed = false;
            _isMuted = false; // clear mute when disarming
            BtnStartRadio.IsEnabled = true;
            BtnStopRadio.IsEnabled = false;

            if (_radioUi != null)
            {
                _radioUi.Close();   // triggers Closed handler which shows main
                _radioUi = null;
            }
            else
            {
                // If window wasn't open, ensure main is visible
                this.Show();
            }

            TxtStatus.Text = "Radio stopped";
        }

        public void SaveProfileToDisk()
        {
            const string profileFileName = "default.json";

            try
            {
                // Only pull from ChannelManagementView if it is currently displayed,
                // otherwise keep whatever is already in _profile.Channels.
                if (MainContent?.Content is ChannelManagementView)
                {
                    var edited = _channelView.GetCurrentChannels();
                    if (edited != null && edited.Count > 0)
                        _profile.Channels = edited;
                }

                _repo.SaveProfile(_profile, profileFileName);
                System.Diagnostics.Debug.WriteLine($"Saved profile to {_basePath}\\{profileFileName}");

                // Don’t call ChannelStatus() if there are zero channels; guard it.
                if (HasChannels())
                    TxtStatus.Text = ChannelStatus();
                else
                    TxtStatus.Text = "Ready – No channels loaded";
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

        private bool HasChannels()
        {
            return _profile?.Channels != null && _profile.Channels.Count > 0;
        }

        private bool IsInKeyboardSettings()
        {
            return MainContent?.Content is KeyboardSettingsView;
        }

        private string CurrentChannelDisplay()
        {
            if (!HasChannels()) return "No channels";
            var ch = _profile.Channels[_currentChannelIndex];
            if (ch == null) return "No channels";
            return $"Channel {ch.ChannelNumber}: {ch.Name}";
        }

        private void OnGlobalKeyDown(Key key)
        {
            // Ignore all hotkeys unless radio is armed, or while editing keys
            if (!_radioArmed || IsInKeyboardSettings()) return;

            string keyName = key.ToString();

            // Mute toggle
            if (!string.IsNullOrWhiteSpace(_muteKeyName) &&
                keyName.Equals(_muteKeyName, StringComparison.OrdinalIgnoreCase))
            {
                _isMuted = !_isMuted;

                Dispatcher.Invoke(() =>
                {
                    _radioUi?.SetMute(_isMuted);
                    TxtStatus.Text = _isMuted ? "MUTED" : ChannelStatus();
                });

                return;
            }

            // Channel up
            if (!string.IsNullOrWhiteSpace(_channelUpKeyName) &&
                keyName.Equals(_channelUpKeyName, StringComparison.OrdinalIgnoreCase))
            {
                if (!HasChannels()) return;

                _currentChannelIndex = (_currentChannelIndex + 1) % _profile.Channels.Count;
                var channel = _profile.Channels[_currentChannelIndex];
                _radio.SetChannel(channel.ChannelNumber);

                // Push channel freq/bw into live DIS settings
                _pduService.Settings.FrequencyHz = (int)channel.FrequencyHz;
                _pduService.Settings.BandwidthHz = channel.BandwidthHz;

                Dispatcher.Invoke(() =>
                {
                    _radioUi?.SetChannel(CurrentChannelDisplay());
                    _radioUi?.SetReceivingChannel(CurrentChannelDisplay()); // keep RX banner text in sync
                    TxtStatus.Text = ChannelStatus();
                });

                return;
            }

            // Channel down
            if (!string.IsNullOrWhiteSpace(_channelDownKeyName) &&
                keyName.Equals(_channelDownKeyName, StringComparison.OrdinalIgnoreCase))
            {
                if (!HasChannels()) return;

                _currentChannelIndex--;
                if (_currentChannelIndex < 0)
                    _currentChannelIndex = _profile.Channels.Count - 1;

                var channel = _profile.Channels[_currentChannelIndex];
                _radio.SetChannel(channel.ChannelNumber);

                // Push channel freq/bw into live DIS settings
                _pduService.Settings.FrequencyHz = (int)channel.FrequencyHz;
                _pduService.Settings.BandwidthHz = channel.BandwidthHz;

                Dispatcher.Invoke(() =>
                {
                    _radioUi?.SetChannel(CurrentChannelDisplay());
                    _radioUi?.SetReceivingChannel(CurrentChannelDisplay()); // keep RX banner text in sync
                    TxtStatus.Text = ChannelStatus();
                });

                return;
            }

            // PTT begin
            if (!string.IsNullOrWhiteSpace(_pttKeyName) &&
                keyName.Equals(_pttKeyName, StringComparison.OrdinalIgnoreCase))
            {
                if (_isMuted || !HasChannels())
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtStatus.Text = !HasChannels()
                            ? "Cannot transmit: No channel selected"
                            : "Cannot transmit: Muted";
                    });
                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    _radio.BeginTransmit();
                    _radioUi?.SetTransmitting(true);
                    TxtStatus.Text = "Transmitting...";
                });
            }
        }

        private void OnGlobalKeyUp(Key key)
        {
            // Ignore unless armed, or while editing keys
            if (!_radioArmed || IsInKeyboardSettings()) return;

            if (!string.IsNullOrWhiteSpace(_pttKeyName) &&
                key.ToString().Equals(_pttKeyName, StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.Invoke(() =>
                {
                    _radio.EndTransmit();
                    _radioUi?.SetTransmitting(false);
                    TxtStatus.Text = _isMuted ? "MUTED" : ChannelStatus();
                });
            }
        }

        private string ChannelStatus()
        {
            if (!HasChannels())
                return "Ready – No channels loaded";

            // Clamp index
            if (_currentChannelIndex < 0 || _currentChannelIndex >= _profile.Channels.Count)
                _currentChannelIndex = 0;

            var channel = _profile.Channels[_currentChannelIndex];
            if (channel == null)
                return "Ready – No channels loaded";

            return $"Ready – Channel {channel.ChannelNumber}: {channel.Name}";
        }

        // ===== Live keybind reload =====

        private void SetupKeybindsWatcher()
        {
            try
            {
                _keybindsWatcher = new FileSystemWatcher(_basePath, "keybinds.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
                };
                _keybindsWatcher.Changed += OnKeybindsFileChanged;
                _keybindsWatcher.Created += OnKeybindsFileChanged;
                _keybindsWatcher.Renamed += OnKeybindsFileChanged;
                _keybindsWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Keybinds watcher failed: {ex.Message}");
            }
        }

        private void OnKeybindsFileChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce to avoid file-lock issues while the other view writes
            Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(100);
                ReloadKeybindsFromDisk();
            });
        }

        private void LoadKeybindsFromDisk()
        {
            try
            {
                var json = File.ReadAllText(_keybindsPath);
                var keybindConfig = JsonSerializer.Deserialize<KeybindConfig>(json) ?? new KeybindConfig();

                _pttKeyName = keybindConfig.PttKey;
                _muteKeyName = keybindConfig.MuteKey;
                _channelUpKeyName = keybindConfig.ChannelUpKey;
                _channelDownKeyName = keybindConfig.ChannelDownKey;
            }
            catch (Exception ex)
            {
                // Auto-heal: reset to blanks if file is corrupted
                System.Diagnostics.Debug.WriteLine("Failed to load keybinds: " + ex.Message);
                _pttKeyName = _muteKeyName = _channelUpKeyName = _channelDownKeyName = null;
            }
        }

        private void ReloadKeybindsFromDisk()
        {
            try
            {
                var json = File.ReadAllText(_keybindsPath);
                var cfg = JsonSerializer.Deserialize<KeybindConfig>(json) ?? new KeybindConfig();

                _pttKeyName = cfg.PttKey;
                _muteKeyName = cfg.MuteKey;
                _channelUpKeyName = cfg.ChannelUpKey;
                _channelDownKeyName = cfg.ChannelDownKey;

                System.Diagnostics.Debug.WriteLine("[Keybinds] Reloaded from disk.");

                // Update status without changing armed state
                if (_radioArmed)
                {
                    TxtStatus.Text = _isMuted ? "MUTED" : ChannelStatus();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Keybinds] Reload failed: {ex.Message}");
            }
        }

        private static System.Collections.Generic.List<ChannelConfig> BuildDefaultChannels()
        {
            // Default channels (MHz -> Hz), 44 kHz bandwidth each
            return new System.Collections.Generic.List<ChannelConfig>
    {
        new ChannelConfig { ChannelNumber = 0, Name = "SIMCON",     FrequencyHz = 32000000, BandwidthHz = 44000 }, // 32.000 MHz
        new ChannelConfig { ChannelNumber = 1, Name = "BLUE LOCON", FrequencyHz = 40000000, BandwidthHz = 44000 }, // 40.000 MHz
        new ChannelConfig { ChannelNumber = 2, Name = "SAFETY",     FrequencyHz = 32100000, BandwidthHz = 44000 }, // 32.100 MHz
        new ChannelConfig { ChannelNumber = 3, Name = "BATTALION",  FrequencyHz = 40050000, BandwidthHz = 44000 }, // 40.050 MHz
        new ChannelConfig { ChannelNumber = 4, Name = "COMPANY",    FrequencyHz = 40100000, BandwidthHz = 44000 }, // 40.100 MHz
        new ChannelConfig { ChannelNumber = 5, Name = "TAD",        FrequencyHz = 32150000, BandwidthHz = 44000 }, // 32.150 MHz
    };
        }

        // Handle "Restore Defaults" coming from ChannelManagementView
        private void OnRestoreDefaultsRequested()
        {
            var ask = MessageBox.Show(
                "Restore the default channels? This will replace your current list.",
                "Restore Default Channels",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (ask != MessageBoxResult.Yes)
                return;

            // 1) Replace in-memory channels and reset selection
            _profile.Channels = BuildDefaultChannels();
            _currentChannelIndex = 0;

            // 2) Push the new list into the grid BEFORE saving (so SaveProfileToDisk picks up the new list)
            _channelView.LoadChannels(_profile);

            // 3) Persist to disk
            SaveProfileToDisk();

            // 4) Refresh status and (optionally) re-apply first channel to radio/DIS if any
            TxtStatus.Text = $"Restored {_profile.Channels.Count} default channels";

            if (HasChannels())
            {
                var ch = _profile.Channels[_currentChannelIndex];
                _radio.SetChannel(ch.ChannelNumber);
                _pduService.Settings.FrequencyHz = (int)ch.FrequencyHz;
                _pduService.Settings.BandwidthHz = ch.BandwidthHz;
            }
        }


        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Auto-apply any pending DIS changes so they persist even if Apply wasn't clicked.
                if (_disView != null)
                {
                    try { _disView.ApplyAndRaise(); } catch { /* ignore validation popups on shutdown */ }
                }

                // Auto-save profile (safer version prevents losing changes)
                SaveProfileToDisk();
            }
            catch
            {
                // Swallow to avoid shutdown crash if disk is locked, etc.
            }

            try
            {
                _keyboardListener?.Stop();
                _keyboardListener = null;
            }
            catch { /* ignore */ }

            try
            {
                if (_keybindsWatcher != null)
                {
                    _keybindsWatcher.EnableRaisingEvents = false;
                    _keybindsWatcher.Dispose();
                    _keybindsWatcher = null;
                }
            }
            catch { /* ignore */ }

            // NEW: dispose RX + playback
            try { _rx?.Stop(); } catch { }
            try { (_rx as IDisposable)?.Dispose(); } catch { }
            try { _playback?.Dispose(); } catch { }

            // Stop blink timer
            try { _rxBlink?.Stop(); } catch { }

            base.OnClosed(e);
        }
    }
}
