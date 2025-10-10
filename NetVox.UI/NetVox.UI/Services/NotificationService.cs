using System;
using System.Windows;
using System.Windows.Threading;
using NetVox.UI.Controls;

namespace NetVox.UI.Services
{
    public static class NotificationService
    {
        // We keep a weak ref so we don't pin windows in memory.
        private static WeakReference<ToastHost>? _currentHost;

        /// <summary>Call this in each window's OnLoaded to set the active host.</summary>
        public static void RegisterHost(ToastHost host)
        {
            _currentHost = new WeakReference<ToastHost>(host);
        }

        /// <summary>Show a toast from anywhere in UI code.</summary>
        public static void Show(string message, ToastKind kind = ToastKind.Info)
        {
            try
            {
                // Prefer current app dispatcher to stay thread-safe
                var disp = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                disp.InvokeAsync(() =>
                {
                    if (_currentHost != null && _currentHost.TryGetTarget(out var host) && host != null)
                    {
                        host.ShowToast(message, kind);
                    }
                    else
                    {
                        // Fallback: no host registered; do nothing quietly.
                        // If you want, write to Debug here.
                    }
                });
            }
            catch
            {
                // Last-resort: swallow, because we never want error reporting to crash the app.
            }
        }
    }
}
