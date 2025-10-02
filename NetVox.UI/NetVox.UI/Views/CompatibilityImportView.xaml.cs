using NetVox.Core.Interfaces;
using NetVox.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace NetVox.UI.Views
{
    public partial class CompatibilityImportView : UserControl
    {
        private readonly INetworkService _network;
        private readonly IPduService _pdu;
        private readonly IConfigRepository _repo;
        private readonly Profile _profile;

        public event Action<string>? Applied;

        private sealed class PreviewRow
        {
            public string Category { get; set; } = "";
            public string Source { get; set; } = "";
            public string Value { get; set; } = "";
            public string Target { get; set; } = "";
        }

        private readonly List<PreviewRow> _rows = new();

        public CompatibilityImportView(INetworkService network, IPduService pdu, IConfigRepository repo, Profile profile)
        {
            InitializeComponent();
            _network = network;
            _pdu = pdu;
            _repo = repo;
            _profile = profile;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // Browse button
            BtnBrowse.Click += async (_, __) =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Open CNR-Sim or ASTi config",
                    Filter = "All supported|*.xml;*.json|XML files|*.xml|JSON files|*.json",
                    Multiselect = false
                };
                if (dlg.ShowDialog() == true)
                {
                    TxtFilePath.Text = dlg.FileName;
                    await LoadFileAsync(dlg.FileName);
                }
            };

            // Drag/drop
            DropZone.AllowDrop = true;
            DropZone.DragEnter += (_, e2) => e2.Handled = true;
            DropZone.DragOver += (_, e2) => { e2.Effects = DragDropEffects.Copy; e2.Handled = true; };
            DropZone.Drop += async (_, e2) =>
            {
                if (e2.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e2.Data.GetData(DataFormats.FileDrop);
                    if (files?.Length > 0)
                    {
                        TxtFilePath.Text = files[0];
                        await LoadFileAsync(files[0]);
                    }
                }
            };

            // Tips follow selection in preview grid
            GridPreview.SelectionChanged += (_, __) =>
            {
                if (GridPreview.SelectedItem is PreviewRow row)
                {
                    TxtCompatTips.Text = $"{row.Category}\nSource: {row.Source}\n→ {row.Target}\nValue: {row.Value}";
                }
            };

            BtnApply.Click += (_, __) => ApplyRows();
        }

        private async Task LoadFileAsync(string path)
        {
            _rows.Clear();
            GridPreview.ItemsSource = null;
            TxtCompatTips.Text = "Parsing…";

            try
            {
                var ext = (Path.GetExtension(path) ?? "").ToLowerInvariant();
                if (ext == ".xml")
                {
                    await Task.Run(() => ParseCnrSimXml(path));
                }
                else if (ext == ".json")
                {
                    // Placeholder for ASTi/Voisus JSON
                    _rows.Add(new PreviewRow
                    {
                        Category = "Info",
                        Source = "ASTi/Voisus",
                        Value = "JSON import support coming next",
                        Target = "Not applied"
                    });
                }
                else
                {
                    _rows.Add(new PreviewRow
                    {
                        Category = "Error",
                        Source = "File",
                        Value = $"Unsupported file type: {ext}",
                        Target = "—"
                    });
                }

                GridPreview.ItemsSource = _rows;
                TxtCompatTips.Text = "Select a row to see details here.";
            }
            catch (Exception ex)
            {
                _rows.Add(new PreviewRow
                {
                    Category = "Error",
                    Source = "Parser",
                    Value = ex.Message,
                    Target = "—"
                });
                GridPreview.ItemsSource = _rows;
                TxtCompatTips.Text = "Parsing failed. See preview grid for errors.";
            }
        }

        // ===== CNR-Sim XML =====
        private void ParseCnrSimXml(string path)
        {
            var doc = XDocument.Load(path);

            // Find channel blocks
            var channelParents =
                doc.Descendants("cnrPresets")
                   .Descendants("team")
                   .Descendants("channels")
                   .ToList();

            if (channelParents.Count == 0)
            {
                // Fallback: any <channels>
                channelParents = doc.Descendants("channels").ToList();
            }

            var parsedChannels = new List<ChannelConfig>();

            foreach (var chParent in channelParents)
            {
                foreach (var ch in chParent.Elements("channel"))
                {
                    // name is ATTRIBUTE, rest are ELEMENTS
                    var nameAttr = (string?)ch.Attribute("name") ?? "";
                    var numStr = (string?)ch.Element("number") ?? "";
                    var freqStr = (string?)ch.Element("frequency") ?? "";
                    var bwStr = (string?)ch.Element("bandwidth") ?? "";

                    if (!int.TryParse(numStr, out var number))
                        continue;

                    long freqHzLong = 0;
                    long.TryParse(freqStr, out freqHzLong);

                    long bwHzLong = 0;
                    long.TryParse(bwStr, out bwHzLong);

                    // Explicit, safe int casts (fixes CS0266)
                    int freqHz = freqHzLong > int.MaxValue ? int.MaxValue : (int)freqHzLong;
                    int bwHz = bwHzLong > int.MaxValue ? int.MaxValue : (int)bwHzLong;

                    var cfg = new ChannelConfig
                    {
                        ChannelNumber = number,
                        Name = nameAttr,
                        FrequencyHz = freqHz,
                        BandwidthHz = bwHz
                    };
                    parsedChannels.Add(cfg);

                    // Preview rows
                    _rows.Add(new PreviewRow
                    {
                        Category = "Channel",
                        Source = $"channel[@name=\"{nameAttr}\"]/number",
                        Value = number.ToString(),
                        Target = $"Channels[{number}].ChannelNumber"
                    });
                    _rows.Add(new PreviewRow
                    {
                        Category = "Channel",
                        Source = $"channel[@name=\"{nameAttr}\"]/@name",
                        Value = nameAttr,
                        Target = $"Channels[{number}].Name"
                    });
                    _rows.Add(new PreviewRow
                    {
                        Category = "Channel",
                        Source = $"channel[@name=\"{nameAttr}\"]/frequency",
                        Value = freqHzLong.ToString(),
                        Target = $"Channels[{number}].FrequencyHz"
                    });
                    _rows.Add(new PreviewRow
                    {
                        Category = "Channel",
                        Source = $"channel[@name=\"{nameAttr}\"]/bandwidth",
                        Value = bwHzLong.ToString(),
                        Target = $"Channels[{number}].BandwidthHz"
                    });
                }

                if (parsedChannels.Count > 0) break;
            }

            // Optional network info
            var net = doc.Descendants("network").FirstOrDefault();
            if (net != null)
            {
                var ip = (string?)net.Descendants("remoteEndPoint").Elements("ipAddress").FirstOrDefault();
                var portStr = (string?)net.Descendants("remoteEndPoint").Elements("port").FirstOrDefault();
                var ttlStr = (string?)net.Descendants("multicast").Elements("timeToLive").FirstOrDefault();
                var tClassStr = (string?)net.Descendants("multicast").Elements("trafficClass").FirstOrDefault();
                var hbStr = (string?)net.Elements("heartbeatTimeout").FirstOrDefault();
                var pktStr = (string?)net.Elements("signalPacketLengthBytes").FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(ip))
                {
                    _rows.Add(new PreviewRow { Category = "Network", Source = "network/remoteEndPoint/ipAddress", Value = ip, Target = "Network.DestinationIPAddress" });
                }
                if (int.TryParse(portStr, out var port))
                {
                    _rows.Add(new PreviewRow { Category = "Network", Source = "network/remoteEndPoint/port", Value = port.ToString(), Target = "Network.DestinationPort" });
                }
                if (int.TryParse(ttlStr, out var ttl))
                {
                    _rows.Add(new PreviewRow { Category = "Network", Source = "network/multicast/timeToLive", Value = ttl.ToString(), Target = "Network.MulticastTTL" });
                }
                if (int.TryParse(tClassStr, out var tclass))
                {
                    _rows.Add(new PreviewRow { Category = "Network", Source = "network/multicast/trafficClass", Value = tclass.ToString(), Target = "Network.MulticastTrafficClass" });
                }
                if (int.TryParse(hbStr, out var hb))
                {
                    _rows.Add(new PreviewRow { Category = "Network", Source = "network/heartbeatTimeout", Value = hb.ToString(), Target = "Network.HeartbeatTimeoutMs" });
                }
                if (int.TryParse(pktStr, out var pkt))
                {
                    _rows.Add(new PreviewRow { Category = "Network", Source = "network/signalPacketLengthBytes", Value = pkt.ToString(), Target = "Network.SignalPacketLengthBytes" });
                }
            }

            parsedChannels = parsedChannels.OrderBy(c => c.ChannelNumber).ToList();
            GridPreview.Tag = parsedChannels;
        }

        private void ApplyRows()
        {
            // Channels
            if (GridPreview.Tag is List<ChannelConfig> parsedChannels && parsedChannels.Count > 0)
            {
                _profile.Channels = parsedChannels;
                Applied?.Invoke($"Imported {parsedChannels.Count} channels from CNR-Sim.");
            }

            // Network
            foreach (var row in _rows)
            {
                if (row.Category != "Network") continue;

                switch (row.Target)
                {
                    case "Network.DestinationIPAddress":
                        _network.CurrentConfig.DestinationIPAddress = row.Value?.Trim() ?? "";
                        break;
                    case "Network.DestinationPort":
                        if (int.TryParse(row.Value, out var port)) _network.CurrentConfig.DestinationPort = port;
                        break;
                    case "Network.MulticastTTL":
                        if (int.TryParse(row.Value, out var ttl)) _network.CurrentConfig.MulticastTTL = ttl;
                        break;
                    case "Network.MulticastTrafficClass":
                        if (int.TryParse(row.Value, out var tclass)) _network.CurrentConfig.MulticastTrafficClass = tclass;
                        break;
                    case "Network.HeartbeatTimeoutMs":
                        if (int.TryParse(row.Value, out var hb)) _network.CurrentConfig.HeartbeatTimeoutMs = hb;
                        break;
                    case "Network.SignalPacketLengthBytes":
                        if (int.TryParse(row.Value, out var pkt)) _network.CurrentConfig.SignalPacketLengthBytes = pkt;
                        break;
                }
            }

            _repo.SaveProfile(_profile, "default.json");

            if (_rows.Count > 0)
                TxtCompatTips.Text = "Applied. You can re-import another file or navigate away.";
        }
    }
}
