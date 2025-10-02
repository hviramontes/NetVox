using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NetVox.UI.Views
{
    public partial class RadioProfileView : UserControl
    {
        // Profile -> (MinHz, MaxHz)
        private readonly Dictionary<string, (double MinHz, double MaxHz)> _profiles =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "Harris AN/PRC-152", (30e6, 512e6) },
                { "Harris AN/PRC-153", (136e6, 520e6) },
                { "AN/PRC-117F",       (30e6, 512e6) },
                { "AN/PRC-117G",       (30e6, 2e9)   },
                { "AN/PRC-119",        (30e6, 87.975e6) },
                { "AN/PRC-148",        (30e6, 512e6) },
                { "PRC-25",            (33e6, 75.95e6) },
                { "9101A",             (33e6, 88e6) }
            };

        public event Action<string>? Applied;

        public RadioProfileView()
        {
            InitializeComponent();

            // Populate profiles
            var names = _profiles.Keys.ToList();
            names.Sort(StringComparer.OrdinalIgnoreCase);
            ComboProfile.ItemsSource = names;

            // Default: Harris AN/PRC-152
            var defaultIndex = names.IndexOf("Harris AN/PRC-152");
            ComboProfile.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;

            // Wire events
            ComboProfile.SelectionChanged += (_, __) => UpdateMinMax();
            BtnApply.Click += (_, __) =>
            {
                Applied?.Invoke($"Radio profile set to {ComboProfile.SelectedItem}  [{TxtMinFreq.Text} – {TxtMaxFreq.Text}]");
            };

            // Initial fill
            UpdateMinMax();
        }

        private void UpdateMinMax()
        {
            if (ComboProfile.SelectedItem is not string key) return;
            if (!_profiles.TryGetValue(key, out var range)) return;

            TxtMinFreq.Text = FormatFrequency(range.MinHz);
            TxtMaxFreq.Text = FormatFrequency(range.MaxHz);
        }

        private static string FormatFrequency(double hz)
        {
            if (hz >= 1_000_000_000) return $"{hz / 1_000_000_000:0.###} GHz";
            if (hz >= 1_000_000) return $"{hz / 1_000_000:0.000} MHz";
            if (hz >= 1_000) return $"{hz / 1_000:0.###} kHz";
            return $"{hz:0} Hz";
        }
    }
}
