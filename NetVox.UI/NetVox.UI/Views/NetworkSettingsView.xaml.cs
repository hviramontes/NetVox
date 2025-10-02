using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using NetVox.Core.Interfaces;

namespace NetVox.UI.Views
{
    public partial class NetworkSettingsView : UserControl
    {
        private readonly INetworkService _networkService;

        /// <summary>Raised after Apply with a status string for the footer.</summary>
        public event Action<string>? Applied;

        public NetworkSettingsView(INetworkService networkService)
        {
            InitializeComponent();
            _networkService = networkService;

            Loaded += NetworkSettingsView_Loaded;
            BtnApply.Click += BtnApply_Click;

            // Wire dynamic tips immediately so focus changes update the help panel.
            WireTips();
        }

        private async void NetworkSettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Local IPs: populate dropdown and preselect current value
            var ipsEnum = await _networkService.GetAvailableLocalIPsAsync();
            var ips = ipsEnum?.ToList() ?? new List<string>();
            ComboLocalIP.ItemsSource = ips;

            var current = _networkService.CurrentConfig.LocalIPAddress;
            if (!string.IsNullOrWhiteSpace(current) && ips.Contains(current))
            {
                ComboLocalIP.SelectedItem = current;
            }
            else if (ips.Count > 0)
            {
                ComboLocalIP.SelectedIndex = 0; // sensible default
            }

            // Address and port
            TxtGroupAddress.Text = _networkService.CurrentConfig.DestinationIPAddress;
            TxtPort.Text = _networkService.CurrentConfig.DestinationPort.ToString(CultureInfo.InvariantCulture);

            // New optional fields (populate if properties exist, otherwise keep XAML defaults)
            var cfg = _networkService.CurrentConfig;

            if (TryGetIntProp(cfg, new[] { "Ttl", "TimeToLive", "MulticastTtl", "MulticastTTL" }, out var ttl))
                TxtTtl.Text = ttl.ToString(CultureInfo.InvariantCulture);

            if (TryGetIntProp(cfg, new[] { "TrafficClass", "MulticastTrafficClass", "Tos", "DSCP", "Dscp" }, out var tclass))
                TxtTrafficClass.Text = tclass.ToString(CultureInfo.InvariantCulture);

            if (TryGetIntProp(cfg, new[] { "SignalDataPacketLengthBytes", "SignalPacketLengthBytes", "SignalDataPacketLength", "SignalPacketLength" }, out var spl))
                TxtSignalPacketLen.Text = spl.ToString(CultureInfo.InvariantCulture);

            if (TryGetIntProp(cfg, new[] { "RadioHeartbeatTimeoutMs", "HeartbeatTimeoutMs", "RadioHeartbeatMs", "HeartbeatMs", "RadioHeartbeatTimeoutMillis" }, out var hb))
                TxtHeartbeatMs.Text = hb.ToString(CultureInfo.InvariantCulture);

            // Default tip
            ShowTip("Default");
        }

