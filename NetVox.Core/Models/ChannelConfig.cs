namespace NetVox.Core.Models
{
    /// <summary>
    /// Configuration for a single radio channel (1–15).
    /// We’ll flesh out properties later (e.g. frequency, codec).
    /// </summary>
    public class ChannelConfig
    {
        public int ChannelNumber { get; set; }
        // TODO: Add FrequencyHz, CodecType, PttHotkey, etc.
    }
}
