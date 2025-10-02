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

        // Default hotkeys
        private const string DefaultPtt = "F9";
        private const string DefaultMute = "M";
        private const string DefaultUp = "PageUp";
        private const string DefaultDown = "PageDown";

        public KeyboardSettingsView()
        {
            InitializeComponent();

            ConfigPath = Path.Combine(ConfigFolder, "keybinds.json");

            // Ensure folder exists
            if (!Directory.Exists(ConfigFolder))
                Directory.CreateDirectory(ConfigFolder);

            // Ensure file exists with defaults
            if (!File.Exists(ConfigPath))
            {
                WriteConfig(new KeybindConfig
                {
                    PttKey = DefaultPtt,
                    MuteKey = DefaultMute,
                    ChannelUpKey = DefaultUp,
                    ChannelDownKey = DefaultDown
                });
            }

            BtnSaveKeys.Click += BtnSaveKeys_Click;

            LoadFromDisk();
        }

        private void BtnSaveKeys_Click(object sender, RoutedEventArgs e)
        {
            // Basic validation to avoid null/blank keys
            if (string.IsNullOrWhiteSpace(TxtPttKey.Text) ||
                string.IsNullOrWhiteSpace(TxtMuteKey.Text) ||
                string.IsNullOrWhiteSpace(TxtChannelUpKey.Text) ||
                string.IsNullOrWhiteSpace(TxtChannelDownKey.Text))
            {
                MessageBox.Show("Set all four keys before saving.");
                return;
            }

            var config = new KeybindConfig
            {
                PttKey = TxtPttKey.Text.Trim(),
                MuteKey = TxtMuteKey.Text.Trim(),
                ChannelUpKey = TxtChannelUpKey.Text.Trim(),
                ChannelDownKey = TxtChannelDownKey.Text.Trim()
            };

            WriteConfig(config);
            MessageBox.Show("Keybindings saved.");
        }

        private void LoadFromDisk()
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<KeybindConfig>(json) ?? new KeybindConfig();

                // If any are missing, backfill with defaults and persist once
                bool changed = false;

                if (string.IsNullOrWhiteSpace(config.PttKey)) { config.PttKey = DefaultPtt; changed = true; }
                if (string.IsNullOrWhiteSpace(config.MuteKey)) { config.MuteKey = DefaultMute; changed = true; }
                if (string.IsNullOrWhiteSpace(config.ChannelUpKey)) { config.ChannelUpKey = DefaultUp; changed = true; }
                if (string.IsNullOrWhiteSpace(config.ChannelDownKey)) { config.ChannelDownKey = DefaultDown; changed = true; }

                if (changed) WriteConfig(config);

                TxtPttKey.Text = config.PttKey;
                TxtMuteKey.Text = config.MuteKey;
                TxtChannelUpKey.Text = config.ChannelUpKey;
                TxtChannelDownKey.Text = config.ChannelDownKey;
            }
            catch (Exception ex)
            {
                // Auto-heal: if the file is corrupted, overwrite with defaults
                try
                {
                    WriteConfig(new KeybindConfig
                    {
                        PttKey = DefaultPtt,
                        MuteKey = DefaultMute,
                        ChannelUpKey = DefaultUp,
                        ChannelDownKey = DefaultDown
                    });
                    TxtPttKey.Text = DefaultPtt;
                    TxtMuteKey.Text = DefaultMute;
                    TxtChannelUpKey.Text = DefaultUp;
                    TxtChannelDownKey.Text = DefaultDown;
                    MessageBox.Show("Keybinds were corrupted and have been reset to defaults.");
                }
                catch
                {
                    MessageBox.Show($"Failed to load keybinds:\n{ex.Message}");
                }
            }
        }

        private void WriteConfig(KeybindConfig cfg)
        {
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
    }
}
