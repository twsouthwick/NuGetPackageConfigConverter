using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.LanguageServices;
using NuGet.VisualStudio;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetPackageConfigConverter
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(PackageReferenceCleaner))]
    internal class PackageReferenceCleaner
    {
        private readonly IVsPackageInstallerServices _services;
        private readonly IVsPackageUninstaller _uninstaller;
        private readonly IVsPackageMetadata _packageMetadata;
        private readonly VisualStudioWorkspace _workspace;

        [ImportingConstructor]
        public PackageReferenceCleaner(IVsPackageInstallerServices services, IVsPackageUninstaller uninstaller, IVsPackageRestorer restorer, VisualStudioWorkspace workspace)
        {
            _services = services;
            _uninstaller = uninstaller;
            _workspace = workspace;
        }

        public async Task<bool> CleanAsync(EnvDTE.Project project, CancellationToken token)
        {
            var rProject = _workspace.GetProject(project);
            var compilation = await rProject.GetCompilationAsync(token).ConfigureAwait(false);

            if (compilation == null)
            {
                return false;
            }

            var diagnostics = compilation.GetDiagnostics();

            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return false;
            }

            var provider = new AssemblyInformationProvider();
            var referencedAssemblies = provider.GetAssemblyReferences(compilation);

            GetNuGetPackages(project, compilation);

            return true;
        }

        private void GetNuGetPackages(EnvDTE.Project project, Compilation compilation)
        {
            foreach (var package in _services.GetInstalledPackages(project))
            {
            }
        }

        private static byte[] GetAssemblyBytes(Compilation compilation)
        {
            using (var ms = new MemoryStream())
            {
                compilation.Emit(ms);

                return ms.ToArray();
            }
        }
    }
}
