using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;
using NetVox.Core.Dis; // requires NetVox.UI to reference NetVox.Core

namespace NetVox.UI.Views
{
    public partial class DisSettingsView : UserControl
    {
        private readonly IPduService _pduService;
        private static readonly Random _rng = new();

        public event Action<string>? Applied;

        // ===== Version labels =====
        private static readonly (DisVersion Value, string Label)[] _versionItems = new[]
        {
            (DisVersion.V6, "Version 6 - IEEE 1278.1a-1998"),
            (DisVersion.V7, "Version 7 - IEEE 1278.1-2012"),
        };

        // ===== Crypto systems =====
        private static readonly (CryptoSystem Value, string Label)[] _cryptoItems = new[]
        {
            (CryptoSystem.None,                     "0. No encryption device"),
            (CryptoSystem.Ky28,                     "1. KY-28"),
            (CryptoSystem.Ky58Vinson,               "2. KY-58 Vinson"),
            (CryptoSystem.NarrowSpectrumSecureVoice_NSVE, "3. Narrow Spectrum Secure Voice (NSVE)"),
            (CryptoSystem.WideSpectrumSecureVoice_WSSV,   "4. Wide Spectrum Secure Voice (WSSV)"),
            (CryptoSystem.SincgarsIcom,             "5. Single Channel Ground-Air Radio System (SINCGARS) ICOM"),
            (CryptoSystem.Ky75,                     "6. KY-75"),
            (CryptoSystem.Ky100,                    "7. KY-100"),
            (CryptoSystem.Ky57,                     "8. KY-57"),
            (CryptoSystem.KyV5,                     "9. KYV-5"),
            (CryptoSystem.Link11_KG40A_P_NTPDS,     "10. Link 11 KG-40A-P (NTPDS)"),
            (CryptoSystem.Link11B_KG40A_S,          "11. Link 11B KG-40A-S"),
            (CryptoSystem.Link11_KG40AR,            "12. Link 11 KG-40AR"),
        };

        // ===== Input Source (from enum) =====
        private static string Label<TEnum>(TEnum v) where TEnum : struct, Enum
            => $"{Convert.ToInt32(v, CultureInfo.InvariantCulture)} {v}";

        public DisSettingsView(IPduService pduService)
        {
            InitializeComponent();
            _pduService = pduService;

            Loaded += DisSettingsView_Loaded;
            BtnApply.Click += BtnApply_Click;

            // Tips: hook focus/selection events for the Entity tab controls
            Loaded += (_, __) => WireEntityTips();

            // Location behavior
            Loaded += (_, __) => WireLocationHandlers();

            // Modulation cascading
            ComboMajorType.SelectionChanged += (_, __) => PopulateDetailsFromMajor();
            ComboDetail.SelectionChanged += (_, __) => PopulateSystemsFromDetail();
        }

