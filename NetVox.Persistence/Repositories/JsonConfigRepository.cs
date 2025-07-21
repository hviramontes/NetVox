using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NetVox.Core.Interfaces;
using NetVox.Core.Models;

namespace NetVox.Persistence.Repositories
{
    public class JsonConfigRepository : IConfigRepository
    {
        private const string Extension = ".json";

        public IEnumerable<string> GetAvailableProfiles(string directory)
        {
            if (!Directory.Exists(directory))
                return Array.Empty<string>();
            return Directory.GetFiles(directory, $"*{Extension}");
        }

        public Profile LoadProfile(string fileName)
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NetVox");

            var fullPath = Path.Combine(folder, fileName);

            if (!File.Exists(fullPath))
                return new Profile { Channels = new List<ChannelConfig>() };

            var json = File.ReadAllText(fullPath);
            return JsonSerializer.Deserialize<Profile>(json) ?? new Profile();
        }


        public void SaveProfile(Profile profile, string fileName)
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NetVox");

            Directory.CreateDirectory(folder); // ensure folder exists

            var fullPath = Path.Combine(folder, fileName);

            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(fullPath, json);
        }

    }
}
