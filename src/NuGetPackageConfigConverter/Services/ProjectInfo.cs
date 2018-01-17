namespace NuGetPackageConfigConverter
{
    public class ProjectInfo
    {
        public ProjectInfo(string fullname, string name)
        {
            FullName = fullname;
            Name = name;
        }

        public string FullName { get; }

        public string Name { get; }
    }
}
