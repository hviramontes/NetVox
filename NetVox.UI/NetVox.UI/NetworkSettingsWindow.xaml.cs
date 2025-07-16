using System;
using System.Windows;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;

namespace NetVox.UI
{
    public partial class NetworkSettingsWindow : Window
    {
        private readonly INetworkService _networkService;

        public NetworkSettingsWindow(INetworkService networkService)
        {
            InitializeComponent();
            _networkService = networkService;

            Loaded += NetworkSettingsWindow_Loaded;
            BtnOK.Click += BtnOK_Click;
            BtnCancel.Click += (_, _) => Close();
        }

        private async void NetworkSettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var ips = await _networkService.GetAvailableLocalIPsAsync();
            ComboLocalIP.ItemsSource = ips;
            ComboLocalIP.SelectedItem = _networkService.CurrentConfig.LocalIPAddress;

            TxtDestinationIP.Text = _networkService.CurrentConfig.DestinationIPAddress;

            ComboMode.ItemsSource = Enum.GetValues(typeof(NetworkMode));
            ComboMode.SelectedItem = _networkService.CurrentConfig.Mode;
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (ComboLocalIP.SelectedItem is string ip)
                _networkService.CurrentConfig.LocalIPAddress = ip;

            _networkService.CurrentConfig.DestinationIPAddress = TxtDestinationIP.Text;

            if (ComboMode.SelectedItem is NetworkMode mode)
                _networkService.CurrentConfig.Mode = mode;

            Close();
        }
    }
}