        // -------- Load / Populate --------
        private void DisSettingsView_Loaded(object? sender, RoutedEventArgs e)
        {
            // DIS & Crypto
            ComboVersion.ItemsSource = _versionItems.Select(v => v.Label).ToArray();
            ComboCryptoSystem.ItemsSource = _cryptoItems.Select(i => i.Label).ToArray();

            // ENTITY dropdowns
            ComboInputSource.ItemsSource = Enum.GetValues(typeof(InputSource))
                .Cast<InputSource>()
                .Select(Label)
                .ToArray();

            ComboEntityKind.ItemsSource = Enum.GetValues(typeof(EntityKind))
                .Cast<EntityKind>()
                .Select(Label)
                .ToArray();

            ComboDomain.ItemsSource = Enum.GetValues(typeof(Domain))
                .Cast<Domain>()
                .Select(Label)
                .ToArray();

            ComboCountry.ItemsSource = Enum.GetValues(typeof(Country))
                .Cast<Country>()
                .OrderBy(v => (int)v)
                .Select(Label)
                .ToArray();

            // Populate modulation triplet (Major -> Detail -> System)
            PopulateMajorsDetailsSystems();

            // Load current settings
            var s = _pduService.Settings ?? new PduSettings();

            // DIS & Crypto selections
            ComboVersion.SelectedIndex = Math.Max(0, Array.FindIndex(_versionItems, v => v.Value == s.Version));
            ComboCryptoSystem.SelectedIndex = Math.Max(0, Array.FindIndex(_cryptoItems, i => i.Value == s.Crypto));
            TxtCryptoKeyId.Text = s.CryptoKey ?? string.Empty;

            // ENTITY values
            CheckAutoRadioId.IsChecked = s.AutoRadioId;
            TxtRadioId.IsEnabled = !s.AutoRadioId;
            TxtRadioId.Text = s.RadioId.ToString(CultureInfo.InvariantCulture);

            TxtNomenclatureVersion.Text = s.NomenclatureVersion ?? string.Empty;
            TxtNomenclature.Text = s.Nomenclature ?? string.Empty;
            TxtCategory.Text = s.Category ?? string.Empty;

            ComboInputSource.SelectedIndex = IndexOf(ComboInputSource, $"{(int)s.Input} ");
            ComboEntityKind.SelectedIndex = IndexOf(ComboEntityKind, $"{(int)s.EntityKind} ");
            ComboDomain.SelectedIndex = IndexOf(ComboDomain, $"{(int)s.Domain} ");
            ComboCountry.SelectedIndex = IndexOf(ComboCountry, $"{(int)s.Country} ");

            // If auto is on and ID is zero/empty, assign a random ID immediately
            CheckAutoRadioId.Checked += AutoRadioId_Toggled;
            CheckAutoRadioId.Unchecked += AutoRadioId_Toggled;
            if (s.AutoRadioId && s.RadioId == 0)
            {
                var rnd = (ushort)_rng.Next(1, 65536);
                TxtRadioId.Text = rnd.ToString(CultureInfo.InvariantCulture);
            }

            // ===== LOCATION + IDENTIFIERS =====
            // Exercise and Entity Identifier (surfaced on Location tab)
            TxtExerciseId.Text = s.ExerciseId.ToString(CultureInfo.InvariantCulture);
            TxtSiteId.Text = s.SiteId.ToString(CultureInfo.InvariantCulture);
            TxtApplicationId.Text = s.ApplicationId.ToString(CultureInfo.InvariantCulture);
            TxtEntityId.Text = s.EntityId.ToString(CultureInfo.InvariantCulture);

            // Attach mode + marking
            ComboAttachBy.SelectedIndex = s.AttachToEntityBy switch
            {
                AttachBy.None => 0,
                AttachBy.EntityId => 1,
                AttachBy.EntityMarking => 2,
                _ => 0
            };
            TxtEntityMarking.Text = s.AttachEntityMarking ?? string.Empty;

            // Absolute / Relative positions
            TxtAbsX.Text = s.AbsoluteX.ToString(CultureInfo.InvariantCulture);
            TxtAbsY.Text = s.AbsoluteY.ToString(CultureInfo.InvariantCulture);
            TxtAbsZ.Text = s.AbsoluteZ.ToString(CultureInfo.InvariantCulture);

            TxtRelX.Text = s.RelativeX.ToString(CultureInfo.InvariantCulture);
            TxtRelY.Text = s.RelativeY.ToString(CultureInfo.InvariantCulture);
            TxtRelZ.Text = s.RelativeZ.ToString(CultureInfo.InvariantCulture);

            // Apply enable/disable based on chosen attach mode
            UpdateLocationMode();

            // Antenna pattern + power + spread flags
            ComboPatternType.SelectedIndex = s.PatternType == AntennaPatternType.DirectionalBeam ? 1 : 0;
            TxtPowerW.Text = s.PowerW.ToString(CultureInfo.InvariantCulture);

            ChkSpreadFH.IsChecked = s.SpreadFrequencyHopping;
            ChkSpreadPN.IsChecked = s.SpreadPseudoNoise;
            ChkSpreadTH.IsChecked = s.SpreadTimeHopping;

            // Preselect modulation from persisted settings (after lists exist)
            PreselectModulationFromSettings(s);
        }

