namespace NuGetPackageConfigConverter
{
    public struct PackageConfigEntry
    {
        public PackageConfigEntry(string id, string version)
        {
            Id = id;
            Version = version??"";
        }

        public string Id { get; }

        public string Version { get; } 
    }
}
