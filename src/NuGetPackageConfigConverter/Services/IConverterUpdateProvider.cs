using System;
using System.Threading.Tasks;

namespace NuGetPackageConfigConverter
{
    public interface IConverterViewProvider
    {
        Task ShowAsync(Action<ConverterUpdateViewModel> model);
    }
}
