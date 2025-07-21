using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NetVox.Core.Models;

namespace NetVox.Core.Utils
{
    public static class CnrSimImporter
    {
        public static Profile LoadFromCnrSimXml(string xmlPath)
        {
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException("CNR-Sim config file not found", xmlPath);

            var doc = XDocument.Load(xmlPath);

            // Navigate to <presets> under <configurationset>
            var presets = doc.Descendants("presets")
                             .Elements("preset");

            var channels = presets.Select((preset, index) =>
            {
                var nameAttr = preset.Attribute("name")?.Value;
                var freqAttr = preset.Attribute("transmitfrequency")?.Value;

                if (!long.TryParse(freqAttr, out long frequencyHz))
                    frequencyHz = 30000000 + index * 25000;

                return new ChannelConfig
                {
                    ChannelNumber = index + 1,
                    FrequencyHz = frequencyHz,
                    BandwidthHz = 25000, // default
                    Name = nameAttr ?? $"CHAN {index + 1}"
                };
            }).ToList();

            return new Profile { Channels = channels };
        }
    }
}
