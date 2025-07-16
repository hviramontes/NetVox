namespace NetVox.Core.Models
{
    public enum DisVersion
    {
        V6, // IEEE 1278.1A-1998
        V7  // IEEE 1278.1-2012
    }

    public class PduSettings
    {
        /// <summary>Which DIS version to use.</summary>
        public DisVersion Version { get; set; } = DisVersion.V7;

        /// <summary>Whether to send Signal PDUs (Type 25).</summary>
        public bool SendType25 { get; set; } = true;

        /// <summary>Whether to send Signal PDUs (Type 26).</summary>
        public bool SendType26 { get; set; } = true;
    }
}
