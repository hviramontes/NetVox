using System.Text.Json.Serialization;

namespace NetVox.Core.Models
{
    public enum NetworkMode
    {
        Unicast,
        Multicast,
        Broadcast
    }

    public class NetworkConfig
    {
        // Keep one backing field so DestinationIP and DestinationIPAddress always match
        private string _destinationIp = "239.2.3.1";

        public string LocalIPAddress { get; set; } = string.Empty;

        /// <summary>Canonical destination IP (unicast/multicast).</summary>
        public string DestinationIPAddress
        {
            get => _destinationIp;
            set => _destinationIp = value ?? string.Empty;
        }

        /// <summary>Legacy alias (kept for older code).</summary>
        [JsonIgnore]
        public string DestinationIP
        {
            get => _destinationIp;
            set => _destinationIp = value ?? string.Empty;
        }

        /// <summary>Destination UDP port for DIS traffic.</summary>
        public int DestinationPort { get; set; } = 3000;

        /// <summary>FYI: active radio frequency in Hz.</summary>
        public long FrequencyHz { get; set; } = 30_000_000;

        /// <summary>Mode is auto-detected now; kept for status/debug.</summary>
        public NetworkMode Mode { get; set; } = NetworkMode.Unicast;

        // ===== New fields you asked for =====

        /// <summary>Multicast Time To Live (hops). Default 60.</summary>
        public int MulticastTTL { get; set; } = 60;

        /// <summary>Multicast Traffic Class (DSCP/ToS byte). Default 0.</summary>
        public int MulticastTrafficClass { get; set; } = 0;

        /// <summary>Signal Data Packet length in bytes. Default 960.</summary>
        public int SignalPacketLengthBytes { get; set; } = 960;

        /// <summary>Radio heartbeat timeout in milliseconds. Default 120000 (2 minutes).</summary>
        public int HeartbeatTimeoutMs { get; set; } = 120_000;

        // ===== Back-compat aliases so existing code compiles without edits =====

        /// <summary>Alias for MulticastTTL; some code still calls it TimeToLive.</summary>
        [JsonIgnore]
        public int TimeToLive
        {
            get => MulticastTTL;
            set => MulticastTTL = value;
        }

        /// <summary>Alias for MulticastTrafficClass; some code may say TrafficClass.</summary>
        [JsonIgnore]
        public int TrafficClass
        {
            get => MulticastTrafficClass;
            set => MulticastTrafficClass = value;
        }
    }
}
