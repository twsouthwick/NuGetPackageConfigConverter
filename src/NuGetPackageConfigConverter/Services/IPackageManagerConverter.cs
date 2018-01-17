using EnvDTE;
using System.Threading.Tasks;

namespace NuGetPackageConfigConverter
{
    public interface IPackageManagerConverter
    {
        Task ConvertAsync(Solution sln,ILogger logger);

        bool NeedsConversion(Solution sln);
    }
}