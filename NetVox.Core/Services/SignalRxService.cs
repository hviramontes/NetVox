// File: NetVox.Core/Services/SignalRxService.cs
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;

namespace NetVox.Core.Services
{
    /// <summary>
    /// Minimal DIS Signal (Type 26) receiver that plays back PCM16 audio (encoding scheme 0x0004).
    /// Joins multicast if DestinationIPAddress is multicast, otherwise listens on the configured port.
    /// </summary>
    public sealed class SignalRxService : IDisposable
    {
        private readonly INetworkService _net;
        private readonly IAudioPlaybackService _playback;
        private UdpClient? _udp;
        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private readonly object _gate = new();

        // Fires once per Signal PDU (argument = sampleRate). Used by UI to blink RX.
        public event Action<int>? PacketReceived;

        public SignalRxService(INetworkService net, IAudioPlaybackService playback)
        {
            _net = net ?? throw new ArgumentNullException(nameof(net));
            _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        }

        public void Start()
        {
            lock (_gate)
            {
                if (_loopTask != null && !_loopTask.IsCompleted) return;
                _cts = new CancellationTokenSource();
                _loopTask = Task.Run(() => RunAsync(_cts.Token));
            }
        }

        public void Stop()
        {
            lock (_gate)
            {
                try { _cts?.Cancel(); } catch { }
                try { _udp?.Close(); } catch { }
                try { _udp?.Dispose(); } catch { }
                _udp = null;
                _cts = null;
                _loopTask = null;
            }
        }

        private async Task RunAsync(CancellationToken ct)
        {
            var cfg = _net.CurrentConfig ?? new NetworkConfig();

            // Port: your config calls it DestinationPort
            int port = cfg.DestinationPort > 0 ? cfg.DestinationPort : 3000;

            _udp = new UdpClient(AddressFamily.InterNetwork);
            _udp.EnableBroadcast = true;
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));

            // Multicast join if the destination is multicast (prefer DestinationIPAddress; alias DestinationIP exists too)
            string destText = string.IsNullOrWhiteSpace(cfg.DestinationIPAddress) ? cfg.DestinationIP : cfg.DestinationIPAddress;
            if (IPAddress.TryParse(destText, out var dest) && IsMulticast(dest))
            {
                try
                {
                    IPAddress local = IPAddress.Any;
                    if (!string.IsNullOrWhiteSpace(cfg.LocalIPAddress) && IPAddress.TryParse(cfg.LocalIPAddress, out var lb))
                        local = lb;
                    _udp.JoinMulticastGroup(dest, local);
                }
                catch
                {
                    // If join fails, we still receive unicast/broadcast just fine.
                }
            }

            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult res;
                try { res = await _udp.ReceiveAsync().ConfigureAwait(false); }
                catch
                {
                    if (ct.IsCancellationRequested) break;
                    else continue;
                }

                var data = res.Buffer;
                if (data == null || data.Length < 34) continue;

                // DIS header
                byte protoVer = data[0];
                byte pduType = data[2];
                byte family = data[3];
                if (protoVer != 6 && protoVer != 7) continue;
                if (family != 4) continue;   // Radio family
                if (pduType != 26) continue; // Signal PDU only

                int o = 12; // DIS common header length
                o += 6;     // Entity ID (site/app/entity)
                o += 2;     // Radio ID

                ushort enc = ReadBE16(data, o); // Encoding Scheme
                o += 2;
                if (enc != 0x0004) continue;    // Only PCM16 big-endian

                o += 2;                           // TDL Type
                int sampleRate = ReadBE32(data, o);  // Sample Rate
                o += 4;

                int dataBits = ReadBE16(data, o);    // Data Length (bits)
                o += 2;

                o += 2; // Number of Samples (not required for playback)

                // Safety: must be whole bytes
                if ((dataBits & 7) != 0) continue;

                int byteLen = dataBits / 8;
                if (o + byteLen > data.Length) continue;

                _playback.EnsureFormat(sampleRate);

                if (byteLen >= 2)
                {
                    var pcm = new byte[byteLen];
                    Buffer.BlockCopy(data, o, pcm, 0, byteLen);

                    // Convert Big Endian -> Little Endian
                    for (int i = 0; i < byteLen - 1; i += 2)
                    {
                        byte hi = pcm[i];
                        pcm[i] = pcm[i + 1];
                        pcm[i + 1] = hi;
                    }

                    _playback.EnqueuePcm16(pcm, 0, pcm.Length);

                    // Tell the UI we got a packet
                    PacketReceived?.Invoke(sampleRate);
                }
            }
        }

        private static bool IsMulticast(IPAddress ip)
        {
            var b = ip.GetAddressBytes();
            return b.Length == 4 && b[0] >= 224 && b[0] <= 239;
        }

        private static ushort ReadBE16(byte[] buf, int offset) =>
            (ushort)((buf[offset] << 8) | buf[offset + 1]);

        private static int ReadBE32(byte[] buf, int offset) =>
            (buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3];

        public void Dispose() => Stop();
    }
}
