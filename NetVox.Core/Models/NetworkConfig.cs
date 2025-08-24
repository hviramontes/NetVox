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
        public string LocalIPAddress { get; set; } = string.Empty;
        public string DestinationIPAddress { get; set; } = string.Empty;
        public NetworkMode Mode { get; set; } = NetworkMode.Unicast;
        /// <summary>Active radio frequency in Hz</summary>
        public int FrequencyHz { get; set; } = 30000000; // Default to 30 MHz

        /// <summary>IP address to send DIS PDUs to (unicast/multicast)</summary>
        public string DestinationIP { get; set; } = "239.2.3.1";

        /// <summary>Destination UDP port for DIS traffic</summary>
        public int DestinationPort { get; set; } = 3000;

    }
}
