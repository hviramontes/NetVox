using System.Collections.Generic;

namespace NetVox.Core.Models
{
    /// <summary>
    /// A radio profile: list of channels, network settings, and codec preferences.
    /// </summary>
    public class Profile
    {
        public List<ChannelConfig> Channels { get; set; }
        // TODO: Add NetworkConfig, PduSettings, etc.
    }
}
