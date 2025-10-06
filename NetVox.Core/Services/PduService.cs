using System;
using System.Globalization;
using System.Threading.Tasks;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;

namespace NetVox.Core.Services
{
    public sealed class PduService : IPduService
    {
        private readonly INetworkService _network;

        public PduSettings Settings { get; set; } = new PduSettings();

        public event Action<string>? LogEvent;

        // --- TX hold (leftover capture bytes that don't fill a full PDU frame yet).
        // NOTE: This buffer always stores SOURCE audio from the mic: 16-bit PCM little-endian.
        private byte[] _txHold = new byte[8192];
        private int _txHoldCount = 0;
        private readonly object _txLock = new();

        // DIS header constants
        private const byte PROTO_FAMILY_RADIO = 4;
        private const byte PDU_TYPE_SIGNAL = 26;
        private const byte PDU_TYPE_TRANSMITTER = 25;
        private const byte PDU_TYPE_RECEIVER = 27;

        // Encoding Scheme (IEEE 1278.1a “Encoding Class: Encoded Audio (0)” + Type)
        private const ushort ENCODING_SCHEME_PCM16_BE = 0x0004;      // 16-bit linear PCM, big-endian
        private const ushort ENCODING_SCHEME_PCM8_UNSIGNED = 0x0005; // 8-bit linear PCM, unsigned

        private const ushort TDL_TYPE_OTHER = 0;

