namespace NuGetPackageConfigConverter
{
    public struct PackageConfigEntry
    {
        public PackageConfigEntry(string id, string version)
        {
            Id = id;
            Version = version ?? string.Empty;
        }

        public string Id { get; }

        public string Version { get; }
    }
}
