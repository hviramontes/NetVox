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

        public Profile LoadProfile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Profile not found: {filePath}");

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Profile>(json)
                   ?? throw new InvalidOperationException("Failed to deserialize profile.");
        }

        public void SaveProfile(Profile profile, string filePath)
        {
            // Ensure the target folder exists
            var dir = Path.GetDirectoryName(filePath)
                      ?? throw new InvalidOperationException("Invalid file path.");
            Directory.CreateDirectory(dir);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(profile, options);
            File.WriteAllText(filePath, json);
        }
    }
}
