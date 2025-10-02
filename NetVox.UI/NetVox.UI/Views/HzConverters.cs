using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NetVox.UI.Views
{
    /// <summary>
    /// Bind Hz to a string like "32.000 MHz" (editable).
    /// ConvertBack accepts "32", "32.5", "32 MHz", "32000000", etc., and returns Hz.
    /// </summary>
    public sealed class HzToMHzConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double hz = ToDouble(value);
            double mhz = hz / 1_000_000.0;
            return mhz.ToString("0.000", CultureInfo.InvariantCulture) + " MHz";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = (value?.ToString() ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text))
                return DependencyProperty.UnsetValue;

            // Accept inputs like "32", "32.5", "32 mhz", "32000000", "32,000,000"
            text = text.Replace(",", "").Trim();
            bool hasUnitMHz = text.EndsWith("mhz");
            bool hasUnitKHz = text.EndsWith("khz");
            bool hasUnitHz = text.EndsWith("hz") && !hasUnitMHz && !hasUnitKHz;

            if (hasUnitMHz || hasUnitKHz || hasUnitHz)
                text = text.Replace("mhz", "").Replace("khz", "").Replace("hz", "").Trim();

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                return DependencyProperty.UnsetValue;

            double hz = hasUnitMHz || (!hasUnitKHz && !hasUnitHz)   // default to MHz if no unit
                        ? num * 1_000_000.0
                        : hasUnitKHz
                          ? num * 1_000.0
                          : num;

            long rounded = (long)Math.Round(hz);

            // Coerce to target numeric type expected by the binding
            if (targetType == typeof(int))
            {
                if (rounded > int.MaxValue) rounded = int.MaxValue;
                if (rounded < int.MinValue) rounded = int.MinValue;
                return (int)rounded;
            }
            return rounded;
        }

        private static double ToDouble(object v)
        {
            if (v is null) return 0;
            if (v is long l) return l;
            if (v is int i) return i;
            if (double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            return 0;
        }
    }

    /// <summary>
    /// Bind Hz to a string like "44.000 kHz" (editable).
    /// ConvertBack accepts "44", "44.5", "44 kHz", "44000", etc., and returns Hz.
    /// </summary>
    public sealed class HzToKHzConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double hz = ToDouble(value);
            double khz = hz / 1_000.0;
            return khz.ToString("0.000", CultureInfo.InvariantCulture) + " kHz";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = (value?.ToString() ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text))
                return DependencyProperty.UnsetValue;

            // Accept "44", "44.5", "44 khz", "44000", "44,000"
            text = text.Replace(",", "").Trim();
            bool hasUnitKHz = text.EndsWith("khz");
            bool hasUnitMHz = text.EndsWith("mhz");
            bool hasUnitHz = text.EndsWith("hz") && !hasUnitKHz && !hasUnitMHz;

            if (hasUnitKHz || hasUnitMHz || hasUnitHz)
                text = text.Replace("khz", "").Replace("mhz", "").Replace("hz", "").Trim();

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                return DependencyProperty.UnsetValue;

            double hz = hasUnitMHz
                        ? num * 1_000_000.0
                        : (hasUnitKHz || !hasUnitHz) // default to kHz if no unit
                          ? num * 1_000.0
                          : num;

            long rounded = (long)Math.Round(hz);

            if (targetType == typeof(int))
            {
                if (rounded > int.MaxValue) rounded = int.MaxValue;
                if (rounded < int.MinValue) rounded = int.MinValue;
                return (int)rounded;
            }
            return rounded;
        }

        private static double ToDouble(object v)
        {
            if (v is null) return 0;
            if (v is long l) return l;
            if (v is int i) return i;
            if (double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            return 0;
        }
    }
}
