using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace NetVox.UI.Views
{
    public partial class RadioProfileView : UserControl
    {
        private readonly string ProfileFolder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NetVox", "Profiles");

        public class RadioProfile
        {
            public string Brand { get; set; }
            public string Model { get; set; }
            public string Display => $"{Brand} {Model}";
        }

        public RadioProfileView()
        {
            InitializeComponent();

            if (!Directory.Exists(ProfileFolder))
                Directory.CreateDirectory(ProfileFolder);

            LoadProfiles();

            ComboProfile.SelectionChanged += ComboProfile_SelectionChanged;

            BtnImport.Click += (_, _) =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Radio Profile",
                    Filter = "JSON Files (*.json)|*.json"
                };

                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        var json = File.ReadAllText(dlg.FileName);
                        var profile = JsonSerializer.Deserialize<RadioProfile>(json);

                        if (profile != null)
                        {
                            ComboProfile.Items.Add(profile);
                            ComboProfile.SelectedItem = profile;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Import failed:\n{ex.Message}");
                    }
                }
            };

            BtnExport.Click += (_, _) =>
            {
                if (ComboProfile.SelectedItem is RadioProfile selected)
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "Export Radio Profile",
                        Filter = "JSON Files (*.json)|*.json",
                        FileName = $"{selected.Model}.json",
                        InitialDirectory = ProfileFolder
                    };

                    if (dlg.ShowDialog() == true)
                    {
                        try
                        {
                            var json = JsonSerializer.Serialize(selected, new JsonSerializerOptions { WriteIndented = true });
                            File.WriteAllText(dlg.FileName, json);
                            MessageBox.Show("Profile exported.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Export failed:\n{ex.Message}");
                        }
                    }
                }
            };

            BtnDelete.Click += (_, _) =>
            {
                if (ComboProfile.SelectedItem is RadioProfile selected)
                {
                    if (MessageBox.Show($"Delete profile '{selected.Display}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        ComboProfile.Items.Remove(selected);
                        TxtProfileName.Clear();
                    }
                }
            };
        }

        private void LoadProfiles()
        {
            var profiles = new List<RadioProfile>
            {
                new RadioProfile { Brand = "Harris", Model = "AN/PRC-152" },
                new RadioProfile { Brand = "Motorola", Model = "AN/PRC-153" },
                new RadioProfile { Brand = "Harris", Model = "AN/PRC-117F" },
                new RadioProfile { Brand = "Harris", Model = "AN/PRC-117G" },
                new RadioProfile { Brand = "Harris", Model = "AN/PRC-119" },
                new RadioProfile { Brand = "Harris", Model = "AN/PRC-148" },
                new RadioProfile { Brand = "Harris", Model = "AN/PRC-25" },
                new RadioProfile { Brand = "Tadiran", Model = "CNR-9101A" },
            };

            ComboProfile.ItemsSource = profiles;
        }

        private void ComboProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboProfile.SelectedItem is RadioProfile selected)
            {
                TxtProfileName.Text = selected.Model;
            }
        }
    }
}
