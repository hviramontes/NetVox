namespace NetVox.Core.Models
{
    /// <summary>DIS Transmitter Transmit State (IEEE 1278.1a Table E-17).</summary>
    public enum TransmitterState : ushort
    {
        Off = 0,
        OnButNotTransmitting = 1,
        OnAndTransmitting = 2
    }
}
