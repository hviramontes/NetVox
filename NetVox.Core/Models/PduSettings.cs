namespace NetVox.Core.Models
{
    public enum DisVersion
    {
        V6, // IEEE 1278.1A-1998
        V7  // IEEE 1278.1-2012
    }

    public enum CodecType
    {
        Pcm16,   // 16-bit PCM
        Pcm8,    // 8-bit PCM
        MuLaw    // μ-law
    }

    public class PduSettings
    {
        /// <summary>Which DIS version to use.</summary>
        public DisVersion Version { get; set; } = DisVersion.V7;

        /// <summary>Which codec to use for the audio payload.</summary>
        public CodecType Codec { get; set; } = CodecType.Pcm16;
    }
}
