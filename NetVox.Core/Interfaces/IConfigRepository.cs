using NetVox.Core.Models;
using System.Collections.Generic;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace NetVox.Core.Interfaces
{
    public interface IConfigRepository
    {
        /// <summary>Returns all JSON profile files in the given folder.</summary>
        IEnumerable<string> GetAvailableProfiles(string directory);

        /// <summary>Loads a profile (list of channels, network settings, etc.).</summary>
        Profile LoadProfile(string filePath);

        /// <summary>Saves a profile back to disk.</summary>
        void SaveProfile(Profile profile, string filePath);
    }
}
