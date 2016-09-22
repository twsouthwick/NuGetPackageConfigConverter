using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;

namespace NuGetPackageConfigConverter
{
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
            var frameworkParser = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(name, false))
                .FirstOrDefault(a => a != null);

            Debug.Assert(model != null);
            Debug.Assert(frameworkParser != null);

            if (frameworkParser != null)
            {
                return model?.DefaultExportProvider.GetExportedValueOrDefault<object>(frameworkParser.FullName);
            }

            return null;
        }
    }
}
