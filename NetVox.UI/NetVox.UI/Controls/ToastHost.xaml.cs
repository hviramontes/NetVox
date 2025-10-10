using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Timer = System.Timers.Timer;
using System.Windows.Data;
using System.Globalization;



namespace NetVox.UI.Controls
{
    public partial class ToastHost : UserControl
    {
        // 5-second lifetime per your spec
        private const int AutoDismissMs = 5000;
        // De-dupe window to avoid spam bursts
        private static readonly TimeSpan DedupeWindow = TimeSpan.FromSeconds(2);
        // Limit on-screen toasts
        private const int MaxToasts = 3;

        public ObservableCollection<ToastItem> Toasts { get; } = new();

        // Per-message last-seen times for de-dupe
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _lastShown
            = new();

        public ToastHost()
        {
            InitializeComponent();
            DataContext = this;
            IsHitTestVisible = false; // clicks pass through
        }

        public void ShowToast(string message, ToastKind kind = ToastKind.Info)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var now = DateTime.UtcNow;
            if (_lastShown.TryGetValue(message, out var last) && (now - last) < DedupeWindow)
                return;
            _lastShown[message] = now;

            Dispatcher.Invoke(() =>
            {
                // keep to at most MaxToasts by removing oldest
                while (Toasts.Count >= MaxToasts)
                {
                    var oldest = Toasts.OrderBy(t => t.CreatedUtc).FirstOrDefault();
                    if (oldest != null) Toasts.Remove(oldest);
                    else break;
                }

                var item = new ToastItem
                {
                    Id = Guid.NewGuid(),
                    Message = message.Trim(),
                    Kind = kind,
                    CreatedUtc = now,
                    Opacity = 0
                };

                Toasts.Add(item);
                // simple fade in
                AnimateOpacity(item, to: 1.0, durationMs: 150);

                // auto-dismiss timer
                var timer = new Timer(AutoDismissMs) { AutoReset = false };
                timer.Elapsed += (_, __) =>
                {
                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (Toasts.Contains(item))
                            {
                                // fade out then remove
                                AnimateOpacity(item, to: 0.0, durationMs: 200, onComplete: () =>
                                {
                                    Toasts.Remove(item);
                                });
                            }
                        });
                    }
                    finally { timer.Dispose(); }
                };
                timer.Start();
            });
        }

        private void AnimateOpacity(ToastItem item, double to, int durationMs, Action? onComplete = null)
        {
            // Minimal animation without Storyboards: step timer
            var steps = Math.Max(1, durationMs / 16);
            var from = item.Opacity;
            var delta = (to - from) / steps;
            var i = 0;
            var t = new Timer(16) { AutoReset = true };
            t.Elapsed += (_, __) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (i++ >= steps)
                    {
                        item.Opacity = to;
                        t.Stop();
                        t.Dispose();
                        onComplete?.Invoke();
                    }
                    else
                    {
                        item.Opacity = item.Opacity + delta;
                    }
                });
            };
            t.Start();
        }
    }

    public enum ToastKind { Info, Warning, Error }

    public sealed class ToastItem
    {
        public Guid Id { get; set; }
        public string Message { get; set; } = "";
        public ToastKind Kind { get; set; } = ToastKind.Info;
        public DateTime CreatedUtc { get; set; }
        public double Opacity { get; set; } = 1.0;
    }

    // Converter for the little severity dot color.
    // We keep it here to avoid extra files. Uses your theme brushes if present.
    // Converter for the little severity dot color.
    // Single-value IValueConverter to match the XAML Binding.
    public sealed class ToastKindToBrushConverter : IValueConverter
    {
        public static readonly ToastKindToBrushConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var kind = value is ToastKind tk ? tk : ToastKind.Info;

            // Try to use your amber/navy theme resources for consistency
            Brush amber500 = TryFindBrush("Amber500") ?? Brushes.Gold;
            Brush amber700 = TryFindBrush("Amber700") ?? Brushes.DarkGoldenrod;
            Brush navy500 = TryFindBrush("Navy500") ?? Brushes.SteelBlue;
            Brush redBrush = new SolidColorBrush(Color.FromRgb(200, 64, 64));

            return kind switch
            {
                ToastKind.Info => navy500,
                ToastKind.Warning => amber700,
                ToastKind.Error => redBrush,
                _ => amber500
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static Brush? TryFindBrush(string key)
        {
            var app = Application.Current;
            if (app != null && app.Resources.Contains(key))
            {
                if (app.Resources[key] is Brush b) return b;
            }
            return null;
        }
    }

}
