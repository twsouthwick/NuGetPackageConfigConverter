using EnvDTE;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.Versioning;

namespace NuGetPackageConfigConverter
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IVsFrameworkParser))]
    internal class VsFrameworkParserProxy : IVsFrameworkParser
    {
        private readonly dynamic _parser;

        public VsFrameworkParserProxy()
        {
            var frameworkParser = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("NuGet.VisualStudio.IVsFrameworkParser", false))
                .FirstOrDefault(a => a != null);

            if (frameworkParser != null)
            {
                _parser = Clide.ServiceLocator.GlobalLocator.GetService(frameworkParser);
            }
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

    }
}
