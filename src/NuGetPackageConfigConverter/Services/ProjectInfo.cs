namespace NuGetPackageConfigConverter
{
    public class ProjectInfo
    {
        public string FullName { get; }
        public string Name { get; }

        public ProjectInfo(string fullname, string name)
        {
            FullName = fullname;
            Name = name;
        }
    }
}