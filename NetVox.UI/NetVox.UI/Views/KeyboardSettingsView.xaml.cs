using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace NetVox.UI.Views
{
    public partial class KeyboardSettingsView : UserControl
    {
        private readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "NetVox", "keybinds.json");

        public class KeybindConfig
        {
            public string PttKey { get; set; }
            public string MuteKey { get; set; }
            public string ChannelUpKey { get; set; }
            public string ChannelDownKey { get; set; }
        }

        public KeyboardSettingsView()
        {
            InitializeComponent();

            BtnSaveKeys.Click += BtnSaveKeys_Click;

            if (File.Exists(ConfigPath))
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

            // Ensure the directory exists
            string dir = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

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