        private void BtnApply_Click(object? sender, RoutedEventArgs e)
        {
            var cfg = _networkService.CurrentConfig;

            // Local IP
            if (ComboLocalIP.SelectedItem is string ip)
                cfg.LocalIPAddress = ip;

            // Destination address
            cfg.DestinationIPAddress = (TxtGroupAddress.Text ?? string.Empty).Trim();

            // Port (1..65535)
            if (!int.TryParse(TxtPort.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Port must be a number from 1 to 65535.", "Invalid Port",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            cfg.DestinationPort = port;

            // TTL (0..255 typical)
            if (TryParseInt(TxtTtl.Text, out var ttl) && ttl >= 0 && ttl <= 255)
                TrySetIntProp(cfg, new[] { "Ttl", "TimeToLive", "MulticastTtl", "MulticastTTL" }, ttl);

            // Traffic Class (0..255)
            if (TryParseInt(TxtTrafficClass.Text, out var tclass) && tclass >= 0 && tclass <= 255)
                TrySetIntProp(cfg, new[] { "TrafficClass", "MulticastTrafficClass", "Tos", "DSCP", "Dscp" }, tclass);

            // Signal Data Packet Length (bytes, positive)
            if (TryParseInt(TxtSignalPacketLen.Text, out var spl) && spl > 0)
                TrySetIntProp(cfg, new[] { "SignalDataPacketLengthBytes", "SignalPacketLengthBytes", "SignalDataPacketLength", "SignalPacketLength" }, spl);

            // Heartbeat timeout (ms, positive)
            if (TryParseInt(TxtHeartbeatMs.Text, out var hb) && hb > 0)
                TrySetIntProp(cfg, new[] { "RadioHeartbeatTimeoutMs", "HeartbeatTimeoutMs", "RadioHeartbeatMs", "HeartbeatMs", "RadioHeartbeatTimeoutMillis" }, hb);

            // Summary (no Mode shown; it's auto-detected now)
            Applied?.Invoke($"Network: {cfg.LocalIPAddress} → {cfg.DestinationIPAddress}:{cfg.DestinationPort} | TTL {ttl} | TC {tclass} | Pkt {spl} | HB {hb}ms");
        }

        // ---------- dynamic tips ----------
        private void WireTips()
        {
            // Local IP
            ComboLocalIP.GotKeyboardFocus += (_, __) => ShowTip("LocalIP");
            ComboLocalIP.SelectionChanged += (_, __) => ShowTip("LocalIP");

            // Group/Broadcast/Multicast address
            TxtGroupAddress.GotKeyboardFocus += (_, __) => ShowTip("DestIP");
            TxtGroupAddress.TextChanged += (_, __) => ShowTip("DestIP");

            // Port
            TxtPort.GotKeyboardFocus += (_, __) => ShowTip("Port");

            // TTL
            TxtTtl.GotKeyboardFocus += (_, __) => ShowTip("TTL");
            TxtTtl.TextChanged += (_, __) => ShowTip("TTL");

            // Traffic Class
            TxtTrafficClass.GotKeyboardFocus += (_, __) => ShowTip("TrafficClass");
            TxtTrafficClass.TextChanged += (_, __) => ShowTip("TrafficClass");

            // Signal Packet Length
            TxtSignalPacketLen.GotKeyboardFocus += (_, __) => ShowTip("SignalLen");
            TxtSignalPacketLen.TextChanged += (_, __) => ShowTip("SignalLen");

            // Heartbeat
            TxtHeartbeatMs.GotKeyboardFocus += (_, __) => ShowTip("Heartbeat");
            TxtHeartbeatMs.TextChanged += (_, __) => ShowTip("Heartbeat");
        }

        private void ShowTip(string key)
        {
            string text = key switch
            {
                "LocalIP" => "Local IP selects which network adapter the app binds for UDP send/receive. Pick the interface on the correct subnet.",
                "DestIP" => "Destination address can be a multicast group (224.0.0.0–239.255.255.255) or a broadcast/unicast. Runtime auto-detects the mode.",
                "Port" => "UDP port for DIS PDUs (1–65535). Typical training ranges: 3000–4000. All peers must match.",
                "TTL" => "Multicast Time To Live controls how far packets propagate (0–255). 1 stays on the local subnet; 60 is a common default.",
                "TrafficClass" => "Multicast Traffic Class (DSCP/ToS). 0 for best-effort. Use a DSCP value if your network QoS honors it.",
                "SignalLen" => "Signal Data Packet Length (bytes) is the voice payload size per DIS Signal PDU. 960 fits 20 ms PCM16 @ 24 kHz (example).",
                "Heartbeat" => "Radio Heartbeat Timeout in milliseconds. Peers that don’t send heartbeats within this window are considered offline.",
                _ => "Networking tips appear here. Click a field to see context-specific guidance."
            };

            if (TxtNetTips != null)
                TxtNetTips.Text = text;
        }

        // ---------- helpers ----------
        private static bool TryParseInt(string? text, out int value)
            => int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        private static bool TryGetIntProp(object obj, string[] propNames, out int value)
        {
            foreach (var name in propNames)
            {
                var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.CanRead && (p.PropertyType == typeof(int) || p.PropertyType == typeof(short) || p.PropertyType == typeof(byte)))
                {
                    var v = p.GetValue(obj);
                    if (v is int i) { value = i; return true; }
                    if (v is short s) { value = s; return true; }
                    if (v is byte b) { value = b; return true; }
                }
            }
            value = default;
            return false;
        }

        private static void TrySetIntProp(object obj, string[] propNames, int value)
        {
            foreach (var name in propNames)
            {
                var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.CanWrite)
                {
                    try
                    {
                        if (p.PropertyType == typeof(int)) p.SetValue(obj, value);
                        else if (p.PropertyType == typeof(short)) p.SetValue(obj, checked((short)value));
                        else if (p.PropertyType == typeof(byte)) p.SetValue(obj, checked((byte)value));
                        else continue;
                        return; // set once if any alias matches
                    }
                    catch
                    {
                        // ignore overflow or type errors and try next alias
                    }
                }
            }
        }
    }
}
