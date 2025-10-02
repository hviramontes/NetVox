using System;

namespace NetVox.Core.Models
{
    public enum DisVersion
    {
        V6 = 6, // IEEE 1278.1a-1998
        V7 = 7  // IEEE 1278.1-2012
    }

    public enum CodecType
    {
        Pcm16,
        Pcm8,
        MuLaw
    }

    public enum CryptoSystem
    {
        None = 0,                         // 0. No encryption device
        Ky28 = 1,                         // 1. KY-28
        Ky58Vinson = 2,                   // 2. KY-58 Vinson
        NarrowSpectrumSecureVoice_NSVE = 3,
        WideSpectrumSecureVoice_WSSV = 4,
        SincgarsIcom = 5,                 // SINCGARS ICOM
        Ky75 = 6,
        Ky100 = 7,
        Ky57 = 8,
        KyV5 = 9,
        Link11_KG40A_P_NTPDS = 10,
        Link11B_KG40A_S = 11,
        Link11_KG40AR = 12
    }

    // Entity tab enums
    public enum InputSource
    {
        Other = 0,
        Pilot = 1,
        Copilot = 2,
        First_Officer = 3,
        Driver = 4,
        Loader = 5,
        Gunner = 6,
        Commander = 7,
        Digital_Data_Device = 8,
        Intercom = 9
    }

    public enum EntityKind
    {
        Other = 0,
        Platform = 1,
        Munition = 2,
        LifeForm = 3,
        Environmental = 4,
        CulturalFeature = 5,
        Supply = 6,
        Radio = 7,
        Expendable = 8,
        SensorEmitter = 9
    }

    public enum Domain
    {
        Other = 0,
        Land = 1,
        Air = 2,
        Surface = 3,
        Subsurface = 4,
        Space = 5
    }

    public enum Country
    {
        Other = 0,
        USA = 225,
        UnitedKingdom = 224,
        Canada = 39,
        Australia = 13,
        Germany = 78,
        France = 71
    }

    /// <summary>How to attach the radio antenna position to an entity (or not).</summary>
    public enum AttachBy
    {
        None = 0,
        EntityId = 1,
        EntityMarking = 2
    }

    /// <summary>Antenna radiation pattern type.</summary>
    public enum AntennaPatternType
    {
        Omnidirectional = 0,
        DirectionalBeam = 1
    }

    /// <summary>
    /// DIS/PDU settings persisted in the profile and consumed by IPduService.
    /// </summary>
    public class PduSettings
    {
        // Basic DIS choices
        public DisVersion Version { get; set; } = DisVersion.V7;
        public CodecType Codec { get; set; } = CodecType.Pcm16;

        /// <summary>Transmit center frequency in Hz (e.g., 30_000_000 for 30 MHz).</summary>
        public int FrequencyHz { get; set; } = 30_000_000;

        /// <summary>Occupied bandwidth in Hz (e.g., 25_000 for 25 kHz).</summary>
        public int BandwidthHz { get; set; } = 0;

        // DIS & Crypto
        public CryptoSystem Crypto { get; set; } = CryptoSystem.None;
        public bool CryptoEnabled { get; set; } = false;
        public string CryptoKey { get; set; } = string.Empty;

        // DIS header: Exercise ID
        public ushort ExerciseId { get; set; } = 1;

        // Audio framing / Signal PDU SR
        public int SampleRate { get; set; } = 8000;

        // Entity Identifier (also surfaced on Location tab)
        public ushort SiteId { get; set; } = 1;
        public ushort ApplicationId { get; set; } = 1;
        public ushort EntityId { get; set; } = 1;

        // Radio ID (separate within the entity)
        public bool AutoRadioId { get; set; } = true;
        public ushort RadioId { get; set; } = 0;

        // Entity appearance
        public string NomenclatureVersion { get; set; } = string.Empty;
        public string Nomenclature { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        // Entity classification
        public InputSource Input { get; set; } = InputSource.Other;
        public EntityKind EntityKind { get; set; } = EntityKind.Radio;
        public Domain Domain { get; set; } = Domain.Land;
        public Country Country { get; set; } = Country.USA;

        // LOCATION: attachment or absolute pos
        public AttachBy AttachToEntityBy { get; set; } = AttachBy.None;
        public ushort AttachEntitySite { get; set; } = 0;
        public ushort AttachEntityApplication { get; set; } = 0;
        public ushort AttachEntityId { get; set; } = 0;
        public string AttachEntityMarking { get; set; } = string.Empty;

        // Absolute antenna position (meters) — active when AttachToEntityBy == None
        public double AbsoluteX { get; set; } = 0.0;
        public double AbsoluteY { get; set; } = 0.0;
        public double AbsoluteZ { get; set; } = 0.0;

        // Relative offset from attached entity (meters) — active when attached
        public double RelativeX { get; set; } = 0.0;
        public double RelativeY { get; set; } = 0.0;
        public double RelativeZ { get; set; } = 0.0;

        // Antenna pattern
        public AntennaPatternType PatternType { get; set; } = AntennaPatternType.Omnidirectional;

        // Power (watts)
        public double PowerW { get; set; } = 50.0;

        // Modulation codes
        public ushort ModulationMajor { get; set; } = 1;  // default non-zero
        public ushort ModulationDetail { get; set; } = 1;
        public ushort ModulationSystem { get; set; } = 1;

        // Spread-spectrum flags
        public bool SpreadFrequencyHopping { get; set; } = false;
        public bool SpreadPseudoNoise { get; set; } = false;
        public bool SpreadTimeHopping { get; set; } = false;
    }
}