        private static int IndexOf(ComboBox cb, string startsWith)
        {
            for (int i = 0; i < cb.Items.Count; i++)
            {
                if ((cb.Items[i]?.ToString() ?? "").StartsWith(startsWith, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return 0;
        }

        // -------- ENTITY: tips + auto radio id --------
        private void WireEntityTips()
        {
            void TipTextBox(TextBox tb, string message)
            {
                tb.GotKeyboardFocus += (_, __) => TxtTips.Text = message;
                tb.TextChanged += (_, __) => TxtTips.Text = message;
            }
            void TipComboBox(ComboBox cb, string message)
            {
                cb.GotKeyboardFocus += (_, __) => TxtTips.Text = message;
                cb.SelectionChanged += (_, __) => TxtTips.Text = message;
            }
            void TipCheckBox(CheckBox cb, string message)
            {
                cb.GotKeyboardFocus += (_, __) => TxtTips.Text = message;
                cb.Checked += (_, __) => TxtTips.Text = message;
                cb.Unchecked += (_, __) => TxtTips.Text = message;
            }

            TipCheckBox(CheckAutoRadioId, "Automatic Radio ID assigns a random 1–65535. Disable to enter a specific ID.");
            TipTextBox(TxtRadioId, "Radio ID identifies this radio within DIS voice PDUs.");
            TipTextBox(TxtNomenclatureVersion, "Nomenclature Version: specific modification or unit type within a family.");
            TipTextBox(TxtNomenclature, "Nomenclature: the device name/designation (free text).");
            TipComboBox(ComboInputSource, "Input Source: crew position or device feeding audio to this radio.");
            TipComboBox(ComboEntityKind, "Entity Kind: DIS enumerated class (e.g., Radio, Platform, etc.).");
            TipComboBox(ComboDomain, "Domain: Land/Air/Surface/Subsurface/Space.");
            TipTextBox(TxtCategory, "Category: free text grouping (e.g., CNR, SATCOM).");
            TipComboBox(ComboCountry, "Country: DIS country enumeration.");

            // Default tip
            TxtTips.Text = "Configure the DIS Entity attributes here. Tips will update based on the field you’re editing.";
        }

        private void AutoRadioId_Toggled(object? sender, RoutedEventArgs e)
        {
            bool auto = CheckAutoRadioId.IsChecked == true;
            TxtRadioId.IsEnabled = !auto;
            if (auto)
            {
                var rnd = (ushort)_rng.Next(1, 65536);
                TxtRadioId.Text = rnd.ToString(CultureInfo.InvariantCulture);
                TxtTips.Text = "Automatic Radio ID assigns a random 1–65535.";
            }
        }

        // -------- LOCATION: mode switch --------
        private void WireLocationHandlers()
        {
            ComboAttachBy.SelectionChanged += (_, __) => UpdateLocationMode();

            // Simple focus tips
            TxtAbsX.GotKeyboardFocus += (_, __) => TxtLocTips.Text = "Absolute coordinates (meters). Active when Attach = NONE.";
            TxtRelX.GotKeyboardFocus += (_, __) => TxtLocTips.Text = "Relative offsets (meters) from the referenced entity. Active when Attach = Entity ID/Marking.";
        }

        private void UpdateLocationMode()
        {
            var mode = (ComboAttachBy.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "NONE";
            bool byMarking = string.Equals(mode, "Entity Marking", StringComparison.OrdinalIgnoreCase);
            bool byId = string.Equals(mode, "Entity ID", StringComparison.OrdinalIgnoreCase);
            bool relative = byMarking || byId;

            TxtEntityMarking.IsEnabled = byMarking;

            TxtRelX.IsEnabled = relative;
            TxtRelY.IsEnabled = relative;
            TxtRelZ.IsEnabled = relative;

            TxtAbsX.IsEnabled = !relative;
            TxtAbsY.IsEnabled = !relative;
            TxtAbsZ.IsEnabled = !relative;

            TxtLocTips.Text = relative
                ? "Relative offsets (meters) from the referenced entity are active."
                : "Absolute coordinates (meters) are active.";
        }

        // -------- Apply --------
        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            var s = _pduService.Settings ?? new PduSettings();

            // DIS Version & Crypto
            s.Version = ComboVersion.SelectedIndex switch
            {
                0 => DisVersion.V6,
                1 => DisVersion.V7,
                _ => s.Version
            };

            s.Crypto = ComboCryptoSystem.SelectedIndex switch
            {
                1 => CryptoSystem.Ky28,
                2 => CryptoSystem.Ky58Vinson,
                3 => CryptoSystem.NarrowSpectrumSecureVoice_NSVE,
                4 => CryptoSystem.WideSpectrumSecureVoice_WSSV,
                5 => CryptoSystem.SincgarsIcom,
                6 => CryptoSystem.Ky75,
                7 => CryptoSystem.Ky100,
                8 => CryptoSystem.Ky57,
                9 => CryptoSystem.KyV5,
                10 => CryptoSystem.Link11_KG40A_P_NTPDS,
                11 => CryptoSystem.Link11B_KG40A_S,
                12 => CryptoSystem.Link11_KG40AR,
                _ => CryptoSystem.None
            };
            s.CryptoEnabled = s.Crypto != CryptoSystem.None;
            s.CryptoKey = TxtCryptoKeyId.Text ?? string.Empty;

            // Entity
            s.AutoRadioId = CheckAutoRadioId.IsChecked == true;
            if (!s.AutoRadioId)
            {
                if (!ushort.TryParse(TxtRadioId.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rid))
                {
                    MessageBox.Show("Radio ID must be a number between 0 and 65535.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                s.RadioId = rid;
            }
            else if (ushort.TryParse(TxtRadioId.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ridAuto))
            {
                s.RadioId = ridAuto;
            }

            s.NomenclatureVersion = TxtNomenclatureVersion.Text ?? string.Empty;
            s.Nomenclature = TxtNomenclature.Text ?? string.Empty;
            s.Category = TxtCategory.Text ?? string.Empty;

            s.Input = ParseEnumFromCombo<InputSource>(ComboInputSource, s.Input);
            s.EntityKind = ParseEnumFromCombo<EntityKind>(ComboEntityKind, s.EntityKind);
            s.Domain = ParseEnumFromCombo<Domain>(ComboDomain, s.Domain);
            s.Country = ParseEnumFromCombo<Country>(ComboCountry, s.Country);

            // ===== LOCATION + IDENTIFIERS =====
            if (ushort.TryParse(TxtExerciseId.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var exId))
                s.ExerciseId = exId;

            if (ushort.TryParse(TxtSiteId.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var site))
                s.SiteId = site;

            if (ushort.TryParse(TxtApplicationId.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var app))
                s.ApplicationId = app;

            if (ushort.TryParse(TxtEntityId.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ent))
                s.EntityId = ent;

            s.AttachToEntityBy = ComboAttachBy.SelectedIndex switch
            {
                1 => AttachBy.EntityId,
                2 => AttachBy.EntityMarking,
                _ => AttachBy.None
            };
            s.AttachEntityMarking = TxtEntityMarking.Text ?? string.Empty;

            if (double.TryParse(TxtAbsX.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ax)) s.AbsoluteX = ax;
            if (double.TryParse(TxtAbsY.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ay)) s.AbsoluteY = ay;
            if (double.TryParse(TxtAbsZ.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var az)) s.AbsoluteZ = az;

            if (double.TryParse(TxtRelX.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var rx)) s.RelativeX = rx;
            if (double.TryParse(TxtRelY.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ry)) s.RelativeY = ry;
            if (double.TryParse(TxtRelZ.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var rz)) s.RelativeZ = rz;

            // ANTENNA CONFIGURATION: persist modulation triplet and simple fields
            s.ModulationMajor = SelectedCode(ComboMajorType);
            s.ModulationDetail = SelectedCode(ComboDetail);
            s.ModulationSystem = SelectedCode(ComboSystem);

            // Pattern
            s.PatternType = ComboPatternType.SelectedIndex == 1 ? AntennaPatternType.DirectionalBeam : AntennaPatternType.Omnidirectional;

            // Power (optional; best-effort parse)
            if (double.TryParse(TxtPowerW.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var pw))
                s.PowerW = pw;

            // Spread flags
            s.SpreadFrequencyHopping = ChkSpreadFH.IsChecked == true;
            s.SpreadPseudoNoise = ChkSpreadPN.IsChecked == true;
            s.SpreadTimeHopping = ChkSpreadTH.IsChecked == true;

            _pduService.Settings = s;

            Applied?.Invoke(
                $"DIS saved: {s.Version}, Crypto {(s.CryptoEnabled ? s.Crypto : CryptoSystem.None)}; " +
                $"RadioId {(s.AutoRadioId ? $"{s.RadioId} (auto)" : s.RadioId)}; " +
                $"Modulation M/D/S = {s.ModulationMajor}/{s.ModulationDetail}/{s.ModulationSystem}"
            );
        }

        private static TEnum ParseEnumFromCombo<TEnum>(ComboBox cb, TEnum fallback) where TEnum : struct, Enum
        {
            if (cb.SelectedItem is string s)
            {
                var first = s.Split(new[] { ' ' }, 2)[0];
                if (int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                {
                    if (Enum.IsDefined(typeof(TEnum), n))
                        return (TEnum)Enum.ToObject(typeof(TEnum), n);
                }
            }
            return fallback;
        }

        // ====================== Modulation cascading ======================
        private void PopulateMajorsDetailsSystems()
        {
            try
            {
                ComboMajorType.Items.Clear();
                var majors = DisModulationCatalog.GetMajors();
                foreach (var m in majors)
                    ComboMajorType.Items.Add(new ComboBoxItem { Content = $"{m.Code} {m.Name}", Tag = m.Code });

                if (ComboMajorType.Items.Count == 0) PopulateMajors_Fallback();
            }
            catch
            {
                PopulateMajors_Fallback();
            }

            if (ComboMajorType.Items.Count > 0 && ComboMajorType.SelectedIndex < 0)
                ComboMajorType.SelectedIndex = 0;

            // Then cascade
            PopulateDetailsFromMajor();
            PopulateSystemsFromDetail();
        }

        private void PopulateDetailsFromMajor()
        {
            ComboDetail.Items.Clear();

            ushort majorCode = SelectedCode(ComboMajorType);

            try
            {
                var details = DisModulationCatalog.GetDetails(majorCode);
                foreach (var d in details)
                    ComboDetail.Items.Add(new ComboBoxItem { Content = $"{d.Code} {d.Name}", Tag = d.Code });

                if (ComboDetail.Items.Count == 0) PopulateDetails_Fallback(majorCode);
            }
            catch
            {
                PopulateDetails_Fallback(majorCode);
            }

            ComboDetail.IsEnabled = ComboDetail.Items.Count > 0;
            ComboDetail.SelectedIndex = ComboDetail.IsEnabled ? 0 : -1;

            PopulateSystemsFromDetail();
        }

        private void PopulateSystemsFromDetail()
        {
            ComboSystem.Items.Clear();

            ushort majorCode = SelectedCode(ComboMajorType);
            ushort detailCode = SelectedCode(ComboDetail);

            try
            {
                var systems = DisModulationCatalog.GetSystems(majorCode, detailCode);
                foreach (var s in systems)
                    ComboSystem.Items.Add(new ComboBoxItem { Content = $"{s.Code} {s.Name}", Tag = s.Code });

                if (ComboSystem.Items.Count == 0) PopulateSystems_Fallback();
            }
            catch
            {
                PopulateSystems_Fallback();
            }

            ComboSystem.IsEnabled = ComboSystem.Items.Count > 0;
            ComboSystem.SelectedIndex = ComboSystem.IsEnabled ? 0 : -1;
        }

        private static ushort SelectedCode(ComboBox cb)
        {
            if (cb?.SelectedItem is ComboBoxItem item && item.Tag is ushort code)
                return code;

            if (cb?.SelectedItem is ComboBoxItem item2 && item2.Content is string s)
            {
                var first = s.Split(new[] { ' ' }, 2)[0];
                if (ushort.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
            }
            return 0;
        }

        private void PreselectModulationFromSettings(PduSettings? s)
        {
            if (s == null) return;

            // Major
            if (TrySelectByCode(ComboMajorType, s.ModulationMajor))
            {
                PopulateDetailsFromMajor(); // refresh details for this major

                // Detail
                if (TrySelectByCode(ComboDetail, s.ModulationDetail))
                {
                    PopulateSystemsFromDetail(); // refresh systems for this detail

                    // System
                    TrySelectByCode(ComboSystem, s.ModulationSystem);
                }
                else
                {
                    PopulateSystemsFromDetail();
                }
            }
        }

        private static bool TrySelectByCode(ComboBox cb, ushort code)
        {
            for (int i = 0; i < cb.Items.Count; i++)
            {
                if (cb.Items[i] is ComboBoxItem it && it.Tag is ushort tag && tag == code)
                {
                    cb.SelectedIndex = i;
                    return true;
                }
                // Fallback: parse "NN name"
                if (cb.Items[i] is ComboBoxItem it2 && it2.Content is string s)
                {
                    var first = s.Split(new[] { ' ' }, 2)[0];
                    if (ushort.TryParse(first, out var parsed) && parsed == code)
                    {
                        cb.SelectedIndex = i;
                        return true;
                    }
                }
            }
            return false;
        }

        // -------- Fallback data so the UI never shows blank lists --------
        private void PopulateMajors_Fallback()
        {
            ComboMajorType.Items.Clear();
            foreach (var m in _fallbackMajors)
                ComboMajorType.Items.Add(new ComboBoxItem { Content = $"{m.code} {m.name}", Tag = m.code });
        }

        private void PopulateDetails_Fallback(ushort major)
        {
            ComboDetail.Items.Clear();
            if (_fallbackDetails.TryGetValue(major, out var list))
            {
                foreach (var d in list)
                    ComboDetail.Items.Add(new ComboBoxItem { Content = $"{d.code} {d.name}", Tag = d.code });
            }
        }

        private void PopulateSystems_Fallback()
        {
            ComboSystem.Items.Clear();
            foreach (var s in _fallbackSystems)
                ComboSystem.Items.Add(new ComboBoxItem { Content = $"{s.code} {s.name}", Tag = s.code });
        }

        // Fallback majors
        private static readonly (ushort code, string name)[] _fallbackMajors = new[]
        {
            ( (ushort)0, "Other" ),
            ( (ushort)1, "Amplitude" ),
            ( (ushort)2, "Amplitude and Angle" ),
            ( (ushort)3, "Angle" ),
            ( (ushort)4, "Combination" ),
            ( (ushort)5, "Pulse" ),
            ( (ushort)6, "Unmodulated" ),
            ( (ushort)7, "Carrier Phase Shift Modulation (CPSM)" )
        };

        // Fallback details by major
        private static readonly Dictionary<ushort, (ushort code, string name)[]> _fallbackDetails =
            new()
            {
                [0] = new[] { ((ushort)0, "Other") },

                [1] = new[]
                {
                    ( (ushort)0,  "Other"),
                    ( (ushort)1,  "AFSK (Audio Frequency Key Shifting)"),
                    ( (ushort)2,  "AM (Amplitude Modulation)"),
                    ( (ushort)3,  "CW (Continuous Wave)"),
                    ( (ushort)4,  "DSB (Double Sideband)"),
                    ( (ushort)5,  "ISB (Independant Sideband)"),
                    ( (ushort)6,  "LSB (Lower Sideband)"),
                    ( (ushort)7,  "SSB-Full (Single Sideband Full)"),
                    ( (ushort)8,  "SSB-Reduc (Single Sideband Reduced)"),
                    ( (ushort)9,  "USB (Upper Sideband)"),
                    ( (ushort)10, "VSB (Vestigial Sideband)")
                },

                [2] = new[]
                {
                    ( (ushort)0, "Other"),
                    ( (ushort)1, "Amplitude and Angle")
                },

                [3] = new[]
                {
                    ( (ushort)0, "Other"),
                    ( (ushort)1, "FM (Frequency Modulation)"),
                    ( (ushort)2, "FSK Frequency Shift Keying)"),
                    ( (ushort)3, "PM (Phase Modulation)")
                },

                [4] = new[]
                {
                    ( (ushort)0, "Other"),
                    ( (ushort)1, "Amplitude-Angle-Pulse")
                },

                [5] = new[]
                {
                    ( (ushort)0, "Other"),
                    ( (ushort)1, "Pulse"),
                    ( (ushort)2, "X Band TACAN Pulse"),
                    ( (ushort)3, "Y Band TCAN Pulse")
                },

                [6] = new[]
                {
                    ( (ushort)0, "Other"),
                    ( (ushort)1, "CW (Continuous Wave)")
                },

                [7] = new[]
                {
                    ( (ushort)0, "Other")
                }
            };

        // Fallback systems (shared for all M/D)
        private static readonly (ushort code, string name)[] _fallbackSystems = new[]
        {
            ( (ushort)0,  "Other"),
            ( (ushort)1,  "Generic Radio or Simple Intercom"),
            ( (ushort)2,  "HAVE QUICK"),
            ( (ushort)3,  "HAVE QUICK II"),
            ( (ushort)4,  "HAVE QUICK IIA"),
            ( (ushort)5,  "Single Channel Ground-Air Radio System (SINCGARS)"),
            ( (ushort)6,  "CCTT SINCGARS"),
            ( (ushort)7,  "EPLRS (Enhanced Position Location Reporting System"),
            ( (ushort)8,  "JTIDS/MIDS"),
            ( (ushort)9,  "Link 11"),
            ( (ushort)10, "Link 11B"),
            ( (ushort)11, "L-Band SATCOM"),
            ( (ushort)12, "Enhanced SINCGARS 7.3"),
            ( (ushort)13, "Navigation Aid")
        };

        // ========= PUBLIC HELPER FOR SHELL =========
        /// <summary>
        /// Applies whatever is currently on-screen into _pduService.Settings,
        /// then raises the existing Applied event so the shell can persist.
        /// </summary>
        public void ApplyAndRaise()
        {
            // Reuse the same validation and event flow as the Apply button.
            BtnApply_Click(this, new RoutedEventArgs());
        }
    }
}
