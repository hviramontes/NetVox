using System;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;
using RadioChannel = NetVox.Core.Models.ChannelConfig;


namespace NetVox.Core.Services
{
    public class RadioSessionManager
    {
        private readonly IRadioService _radioService;
        private readonly IPduService _pduService;
        private readonly INetworkService _networkService;

        public bool IsMuted { get; private set; } = false;
        public bool IsTransmitting { get; private set; } = false;
        public int CurrentChannelIndex { get; private set; } = 0;
        public Profile ActiveProfile { get; private set; }

        public event Action<string> OnStatusChanged;

        public RadioSessionManager(
            IRadioService radio,
            IPduService pdu,
            INetworkService network,
            Profile profile)
        {
            _radioService = radio;
            _pduService = pdu;
            _networkService = network;
            ActiveProfile = profile;

            ApplyCurrentChannelConfig();
        }

        public void BeginTransmit()
        {
            if (!IsMuted && !IsTransmitting)
            {
                _radioService.BeginTransmit();
                IsTransmitting = true;
                OnStatusChanged?.Invoke("Transmitting");
            }
        }

        public void EndTransmit()
        {
            if (IsTransmitting)
            {
                _radioService.EndTransmit();
                IsTransmitting = false;
                OnStatusChanged?.Invoke("Idle");
            }
        }

        public void ToggleMute()
        {
            IsMuted = !IsMuted;
            OnStatusChanged?.Invoke(IsMuted ? "Muted" : "Unmuted");

            if (IsMuted && IsTransmitting)
            {
                EndTransmit();
            }
        }

        public void ChannelUp()
        {
            if (ActiveProfile?.Channels?.Count > 1)
            {
                CurrentChannelIndex = (CurrentChannelIndex + 1) % ActiveProfile.Channels.Count;
                ApplyCurrentChannelConfig();
                OnStatusChanged?.Invoke($"Channel: {GetCurrentChannel()?.Name}");
            }
        }

        public void ChannelDown()
        {
            if (ActiveProfile?.Channels?.Count > 1)
            {
                CurrentChannelIndex = (CurrentChannelIndex - 1 + ActiveProfile.Channels.Count) % ActiveProfile.Channels.Count;
                ApplyCurrentChannelConfig();
                OnStatusChanged?.Invoke($"Channel: {GetCurrentChannel()?.Name}");
            }
        }

        public RadioChannel GetCurrentChannel()
        {
            if (ActiveProfile?.Channels == null || ActiveProfile.Channels.Count == 0)
                return null;

            return ActiveProfile.Channels[CurrentChannelIndex];
        }

        private void ApplyCurrentChannelConfig()
        {
            var channel = GetCurrentChannel();
            if (channel == null) return;

            _pduService.Settings = new PduSettings
            {
                Version = DisVersion.V6,
                Codec = CodecType.Pcm16
            };

            _networkService.CurrentConfig.FrequencyHz = channel.Frequency;
        }
    }
}
