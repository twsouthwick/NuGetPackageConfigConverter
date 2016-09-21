using EnvDTE;
using System.Runtime.Versioning;

namespace NuGetPackageConfigConverter
{
    public interface IVsFrameworkParser
    {
        string GetShortenedTfm(Project project);
        FrameworkName GetFrameworkName(Project project);
    }
}
