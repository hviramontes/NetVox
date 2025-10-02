using System.Collections.Generic;

namespace NetVox.Core.Models
{
    /// <summary>
    /// A radio profile: list of channels, network settings, and DIS/PDU preferences.
    /// </summary>
    public class Profile
    {
        public List<ChannelConfig> Channels { get; set; } = new();

        /// <summary>Persisted network configuration for the UI's Network tab.</summary>
        public NetworkConfig Network { get; set; } = new();

        /// <summary>Persisted DIS/PDU settings (version, codec, etc.).</summary>
        public PduSettings Dis { get; set; } = new();
    }
}
