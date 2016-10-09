using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace NuGetPackageConfigConverter
{
    /// <summary>
    /// A proxy for NuGet.VisualStudio.IVsFrameworkParser that is not publicly available
    /// </summary>
    /// <remarks>This may break on later releases of Visual Studio or NuGet package manager. If this breaks, the resulting project.json will not have a TFM</remarks>
    /// <see cref="https://github.com/NuGet/NuGet.Client/blob/4cccb13833ad29d6a0bcff055460d964f1b49cfe/src/NuGet.Clients/VisualStudio/Extensibility/IVsFrameworkParser.cs"/>
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IVsFrameworkParser))]
    internal class VsFrameworkParserProxy : IVsFrameworkParser
    {
        private readonly dynamic _parser;

        [ImportingConstructor]
        public VsFrameworkParserProxy()
        {
            _parser = GetService("NuGet.VisualStudio.IVsFrameworkParser");
        }

        public FrameworkName GetFrameworkName(Project project)
        {
            try
            {
                var tfm = project.Properties.Item("TargetFrameworkMoniker")?.Value?.ToString();

                if (tfm == null)
                {
                    return null;
                }

                return new FrameworkName(tfm);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public string GetShortenedTfm(Project project)
        {
            if (_parser == null)
            {
                return null;
            }

            var tfm = GetFrameworkName(project);

            if (tfm == null)
            {
                return null;
            }

            return (string)_parser.GetShortFrameworkName(tfm) ?? string.Empty;
        }

        private static dynamic GetService(string name)
        {
            var model = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var parser = model.DefaultExportProvider.GetExportedValueOrDefault<object>(name);

            Debug.Assert(parser != null);

            return parser;
        }
    }
}
