using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using NetVox.Core.Models;


namespace NetVox.UI.Views
{
    public partial class KeyboardSettingsView : UserControl
    {
        private readonly string ConfigFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "NetVox");

        private readonly string ConfigPath;

        public KeyboardSettingsView()
        {
            InitializeComponent();

            ConfigPath = Path.Combine(ConfigFolder, "keybinds.json");

            // Ensure folder and file exist
            if (!Directory.Exists(ConfigFolder))
                Directory.CreateDirectory(ConfigFolder);

            if (!File.Exists(ConfigPath))
            {
                var empty = new KeybindConfig();
                var json = JsonSerializer.Serialize(empty, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }

            BtnSaveKeys.Click += BtnSaveKeys_Click;

            LoadFromDisk();
        }

        private void BtnSaveKeys_Click(object sender, RoutedEventArgs e)
        {
            var config = new KeybindConfig
            {
                PttKey = TxtPttKey.Text,
                MuteKey = TxtMuteKey.Text,
                ChannelUpKey = TxtChannelUpKey.Text,
                ChannelDownKey = TxtChannelDownKey.Text
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);

            MessageBox.Show("Keybindings saved.");
        }

        private void LoadFromDisk()
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<KeybindConfig>(json);

                if (config != null)
                {
                    TxtPttKey.Text = config.PttKey;
                    TxtMuteKey.Text = config.MuteKey;
                    TxtChannelUpKey.Text = config.ChannelUpKey;
                    TxtChannelDownKey.Text = config.ChannelDownKey;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load keybinds:\n{ex.Message}");
            }
        }
    }
}
