using System;
using NetVox.Core.Models;

namespace NetVox.Core.Services
{
    /// <summary>
    /// Builds raw DIS Signal (Type 25) and Data (Type 26) PDUs from audio buffers.
    /// </summary>
    public static class PduBuilder
    {
        /// <summary>Builds a Signal PDU (Type 25).</summary>
        public static byte[] BuildSignalPdu(byte[] audioData, DisVersion version)
        {
            byte versionByte = version == DisVersion.V6 ? (byte)6 : (byte)7;
            const byte pduType = 25;
            var pdu = new byte[2 + audioData.Length];
            pdu[0] = versionByte;
            pdu[1] = pduType;
            Buffer.BlockCopy(audioData, 0, pdu, 2, audioData.Length);
            return pdu;
        }

        /// <summary>Builds a Data PDU (Type 26).</summary>
        public static byte[] BuildDataPdu(byte[] audioData, DisVersion version)
        {
            byte versionByte = version == DisVersion.V6 ? (byte)6 : (byte)7;
            const byte pduType = 26;
            var pdu = new byte[2 + audioData.Length];
            pdu[0] = versionByte;
            pdu[1] = pduType;
            Buffer.BlockCopy(audioData, 0, pdu, 2, audioData.Length);
            return pdu;
        }
    }
}
