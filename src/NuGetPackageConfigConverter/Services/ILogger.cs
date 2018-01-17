using EnvDTE;

namespace NuGetPackageConfigConverter
{
    public interface ILogger
    {
        void Log(string txt);

        void Init(Solution sln);
    }
}
