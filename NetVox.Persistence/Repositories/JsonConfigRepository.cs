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
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return Array.Empty<string>();

            return Directory.GetFiles(directory, $"*{Extension}");
        }

        public Profile LoadProfile(string fileName)
        {
            string fullPath = ResolvePath(fileName, ensureFolder: false);

            if (!File.Exists(fullPath))
                return new Profile { Channels = new List<ChannelConfig>() };

            var json = File.ReadAllText(fullPath);
            return JsonSerializer.Deserialize<Profile>(json)
                ?? new Profile { Channels = new List<ChannelConfig>() };
        }

        public void SaveProfile(Profile profile, string fileName)
        {
            string fullPath = ResolvePath(fileName, ensureFolder: true);

            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(fullPath, json);
        }

        /// <summary>
        /// Accepts either a full path (C:\...\something.json) or a simple name (default.json).
        /// For simple names, stores under Documents\NetVox\.
        /// Ensures .json extension and optionally creates the folder.
        /// </summary>
        private static string ResolvePath(string fileName, bool ensureFolder)
        {
            string path = fileName ?? string.Empty;

            // Ensure extension
            if (!Path.HasExtension(path))
                path += Extension;

            if (Path.IsPathRooted(path))
            {
                if (ensureFolder)
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                }
                return path;
            }

            // Relative name → default folder
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NetVox");

            if (ensureFolder)
                Directory.CreateDirectory(folder);

            return Path.Combine(folder, path);
        }
    }
}
