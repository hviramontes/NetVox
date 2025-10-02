using System;
using System.Collections.Generic;
using System.Linq;

namespace NetVox.Core.Dis
{
    /// <summary>
    /// Central lookup for DIS Modulation options with cascading M -> D -> S.
    /// Names are preserved exactly as provided (including typos) unless instructed otherwise.
    /// </summary>
    public sealed class ModulationOption
    {
        public ushort Code { get; }
        public string Name { get; }

        public ModulationOption(ushort code, string name)
        {
            Code = code;
            Name = name;
        }

        public override string ToString() => $"{Code} | {Name}";
    }

    public static class DisModulationCatalog
    {
        // Major types (M)
        public static readonly IReadOnlyList<ModulationOption> Majors = new List<ModulationOption>
        {
            new(0, "Other"),
            new(1, "Amplitude"),
            new(2, "Amplitude and Angle"),
            new(3, "Angle"),
            new(4, "Combination"),
            new(5, "Pulse"),
            new(6, "Unmodulated"),
            new(7, "Carrier Phase Shift Modulation (CPSM)")
        };

        // Details (D) by Major (M)
        private static readonly Dictionary<ushort, IReadOnlyList<ModulationOption>> _detailsByMajor =
            new()
            {
                // M = 0
                [0] = new List<ModulationOption>
                {
                    new(0, "Other")
                },

                // M = 1 Amplitude
                [1] = new List<ModulationOption>
                {
                    new(0,  "Other"),
                    new(1,  "AFSK (Audio Frequency Key Shifting)"),
                    new(2,  "AM (Amplitude Modulation)"),
                    new(3,  "CW (Continuous Wave)"),
                    new(4,  "DSB (Double Sideband)"),
                    new(5,  "ISB (Independant Sideband)"),
                    new(6,  "LSB (Lower Sideband)"),
                    new(7,  "SSB-Full (Single Sideband Full)"),
                    new(8,  "SSB-Reduc (Single Sideband Reduced)"),
                    new(9,  "USB (Upper Sideband)"),
                    new(10, "VSB (Vestigial Sideband)")
                },

                // M = 2 Amplitude and Angle
                [2] = new List<ModulationOption>
                {
                    new(0, "Other"),
                    new(1, "Amplitude and Angle")
                },

                // M = 3 Angle
                [3] = new List<ModulationOption>
                {
                    new(0, "Other"),
                    new(1, "FM (Frequency Modulation)"),
                    new(2, "FSK Frequency Shift Keying)"),
                    new(3, "PM (Phase Modulation)")
                },

                // M = 4 Combination
                [4] = new List<ModulationOption>
                {
                    new(0, "Other"),
                    new(1, "Amplitude-Angle-Pulse")
                },

                // M = 5 Pulse
                [5] = new List<ModulationOption>
                {
                    new(0, "Other"),
                    new(1, "Pulse"),
                    new(2, "X Band TACAN Pulse"),
                    new(3, "Y Band TCAN Pulse")
                },

                // M = 6 Unmodulated
                [6] = new List<ModulationOption>
                {
                    new(0, "Other"),
                    new(1, "CW (Continuous Wave)")
                },

                // M = 7 Carrier Phase Shift Modulation (CPSM)
                [7] = new List<ModulationOption>
                {
                    new(0, "Other")
                }
            };

        // Shared Systems (S) list for all provided M/D pairs
        private static readonly IReadOnlyList<ModulationOption> _sharedSystems = new List<ModulationOption>
        {
            new(0,  "Other"),
            new(1,  "Generic Radio or Simple Intercom"),
            new(2,  "HAVE QUICK"),
            new(3,  "HAVE QUICK II"),
            new(4,  "HAVE QUICK IIA"),
            new(5,  "Single Channel Ground-Air Radio System (SINCGARS)"),
            new(6,  "CCTT SINCGARS"),
            new(7,  "EPLRS (Enhanced Position Location Reporting System"),
            new(8,  "JTIDS/MIDS"),
            new(9,  "Link 11"),
            new(10, "Link 11B"),
            new(11, "L-Band SATCOM"),
            new(12, "Enhanced SINCGARS 7.3"),
            new(13, "Navigation Aid")
        };

        // Systems by (Major, Detail)
        private static readonly Dictionary<(ushort major, ushort detail), IReadOnlyList<ModulationOption>> _systemsByPair =
            new();

        static DisModulationCatalog()
        {
            // Map every provided (M,D) pair to the same S list you specified
            foreach (var kvp in _detailsByMajor)
            {
                var major = kvp.Key;
                foreach (var detail in kvp.Value)
                {
                    _systemsByPair[(major, detail.Code)] = _sharedSystems;
                }
            }
        }

        // Public query API
        public static IReadOnlyList<ModulationOption> GetMajors() => Majors;

        public static IReadOnlyList<ModulationOption> GetDetails(ushort major)
        {
            return _detailsByMajor.TryGetValue(major, out var list)
                ? list
                : Array.Empty<ModulationOption>();
        }

        public static IReadOnlyList<ModulationOption> GetSystems(ushort major, ushort detail)
        {
            return _systemsByPair.TryGetValue((major, detail), out var list)
                ? list
                : Array.Empty<ModulationOption>();
        }

        public static ModulationOption? FindMajor(ushort code) =>
            Majors.FirstOrDefault(m => m.Code == code);

        public static ModulationOption? FindDetail(ushort major, ushort code) =>
            GetDetails(major).FirstOrDefault(d => d.Code == code);

        public static ModulationOption? FindSystem(ushort major, ushort detail, ushort code) =>
            GetSystems(major, detail).FirstOrDefault(s => s.Code == code);

        public static bool IsValid(ushort major, ushort detail, ushort system)
        {
            if (FindMajor(major) == null) return false;
            if (FindDetail(major, detail) == null) return false;
            return FindSystem(major, detail, system) != null;
        }
    }
}
