using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using NetVox.UI.Services; // NotificationService

namespace NetVox.UI
{
    public partial class RadioInterfaceWindow : Window
    {
        private enum RadioState { Idle, Receiving, Transmitting, Muted }

        private RadioState _state = RadioState.Idle;
        private bool _armed = false;

        // Events the host can subscribe to
        public event Action PttPressed;
        public event Action PttReleased;
        public event Action ChannelUpClicked;
        public event Action ChannelDownClicked;
        public event Action<bool> MuteToggled; // bool = isMuted
        public event Action StopRequested;

        public RadioInterfaceWindow()
        {
            InitializeComponent();

            NotificationService.RegisterHost(RadioToast);
            NotificationService.Show("Radio UI notifications ready", Controls.ToastKind.Info);


            // Wire UI -> events
            BtnTransmit.PreviewMouseLeftButtonDown += (_, __) =>
            {
                if (!_armed) return;
                SetTransmitting(true);
                PttPressed?.Invoke();
            };

            BtnTransmit.PreviewMouseLeftButtonUp += (_, __) =>
            {
                if (!_armed) return;
                SetTransmitting(false);
                PttReleased?.Invoke();
            };

            BtnChannelUp.Click += (_, __) => ChannelUpClicked?.Invoke();
            BtnChannelDown.Click += (_, __) => ChannelDownClicked?.Invoke();

            BtnMute.Checked += (_, __) =>
            {
                SetMute(true);
                MuteToggled?.Invoke(true);
            };
            BtnMute.Unchecked += (_, __) =>
            {
                SetMute(false);
                MuteToggled?.Invoke(false);
            };

            BtnStop.Click += (_, __) => StopRequested?.Invoke();

            UpdateVisuals();
        }

        // ---------- Public API for host window ----------

        public void SetArmed(bool armed)
        {
            _armed = armed;
            BtnTransmit.IsEnabled = _armed;
            if (!_armed && _state == RadioState.Transmitting)
                _state = RadioState.Idle;
            UpdateVisuals();
        }

        public void SetChannel(string displayText)
        {
            TxtChannel.Text = displayText ?? "";
        }

        /// <summary>
        /// Set whether we are receiving. When true, the green RX banner shows.
        /// The banner text itself is set via SetReceivingChannel.
        /// </summary>
        public void SetReceiving(bool receiving)
        {
            if (receiving && _state != RadioState.Transmitting && _state != RadioState.Muted)
                _state = RadioState.Receiving;
            else if (!receiving && _state == RadioState.Receiving)
                _state = RadioState.Idle;

            RxBanner.Visibility = receiving ? Visibility.Visible : Visibility.Collapsed;
            UpdateVisuals();
        }

        /// <summary>
        /// Update the text shown in the RX banner, e.g., "Channel 3: PLT NET".
        /// Safe to call anytime; it only displays while receiving.
        /// </summary>
        public void SetReceivingChannel(string displayText)
        {
            TxtRxChannel.Text = displayText ?? "";
        }

        public void SetTransmitting(bool tx)
        {
            SetTransmittingInternal(tx);
        }

        public void SetMute(bool isMuted)
        {
            BtnMute.IsChecked = isMuted;
            SetMuted(isMuted);
        }

        // ---------- Internal helpers ----------

        private void SetTransmittingInternal(bool tx)
        {
            if (!_armed) return;
            _state = tx ? RadioState.Transmitting : RadioState.Idle;

            // Hide RX banner while transmitting
            if (tx) RxBanner.Visibility = Visibility.Collapsed;

            UpdateVisuals();
        }

        private void SetMuted(bool muted)
        {
            _state = muted ? RadioState.Muted : RadioState.Idle;

            // Hide RX banner while muted
            RxBanner.Visibility = muted ? Visibility.Collapsed : RxBanner.Visibility;

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            // Colors per state
            Color bg, border;
            string text;

            switch (_state)
            {
                case RadioState.Transmitting:
                    bg = (Color)ColorConverter.ConvertFromString("#8F1F1F");   // red-ish
                    border = (Color)ColorConverter.ConvertFromString("#B34A4A");
                    text = "TRANSMITTING";
                    break;
                case RadioState.Receiving:
                    bg = (Color)ColorConverter.ConvertFromString("#1F5F2A");   // green-ish
                    border = (Color)ColorConverter.ConvertFromString("#3D8C50");
                    text = "RECEIVING";
                    break;
                case RadioState.Muted:
                    bg = (Color)ColorConverter.ConvertFromString("#6A4C1F");   // amber-ish
                    border = (Color)ColorConverter.ConvertFromString("#8C642B");
                    text = "MUTED";
                    break;
                default:
                    bg = (Color)ColorConverter.ConvertFromString("#1F252B");   // idle
                    border = (Color)ColorConverter.ConvertFromString("#2D3742");
                    text = "READY";
                    break;
            }

            StatusPanel.Background = new SolidColorBrush(bg);
            StatusPanel.BorderBrush = new SolidColorBrush(border);
            TxtStatus.Text = text;
        }
    }
}
