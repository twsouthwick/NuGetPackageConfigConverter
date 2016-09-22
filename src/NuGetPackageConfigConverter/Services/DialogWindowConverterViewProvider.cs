using EnvDTE;
using Microsoft.VisualStudio.PlatformUI;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetPackageConfigConverter
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IConverterViewProvider))]
    public class DialogWindowConverterViewProvider : IConverterViewProvider
    {
        public async Task ShowAsync(Solution sln, Action<ConverterUpdateViewModel, CancellationToken> action)
        {
            if (IsUnsaved(sln))
            {
                MessageDialog.Show("Save before conversion.", "Solution and projects needs to be saved before converting to project.json", MessageDialogCommandSet.Ok);
                return;
            }

            var model = new ConverterUpdateViewModel();

            using (var cts = new CancellationTokenSource())
            {
                // This is run on the UI thread, so we capture this before we do anything else
                var taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

                var viewer = new ConvertProgressViewer(model, cts.Cancel);
                var tcs = new TaskCompletionSource<bool>();

                var task = Task.Run(() =>
                {
                    try
                    {
                        action(model, cts.Token);
                        tcs.SetResult(true);
                    }
                    catch (OperationCanceledException)
                    {
                        tcs.SetCanceled();
                    }
                    catch (Exception e)
                    {
                        tcs.SetException(e);
                    }
                    finally
                    {
                        cts.Cancel();
                    }
                });

                var result = viewer.ShowModal(cts.Token);

                await tcs.Task.ContinueWith(r =>
                {
                    if (r.IsCanceled)
                    {
                        MessageDialog.Show("Conversion incomplete", "Conversion to project.json dependency was canceled. Please revert any changes and try again.", MessageDialogCommandSet.Ok);
                    }
                    else if (r.IsFaulted)
                    {
                        const string url = "https://github.com/twsouthwick/NuGetPackageConfigConverter/";

                        if (MessageDialog.Show("Conversion failed", $"An unexpected error occured. Please open an issue at {url} with the contents on the clipboard. Press OK to be taken to the issue tracker.", MessageDialogCommandSet.OkCancel) == MessageDialogCommand.Ok)
                        {
                            System.Windows.Clipboard.SetText(r.Exception?.ToString() ?? "No exception");
                            System.Diagnostics.Process.Start($"{url}issues/new");
                        }
                    }
                    else
                    {
                        MessageDialog.Show("Conversion complete", "Conversion to project.json dependency is complete. Some files may have been removed by uninstalling the packages and not added back. Please ensure project builds and runs before committing any changes.", MessageDialogCommandSet.Ok);
                    }
                }, taskScheduler);
            }
        }

        private static bool IsUnsaved(Solution sln)
        {
            if (!sln.Saved)
            {
                return true;
            }

            return sln.Projects.OfType<Project>().Any(p => !p.Saved);
        }
    }
}
