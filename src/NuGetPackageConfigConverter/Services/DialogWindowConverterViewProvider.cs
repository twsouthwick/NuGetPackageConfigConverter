using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetPackageConfigConverter
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IConverterViewProvider))]
    public class DialogWindowConverterViewProvider : IConverterViewProvider
    {
        public Task ShowAsync(Action<ConverterUpdateViewModel> action)
        {
            var model = new ConverterUpdateViewModel();
            var viewer = new ConvertProgressViewer(model);

            using (var cts = new CancellationTokenSource())
            {
                var task = Task.Run(() =>
                {
                    action(model);

                    cts.Cancel();
                });

                viewer.ShowModal(cts.Token);
            }

            return Task.CompletedTask;
        }
    }
}
