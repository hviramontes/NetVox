using System.Text.Json.Serialization;

namespace NetVox.Core.Models
{
    /// <summary>
    /// Configuration for a single radio channel (1–15).
    /// </summary>
    public class ChannelConfig
    {
        public int ChannelNumber { get; set; }

        /// <summary>Center frequency in Hz (e.g., 30000000 for 30 MHz).</summary>
        public long FrequencyHz { get; set; }

        /// <summary>Bandwidth in Hz (e.g., 25000 for 25 kHz).</summary>
        public int BandwidthHz { get; set; }

        /// <summary>Display name or label (e.g., "Alpha").</summary>
        public string Name { get; set; } = string.Empty;

        // ===== Backward-compatible aliases (not serialized) =====

        /// <summary>
        /// Legacy alias for FrequencyHz. Not serialized.
        /// Keeps older code that uses 'Frequency' compiling.
        /// </summary>
        [JsonIgnore]
        public long Frequency
        {
            get => FrequencyHz;
            set => FrequencyHz = value;
        }

        /// <summary>
        /// Legacy alias for BandwidthHz. Not serialized.
        /// Keeps older code that uses 'Bandwidth' compiling.
        /// </summary>
        [JsonIgnore]
        public int Bandwidth
        {
            get => BandwidthHz;
            set => BandwidthHz = value;
        }
    }
}
