using EnvDTE;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetPackageConfigConverter
{
    public interface IConverterViewProvider
    {
        Task ShowAsync(Solution sln, Action<ConverterUpdateViewModel, CancellationToken> model);
    }
}
