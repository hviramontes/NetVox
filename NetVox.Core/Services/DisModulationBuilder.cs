using System;
using NetVox.Core.Models;

namespace NetVox.Core.Services
{
    public static class DisModulationBuilder
    {
        public static byte[] Build(ModulationScheme scheme, CodecType codec, uint frequency)
        {
            byte[] modulationParameters = new byte[32]; // DIS spec requires 32 bytes
            Array.Clear(modulationParameters, 0, modulationParameters.Length);

            // Example: CNR-Sim (Voisus) sends Major=Amplitude, Detail=AM, System=Other
            modulationParameters[0] = (byte)ModulationMajor.Amplitude;
            modulationParameters[1] = (byte)ModulationDetail.Amplitude_Modulation;
            modulationParameters[2] = (byte)ModulationSystem.Other;

            // Encode frequency into bytes 4–7 (little-endian)
            byte[] freqBytes = BitConverter.GetBytes(frequency);
            Array.Copy(freqBytes, 0, modulationParameters, 4, 4);

            // Encode codec type into byte 8
            modulationParameters[8] = codec switch
            {
                CodecType.Pcm16 => 0x01,
                CodecType.Pcm8 => 0x02,
                CodecType.MuLaw => 0x03,
                _ => 0x00
            };

            // (Optionally add more future fields here)

            return modulationParameters;
        }
    }

    public enum ModulationMajor : byte
    {
        Amplitude = 1,
        Frequency = 2,
        Phase = 3,
        Combination = 4,
        Other = 5
    }

    public enum ModulationDetail : byte
    {
        Amplitude_Modulation = 1,
        Amplitude_Modulation_Pulse = 2,
        Frequency_Modulation = 3,
        Phase_Modulation = 4,
        Other = 5
    }

    public enum ModulationSystem : byte
    {
        Other = 0,
        Generic = 1
    }

    public enum ModulationScheme
    {
        AmplitudeAM,
        FrequencyFM,
        PhasePM
    }
}
