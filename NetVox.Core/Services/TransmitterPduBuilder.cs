using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NetVox.Core.Models;

namespace NetVox.Core.Services
{
    /// <summary>
    /// Builds a DIS Type 25 (Transmitter) PDU (IEEE 1278.1a).
    /// This is a minimal, fixed-size implementation (no antenna pattern list).
    /// </summary>
    internal static class TransmitterPduBuilder
    {
        // ---- Fixed sizes (bytes) ----
        // DIS header = 12
        // Body (fixed):
        //   EntityId (Site,App,Entity)      6
        //   RadioID                          2
        //   RadioEntityType (Kind,Domain,Country,Cat,Sub,Spec,Extra) 8
        //   TransmitState                    2
        //   InputSource                      2
        //   AntennaLocation (Vector3Double) 24
        //   RelativeLocation (Vector3Float)  12
        //   AntennaPatternType               2
        //   AntennaPatternCount              2  (we send 0)
        //   Frequency (uint32 Hz)            4
        //   Power (float32 watts)            4
        //   ModulationType (major,detail,system,crypto) 8 (4x uint16)
        //   ModulationParameters             32 (per your builder)
        // Total body = 108; header+body = 120.
        private const int HeaderSize = 12;
        private const int BodySize = 108;
        private const int PduSize = HeaderSize + BodySize;

        /// <summary>
        /// Build a Type-25 Transmitter PDU from settings and state.
        /// </summary>
        public static byte[] Build(PduSettings s, TransmitterState state, byte[]? modulationParams = null, uint? timestamp = null)
        {
            // Modulation parameters: use provided or generate a simple default
            modulationParams ??= DisModulationBuilder.Build(
                scheme: ToScheme(s),
                codec: s.Codec,
                frequency: (uint)Math.Clamp(s.FrequencyHz, 0, int.MaxValue)
            );

            if (modulationParams.Length != 32)
                throw new ArgumentException("Transmitter PDU requires 32 bytes of modulationParameters.", nameof(modulationParams));

            var buf = new byte[PduSize];

            // ---------------- DIS header (12) ----------------
            buf[0] = (byte)s.Version;                   // Protocol Version (6 or 7)
            buf[1] = (byte)s.ExerciseId;                // Exercise ID
            buf[2] = 25;                                // PDU Type: Transmitter (25)
            buf[3] = 4;                                 // Protocol Family: Radio (4)

            // Timestamp: DIS "relative" style 1/65536 seconds
            var ts = timestamp ?? MakeDisRelativeTimestamp();
            BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(4), ts);

            // PDU Length
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(8), (ushort)PduSize);

            // Padding (2) -> zero
            // ------------------------------------------------

            int o = HeaderSize;

            // Entity ID (Site, Application, Entity) – uint16 each
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o + 0), s.SiteId);        // Site
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o + 2), s.ApplicationId); // App
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o + 4), s.EntityId);      // Entity
            o += 6;

            // Radio ID (uint16)
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o), s.RadioId);
            o += 2;

            // Radio Entity Type (8 bytes)
            // [Kind u8][Domain u8][Country u16][Category u8][Subcategory u8][Specific u8][Extra u8]
            buf[o + 0] = (byte)s.EntityKind;
            buf[o + 1] = (byte)s.Domain;
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o + 2), (ushort)s.Country);
            buf[o + 4] = ParseByteOrZero(s.Category); // Category as free text → 0 if unknown
            buf[o + 5] = 0;                           // Subcategory
            buf[o + 6] = 0;                           // Specific
            buf[o + 7] = 0;                           // Extra
            o += 8;

            // Transmit State (uint16)
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o), (ushort)state);
            o += 2;

            // Input Source (uint16)
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o), (ushort)s.Input);
            o += 2;

            // Antenna Location (Vector3Double) – world coords meters
            WriteBEFloat64(buf, ref o, s.AttachToEntityBy == AttachBy.None ? s.AbsoluteX : 0.0);
            WriteBEFloat64(buf, ref o, s.AttachToEntityBy == AttachBy.None ? s.AbsoluteY : 0.0);
            WriteBEFloat64(buf, ref o, s.AttachToEntityBy == AttachBy.None ? s.AbsoluteZ : 0.0);

            // Relative Antenna Location (Vector3Float) – meters
            WriteBEFloat32(buf, ref o, s.AttachToEntityBy != AttachBy.None ? (float)s.RelativeX : 0f);
            WriteBEFloat32(buf, ref o, s.AttachToEntityBy != AttachBy.None ? (float)s.RelativeY : 0f);
            WriteBEFloat32(buf, ref o, s.AttachToEntityBy != AttachBy.None ? (float)s.RelativeZ : 0f);

            // Antenna Pattern Type (uint16) + Pattern Count (uint16 → 0)
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o), (ushort)s.PatternType);
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o + 2), 0);
            o += 4;

            // Frequency (uint32 Hz)
            var hz = (uint)Math.Clamp(s.FrequencyHz, 0, int.MaxValue);
            BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(o), hz);
            o += 4;

            // Transmit Power (float32 watts)
            WriteBEFloat32(buf, ref o, (float)s.PowerW);

            // Modulation Type (4 x uint16)
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o + 0), s.ModulationMajor);
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o + 2), s.ModulationDetail);
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o + 4), s.ModulationSystem);
            var crypto = (ushort)(s.CryptoEnabled ? s.Crypto : CryptoSystem.None);
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o + 6), crypto);
            o += 8;

            // Modulation Parameters (32 bytes fixed)
            modulationParams.CopyTo(buf, o);
            o += 32;

            // (No antenna pattern records; count=0)
            // Done
            Debug.Assert(o == PduSize, $"TransmitterPduBuilder: wrote {o} bytes, expected {PduSize}.");

            return buf;
        }

        // ------- helpers -------

        private static uint MakeDisRelativeTimestamp()
        {
            // Relative timestamp: (milliseconds within hour) * 65536 / 1000
            long ms = (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);
            uint msInHour = (uint)(ms % 3_600_000L);
            return (uint)((msInHour * 65536L) / 1000L);
        }

        private static byte ParseByteOrZero(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return byte.TryParse(text.Trim(), out var b) ? b : (byte)0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteBEFloat32(byte[] dst, ref int offset, float value)
        {
            Span<byte> tmp = stackalloc byte[4];
            BinaryPrimitives.WriteSingleBigEndian(tmp, value);
            tmp.CopyTo(dst.AsSpan(offset));
            offset += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteBEFloat64(byte[] dst, ref int offset, double value)
        {
            Span<byte> tmp = stackalloc byte[8];
            BinaryPrimitives.WriteDoubleBigEndian(tmp, value);
            tmp.CopyTo(dst.AsSpan(offset));
            offset += 8;
        }

        private static ModulationScheme ToScheme(PduSettings s)
        {
            // crude mapping for the optional 32-byte params we already generate
            if (s.ModulationMajor == 1 && s.ModulationDetail == 1) return ModulationScheme.AmplitudeAM;
            if (s.ModulationMajor == 2 && s.ModulationDetail == 3) return ModulationScheme.FrequencyFM;
            if (s.ModulationMajor == 3 && s.ModulationDetail == 4) return ModulationScheme.PhasePM;
            return ModulationScheme.AmplitudeAM;
        }
    }
}
