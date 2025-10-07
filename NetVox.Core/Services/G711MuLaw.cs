// File: NetVox.Core/Services/G711MuLaw.cs
using System;

namespace NetVox.Core.Services
{
    /// <summary>
    /// G.711 μ-law encoder/decoder.
    /// - Encode: 16-bit linear PCM (little-endian) -> 8-bit μ-law
    /// - Decode: 8-bit μ-law -> 16-bit linear PCM (little-endian)
    /// Safe for C# 7.3; no Span<> used to avoid language level issues.
    /// </summary>
    public static class G711MuLaw
    {
        // Standard μ-law constants
        private const int BIAS = 0x84;      // 132
        private const int CLIP = 32635;

        /// <summary>
        /// Encode a block of little-endian 16-bit PCM samples to μ-law bytes.
        /// </summary>
        /// <param name="srcLe16">Source buffer containing 16-bit PCM LE samples.</param>
        /// <param name="offset">Byte offset into src.</param>
        /// <param name="countBytes">Number of bytes to read from src (must be even).</param>
        /// <returns>New byte[] with μ-law samples (one byte per input sample).</returns>
        public static byte[] EncodeFromPcm16Le(byte[] srcLe16, int offset, int countBytes)
        {
            if (srcLe16 == null) throw new ArgumentNullException(nameof(srcLe16));
            if (offset < 0 || countBytes < 0 || offset + countBytes > srcLe16.Length)
                throw new ArgumentOutOfRangeException(nameof(countBytes));
            int samples = countBytes >> 1;
            var dst = new byte[samples];

            int si = offset;
            for (int i = 0; i < samples; i++)
            {
                short s = (short)(srcLe16[si] | (srcLe16[si + 1] << 8));
                si += 2;
                dst[i] = LinearToMuLawSample(s);
            }
            return dst;
        }

        /// <summary>
        /// Decode a block of μ-law bytes to little-endian 16-bit PCM.
        /// </summary>
        /// <param name="src">Source buffer containing μ-law bytes.</param>
        /// <param name="offset">Offset into src.</param>
        /// <param name="count">Number of μ-law bytes to read.</param>
        /// <returns>New byte[] with 16-bit PCM LE (2 bytes per input sample).</returns>
        public static byte[] DecodeToPcm16Le(byte[] src, int offset, int count)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (offset < 0 || count < 0 || offset + count > src.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            var dst = new byte[count * 2];
            int di = 0;
            for (int i = 0; i < count; i++)
            {
                short s = MuLawToLinearSample(src[offset + i]);
                dst[di++] = (byte)(s & 0xFF);       // LE low byte
                dst[di++] = (byte)((s >> 8) & 0xFF);// LE high byte
            }
            return dst;
        }

        /// <summary>
        /// Convert a single 16-bit linear PCM sample to μ-law.
        /// </summary>
        public static byte LinearToMuLawSample(short sample)
        {
            int sign = (sample >> 8) & 0x80;         // sign bit (1 for negative)
            if (sample < 0) sample = (short)-sample; // absolute value
            if (sample > CLIP) sample = CLIP;

            sample = (short)(sample + BIAS);
            int exponent = MuLawExponent(sample);
            int mantissa = (sample >> (exponent + 3)) & 0x0F;
            int mu = ~(sign | (exponent << 4) | mantissa);
            return (byte)mu;
        }

        /// <summary>
        /// Convert a single μ-law byte to 16-bit linear PCM sample.
        /// </summary>
        public static short MuLawToLinearSample(byte muLaw)
        {
            muLaw = (byte)~muLaw;
            int sign = muLaw & 0x80;
            int exponent = (muLaw >> 4) & 0x07;
            int mantissa = muLaw & 0x0F;
            int sample = ((mantissa << 3) + BIAS) << exponent;
            sample -= BIAS;

            return (short)(sign != 0 ? -sample : sample);
        }

        private static int MuLawExponent(int value)
        {
            // Find segment (exponent) for μ-law: 0..7
            if (value < 0x100) return 0;
            if (value < 0x200) return 1;
            if (value < 0x400) return 2;
            if (value < 0x800) return 3;
            if (value < 0x1000) return 4;
            if (value < 0x2000) return 5;
            if (value < 0x4000) return 6;
            return 7;
        }
    }
}