        public PduService(INetworkService network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public void FlushSignalHold()
        {
            lock (_txLock)
            {
                _txHoldCount = 0;
            }
        }

        public void Log(string message) => LogEvent?.Invoke(message);

        // =========================================================
        // Type 26: Signal PDU (audio)
        // =========================================================
        public async Task SendSignalPduAsync(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid buffer range.");

            // Snapshot settings once for this call
            var sr = Settings.SampleRate <= 0 ? 44100 : Settings.SampleRate;
            var codec = Settings.Codec;

            // ---- IMPORTANT: Source vs Target formats ----
            // Source (from mic) is ALWAYS 16-bit PCM little-endian, mono.
            const int sourceBytesPerSample = 2;

            // Target (on the wire) depends on Codec:
            //   - Pcm16: 16-bit big-endian
            //   - Pcm8 : 8-bit unsigned
            int targetBytesPerSample = codec == CodecType.Pcm16 ? 2 : 1;

            // Frame by SAMPLES (not bytes). Choose a nice “audio frame” size by sample rate,
            // then compute how many SOURCE BYTES we need to accumulate to make one frame.
            int frameSamples = PickFrameSamples(codec, sr);
            int frameBytesSource = frameSamples * sourceBytesPerSample;

            int appended;
            lock (_txLock)
            {
                EnsureHoldCapacity(_txHoldCount + count);
                Buffer.BlockCopy(buffer, offset, _txHold, _txHoldCount, count);
                _txHoldCount += count;
                appended = count;
            }

            int chunks = 0;
            int totalSentBytes = 0; // bytes of SOURCE consumed
            int remainingAfter = 0;

            while (true)
            {
                byte[] src; // SOURCE bytes (16-bit LE)
                lock (_txLock)
                {
                    if (_txHoldCount < frameBytesSource)
                    {
                        remainingAfter = _txHoldCount;
                        break;
                    }

                    src = new byte[frameBytesSource];
                    Buffer.BlockCopy(_txHold, 0, src, 0, frameBytesSource);

                    // shift remaining source bytes left
                    int left = _txHoldCount - frameBytesSource;
                    if (left > 0)
                        Buffer.BlockCopy(_txHold, frameBytesSource, _txHold, 0, left);
                    _txHoldCount = left;
                }

                // Convert SOURCE(16-bit LE) -> TARGET payload
                byte[] payload;
                ushort encodingScheme;

                if (codec == CodecType.Pcm16)
                {
                    // Target is 16-bit BIG-endian. Just swap each 16-bit sample.
                    payload = new byte[src.Length];
                    for (int i = 0; i < src.Length; i += 2)
                    {
                        byte lo = src[i];
                        byte hi = src[i + 1];
                        payload[i] = hi;       // big-endian high
                        payload[i + 1] = lo;   // big-endian low
                    }
                    encodingScheme = ENCODING_SCHEME_PCM16_BE;
                }
                else if (codec == CodecType.Pcm8)
                {
                    // Target is 8-bit UNSIGNED.
                    // Convert each 16-bit little-endian sample to 8-bit: (sample >> 8) + 128
                    // This uses the top 8 bits and biases to unsigned range.
                    payload = new byte[frameSamples]; // one byte per sample
                    int si = 0;
                    for (int s = 0; s < frameSamples; s++)
                    {
                        // read LE 16
                        short sample16 = (short)(src[si] | (src[si + 1] << 8));
                        si += 2;

                        // downscale to 8-bit unsigned
                        int s8 = (sample16 >> 8) + 128; // [-32768..32767] -> [0..255]
                        if (s8 < 0) s8 = 0;
                        if (s8 > 255) s8 = 255;
                        payload[s] = (byte)s8;
                    }
                    encodingScheme = ENCODING_SCHEME_PCM8_UNSIGNED;
                }
                else
                {
                    // Not implemented codecs (e.g., MuLaw) – skip sending to avoid bad wire format.
                    Log("[PDU] Skipped frame: codec not implemented on TX path.");
                    continue;
                }

                // Build Signal PDU with TARGET payload (payload length drives DataLength/NumSamples).
                byte[] pdu = BuildSignalPdu(
                    version: (byte)Settings.Version,
                    exerciseId: (byte)Settings.ExerciseId,
                    siteId: Settings.SiteId,
                    appId: Settings.ApplicationId,
                    entityId: Settings.EntityId,
                    radioId: Settings.RadioId,
                    sampleRate: sr,
                    encodingScheme: encodingScheme,
                    tdlType: TDL_TYPE_OTHER,
                    audioPayload: payload,
                    bytesPerSample: targetBytesPerSample // used to compute “Number of Samples”
                );

                await _network.SendAsync(pdu).ConfigureAwait(false);

                chunks++;
                totalSentBytes += frameBytesSource; // we consumed this many SOURCE bytes
            }

            LogEvent?.Invoke($"[LOG] [PDU-DIAG] srSetting={sr} codec={codec} frameSamples={frameSamples} srcBytesPerSample={sourceBytesPerSample} tgtBytesPerSample={targetBytesPerSample} ex={Settings.ExerciseId} site={Settings.SiteId} app={Settings.ApplicationId} ent={Settings.EntityId} radio={Settings.RadioId}");
            LogEvent?.Invoke($"[LOG] [PDU] Signal sent in {Math.Max(chunks, 1)} chunk(s), sourceBytesConsumed={Math.Max(totalSentBytes, Math.Min(appended, frameBytesSource))}, remainingSourceBytes={remainingAfter}");
        }

        // Convenience overload
        public Task SendSignalPduAsync(byte[] buffer)
            => SendSignalPduAsync(buffer, 0, buffer?.Length ?? 0);

        // =========================================================
        // Type 25: Transmitter PDU
        // =========================================================
        public async Task SendTransmitterPduAsync(bool isOn)
        {
            var pdu = BuildTransmitterPdu(
                version: (byte)Settings.Version,
                exerciseId: (byte)Settings.ExerciseId,
                siteId: Settings.SiteId,
                appId: Settings.ApplicationId,
                entityId: Settings.EntityId,
                radioId: Settings.RadioId,
                isTransmitting: isOn
            );

            await _network.SendAsync(pdu).ConfigureAwait(false);

            LogEvent?.Invoke($"[LOG] [PDU-25] Transmitter {(isOn ? "ON (Tx)" : "ON (Idle)")} sent, len={pdu.Length}");
        }

        // =========================================================
        // Type 27: Receiver PDU (minimal stub)
        // =========================================================
        public Task SendReceiverPduAsync(bool isReceiving)
        {
            LogEvent?.Invoke($"[LOG] [PDU-27] Receiver {(isReceiving ? "ACTIVE" : "IDLE")} (stubbed)");
            return Task.CompletedTask;
        }

        // ---------- helpers ----------

        private static int PickFrameSamples(CodecType codec, int sampleRate)
        {
            // Make 8-bit @ 44.1 kHz match CNR-Sim framing (960 samples ~21.8 ms)
            if (codec == CodecType.Pcm8 && sampleRate >= 44100) return 960;

            // Otherwise keep our previous framing:
            // 80 for 8k, 160 for 16k, 320 for 32k, 480 for >= 44.1k
            if (sampleRate <= 8000) return 80;
            if (sampleRate <= 16000) return 160;
            if (sampleRate <= 32000) return 320;
            return 480;
        }

        // Back-compat wrapper (only used if anything still calls the old signature)
        private static int PickFrameSamples(int sampleRate) => PickFrameSamples(CodecType.Pcm16, sampleRate);


        private void EnsureHoldCapacity(int needed)
        {
            if (needed <= _txHold.Length) return;
            int newSize = _txHold.Length;
            while (newSize < needed) newSize *= 2;
            Array.Resize(ref _txHold, newSize);
        }

        private static uint MakeRelativeTimestamp()
        {
            // DIS timestamp units are 1/65536 sec.
            double seconds = Environment.TickCount64 / 1000.0;
            return (uint)(seconds * 65536.0);
        }

        private static void WriteBE16(byte[] buf, int offset, ushort v)
        {
            buf[offset + 0] = (byte)(v >> 8);
            buf[offset + 1] = (byte)(v & 0xFF);
        }

        private static void WriteBE32(byte[] buf, int offset, uint v)
        {
            buf[offset + 0] = (byte)((v >> 24) & 0xFF);
            buf[offset + 1] = (byte)((v >> 16) & 0xFF);
            buf[offset + 2] = (byte)((v >> 8) & 0xFF);
            buf[offset + 3] = (byte)(v & 0xFF);
        }

        private static void WriteBE64(byte[] buf, int offset, ulong v)
        {
            buf[offset + 0] = (byte)((v >> 56) & 0xFF);
            buf[offset + 1] = (byte)((v >> 48) & 0xFF);
            buf[offset + 2] = (byte)((v >> 40) & 0xFF);
            buf[offset + 3] = (byte)((v >> 32) & 0xFF);
            buf[offset + 4] = (byte)((v >> 24) & 0xFF);
            buf[offset + 5] = (byte)((v >> 16) & 0xFF);
            buf[offset + 6] = (byte)((v >> 8) & 0xFF);
            buf[offset + 7] = (byte)(v & 0xFF);
        }

        private static void WriteBEDouble(byte[] buf, int offset, double value)
        {
            ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
            WriteBE64(buf, offset, bits);
        }

        private static void WriteBEFloat(byte[] buf, int offset, float value)
        {
            uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
            WriteBE32(buf, offset, bits);
        }

        private static void WriteEntityType(byte[] buf, int offset, EntityKind kind, Domain domain, Country country, byte category = 0, byte subcategory = 0, byte specific = 0, byte extra = 0)
        {
            buf[offset + 0] = (byte)kind;               // kind (1)
            buf[offset + 1] = (byte)domain;             // domain (1)
            WriteBE16(buf, offset + 2, (ushort)country);// country (2)
            buf[offset + 4] = category;                 // category (1)
            buf[offset + 5] = subcategory;              // subcategory (1)
            buf[offset + 6] = specific;                 // specific (1)
            buf[offset + 7] = extra;                    // extra (1)
        }

        private static ushort BuildSpreadFlags(bool freqHop, bool pseudoNoise, bool timeHop)
        {
            ushort flags = 0;
            if (freqHop) flags |= 0x0001;
            if (pseudoNoise) flags |= 0x0002;
            if (timeHop) flags |= 0x0004;
            return flags;
        }

        private byte[] BuildSignalPdu(
            byte version,
            byte exerciseId,
            ushort siteId,
            ushort appId,
            ushort entityId,
            ushort radioId,
            int sampleRate,
            ushort encodingScheme,
            ushort tdlType,
            byte[] audioPayload,
            int bytesPerSample)
        {
            int dataLenBits = checked(audioPayload.Length * 8);
            int numSamples = bytesPerSample > 0 ? audioPayload.Length / bytesPerSample : audioPayload.Length;
            int pad = (4 - (audioPayload.Length & 3)) & 3;

            int bodyLen =
                6 + // EntityID
                2 + // RadioID
                2 + // EncodingScheme
                2 + // TDL Type
                4 + // SampleRate
                2 + // DataLength (bits)
                2 + // Number of Samples
                audioPayload.Length +
                pad;

            int pduLen = 12 + bodyLen;

            var buf = new byte[pduLen];

            // --- DIS header
            buf[0] = version;               // Protocol Version
            buf[1] = (byte)exerciseId;      // Exercise ID
            buf[2] = PDU_TYPE_SIGNAL;       // PDU type
            buf[3] = PROTO_FAMILY_RADIO;    // Protocol family (radio)
            WriteBE32(buf, 4, MakeRelativeTimestamp());  // Timestamp (relative)
            WriteBE16(buf, 8, (ushort)pduLen);           // PDU length
            WriteBE16(buf, 10, 0);                       // Padding

            int o = 12;

            // Entity ID
            WriteBE16(buf, o + 0, siteId);
            WriteBE16(buf, o + 2, appId);
            WriteBE16(buf, o + 4, entityId);
            o += 6;

            // Radio ID
            WriteBE16(buf, o, radioId);
            o += 2;

            // Encoding Scheme
            WriteBE16(buf, o, encodingScheme);
            o += 2;

            // TDL type
            WriteBE16(buf, o, tdlType);
            o += 2;

            // Sample Rate (Hz)
            WriteBE32(buf, o, (uint)sampleRate);
            o += 4;

            // Data Length (bits)
            WriteBE16(buf, o, (ushort)dataLenBits);
            o += 2;

            // Number of Samples
            WriteBE16(buf, o, (ushort)numSamples);
            o += 2;

            // Audio data
            Buffer.BlockCopy(audioPayload, 0, buf, o, audioPayload.Length);
            o += audioPayload.Length;

            // Pad to 32-bit boundary
            for (int i = 0; i < pad; i++) buf[o + i] = 0;

            return buf;
        }

        private byte[] BuildTransmitterPdu(
            byte version,
            byte exerciseId,
            ushort siteId,
            ushort appId,
            ushort entityId,
            ushort radioId,
            bool isTransmitting)
        {
            // fixed part lengths
            const int LEN_ENTITY_ID = 6;
            const int LEN_RADIO_ID = 2;
            const int LEN_ENTITY_TYPE = 8;
            const int LEN_TXSTATE_INPUT_PADDING = 4; // 1 + 1 + 2
            const int LEN_ANT_WORLD = 24;   // Vector3Double
            const int LEN_ANT_REL = 12;     // Vector3Float
            const int LEN_PATTERN_HDR = 4;  // AntennaPatternType (2) + PatternLength (2)
            const int LEN_FREQ = 8;         // double (we send uint64 bits via WriteBE64)
            const int LEN_BW = 4;           // float
            const int LEN_POWER = 4;        // float
            const int LEN_MODTYPE = 8;      // spread(2) + major(2) + detail(2) + system(2)
            const int LEN_CRYPTO = 4;       // cryptoSystem(2) + cryptoKeyId(2)
            const int LEN_MODPAR_HDR = 4;   // modulationParamLength(2) + padding(2)

            int bodyLen =
                LEN_ENTITY_ID +
                LEN_RADIO_ID +
                LEN_ENTITY_TYPE +
                LEN_TXSTATE_INPUT_PADDING +
                LEN_ANT_WORLD +
                LEN_ANT_REL +
                LEN_PATTERN_HDR +
                LEN_FREQ +
                LEN_BW +
                LEN_POWER +
                LEN_MODTYPE +
                LEN_CRYPTO +
                LEN_MODPAR_HDR;

            int pduLen = 12 + bodyLen;

            var buf = new byte[pduLen];

            // --- DIS header
            buf[0] = version;
            buf[1] = exerciseId;
            buf[2] = PDU_TYPE_TRANSMITTER;
            buf[3] = PROTO_FAMILY_RADIO;
            WriteBE32(buf, 4, MakeRelativeTimestamp());
            WriteBE16(buf, 8, (ushort)pduLen);
            WriteBE16(buf, 10, 0);

            int o = 12;

            // Entity ID
            WriteBE16(buf, o + 0, siteId);
            WriteBE16(buf, o + 2, appId);
            WriteBE16(buf, o + 4, entityId);
            o += LEN_ENTITY_ID;

            // Radio ID
            WriteBE16(buf, o, radioId);
            o += LEN_RADIO_ID;

            // Radio Entity Type (8 bytes)
            WriteEntityType(buf, o, Settings.EntityKind, Settings.Domain, Settings.Country);
            o += LEN_ENTITY_TYPE;

            // Transmit State (1), Input Source (1), Padding (2)
            // 0=Off, 1=On not transmitting, 2=On and transmitting
            buf[o + 0] = (byte)(isTransmitting ? 2 : 1);
            buf[o + 1] = (byte)Settings.Input;
            buf[o + 2] = 0;
            buf[o + 3] = 0;
            o += LEN_TXSTATE_INPUT_PADDING;

            // Antenna Location (World, Vector3Double)
            WriteBEDouble(buf, o + 0, Settings.AbsoluteX);
            WriteBEDouble(buf, o + 8, Settings.AbsoluteY);
            WriteBEDouble(buf, o + 16, Settings.AbsoluteZ);
            o += LEN_ANT_WORLD;

            // Relative Antenna Location (Vector3Float)
            WriteBEFloat(buf, o + 0, (float)Settings.RelativeX);
            WriteBEFloat(buf, o + 4, (float)Settings.RelativeY);
            WriteBEFloat(buf, o + 8, (float)Settings.RelativeZ);
            o += LEN_ANT_REL;

            // Antenna Pattern: Type + Length (we send none)
            WriteBE16(buf, o + 0, (ushort)Settings.PatternType);
            WriteBE16(buf, o + 2, 0);
            o += LEN_PATTERN_HDR;

            // Frequency (Hz, uint64 big-endian per DIS)
            ulong freqHzU64 = (ulong)(Settings.FrequencyHz <= 0 ? 30_000_000 : Settings.FrequencyHz);
            WriteBE64(buf, o, freqHzU64);
            o += LEN_FREQ;

            // Bandwidth (float)
            WriteBEFloat(buf, o, (float)Settings.BandwidthHz);
            o += LEN_BW;

            // Power (float, watts)
            WriteBEFloat(buf, o, (float)Settings.PowerW);
            o += LEN_POWER;

            // Modulation Type
            ushort spread = BuildSpreadFlags(Settings.SpreadFrequencyHopping, Settings.SpreadPseudoNoise, Settings.SpreadTimeHopping);
            WriteBE16(buf, o + 0, spread);
            WriteBE16(buf, o + 2, Settings.ModulationMajor);
            WriteBE16(buf, o + 4, Settings.ModulationDetail);
            WriteBE16(buf, o + 6, Settings.ModulationSystem);
            o += LEN_MODTYPE;

            // Crypto
            ushort crypto = (ushort)Settings.Crypto;
            ushort keyId = 0;
            if (!string.IsNullOrWhiteSpace(Settings.CryptoKey) &&
                ushort.TryParse(Settings.CryptoKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                keyId = parsed;
            }
            WriteBE16(buf, o + 0, Settings.CryptoEnabled ? crypto : (ushort)0);
            WriteBE16(buf, o + 2, Settings.CryptoEnabled ? keyId : (ushort)0);
            o += LEN_CRYPTO;

            // Modulation Parameters length (0) + padding (0)
            WriteBE16(buf, o + 0, 0);
            WriteBE16(buf, o + 2, 0);
            o += LEN_MODPAR_HDR;

            return buf;
        }
    }
}
