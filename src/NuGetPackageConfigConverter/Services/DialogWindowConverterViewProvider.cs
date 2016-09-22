using EnvDTE;
using Microsoft.VisualStudio.PlatformUI;
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
        public Task ShowAsync(Solution sln, Action<ConverterUpdateViewModel> action)
        {
            if (!sln.Saved)
            {
                MessageDialog.Show("Save before conversion.", "Solution needs to be saved before converting to project.json", MessageDialogCommandSet.Ok);
                return Task.CompletedTask;
            }

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

                MessageDialog.Show("Conversion complete", "Conversion to project.json dependency is complete. Some files may have been removed by uninstalling the packages and not added back. Please ensure project builds and runs before committing any changes.", MessageDialogCommandSet.Ok);
            }

            return Task.CompletedTask;
        }
    }
}
