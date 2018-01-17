using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Composition;

using static Microsoft.VisualStudio.VSConstants;

namespace NuGetPackageConfigConverter
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(ILogger))]
    public class OutputPaneLogger : ILogger
    {
        private readonly Lazy<IVsOutputWindowPane> _outputWindow;

        private static Guid s_outputWindowGuid = new Guid("002E297B-E99B-4F3C-98C5-5C270E456F38");

        [ImportingConstructor]
        public OutputPaneLogger()
        {
            _outputWindow = new Lazy<IVsOutputWindowPane>(GetWindowPane, true);
        }

        private static IVsOutputWindowPane GetWindowPane()
        {
            if (Package.GetGlobalService(typeof(SVsOutputWindow)) is IVsOutputWindow window)
            {
                if (window.GetPane(ref s_outputWindowGuid, out var windowPane) == S_OK)
                {
                    return windowPane;
                }

                if (window.CreatePane(ref s_outputWindowGuid, "NuGet Package Converter", 1, 0) == S_OK
                    && window.GetPane(ref s_outputWindowGuid, out windowPane) == S_OK)
                {
                    return windowPane;
                }
            }

            return Package.GetGlobalService(typeof(SVsGeneralOutputWindowPane)) as IVsOutputWindowPane;
        }

        public void WriteLine(string txt)
        {
            _outputWindow.Value.OutputStringThreadSafe(txt);
        }
    }
}
