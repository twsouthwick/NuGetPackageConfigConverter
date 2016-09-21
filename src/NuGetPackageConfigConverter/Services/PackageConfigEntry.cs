using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NuGetPackageConfigConverter
{
    public struct PackageConfigEntry
    {
        public PackageConfigEntry(string id, string version)
        {
            Id = id;
            Version = version;
        }

        public string Id { get; }

        public string Version { get; }

        public static ICollection<PackageConfigEntry> ParseFile(string path)
        {
            try
            {
                return XDocument.Load(path).Descendants("package")
                    .Select(entry =>
                    {
                        var name = entry.Attribute("id")?.Value;
                        var version = entry.Attribute("version")?.Value;
                        var dev = entry.Attribute("developmentDependency")?.Value;

                        return new PackageConfigEntry(name, version);
                    })
                    .ToList().AsReadOnly();
            }
            catch (Exception)
            {
                return Array.Empty<PackageConfigEntry>();
            }
        }
    }
}
