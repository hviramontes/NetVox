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
    }
}
