using EnvDTE;
using NuGet.VisualStudio;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetPackageConfigConverter
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IPackageManagerConverter))]
    public class PackageManagerConverter : IPackageManagerConverter
    {
        private readonly IVsPackageInstaller _installer;
        private readonly IVsPackageUninstaller _uninstaller;
        private readonly IVsPackageRestorer _restorer;
        private readonly IVsFrameworkParser _frameworkParser;
        private readonly IConverterViewProvider _converterViewProvider;

        [ImportingConstructor]
        public PackageManagerConverter(
            IConverterViewProvider converterViewProvider,
            IVsPackageInstaller installer,
            IVsPackageUninstaller uninstaller,
            IVsPackageRestorer restorer,
            IVsFrameworkParser frameworkParser)
        {
            _converterViewProvider = converterViewProvider;
            _installer = installer;
            _uninstaller = uninstaller;
            _restorer = restorer;
            _frameworkParser = frameworkParser;
        }

        public Task ConvertAsync(Solution sln)
        {
            return _converterViewProvider.ShowAsync(model =>
            {
                var items = sln.Projects
                    .OfType<Project>()
                    .Select(p => new { Project = p, Config = GetPackageConfig(p) })
                    .Where(p => p.Config != null)
                    .ToDictionary(p => p.Project, p => p.Config);

                model.Total = items.Count * 2 + 1;
                model.IsIndeterminate = false;
                model.Count = 1;

                var packages = RemoveAndCachePackages(items, model);

                model.Status = "Restarting solution";
                RefreshSolution(sln);

                InstallPackages(sln.Projects, packages, model);
            });
        }

        private IDictionary<string, IEnumerable<PackageConfigEntry>> RemoveAndCachePackages(IEnumerable<KeyValuePair<Project, ProjectItem>> items, ConverterUpdateViewModel model)
        {
            var installedPackages = new Dictionary<string, IEnumerable<PackageConfigEntry>>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                var project = item.Key;
                var config = item.Value;

                model.Status = $"Removing package.config: {project.Name}";

                var packages = PackageConfigEntry.ParseFile(config.FileNames[0]);

                installedPackages.Add(project.FullName, packages);
                _restorer.RestorePackages(project);

                if (!RemovePackages(project, packages.Select(p => p.Id)))
                {
                    // Add warning that forcing deletion of package.config
                    config.Delete();
                }

                var path = CreateProjectJson(project);
                project.ProjectItems.AddFromFile(path);
                project.Save();

                model.Count++;
            }

            return installedPackages;
        }

        private string CreateProjectJson(Project project)
        {
            var path = Path.Combine(Path.GetDirectoryName(project.FullName), "project.json");
            var tfm = _frameworkParser.GetShortenedTfm(project);

            var projectJson = @"{
  ""dependencies"": {
  },
  ""frameworks"": {
    ""[TFM]"": {}
  },
  ""runtimes"": {
    ""win"": {}
  }
}".Replace("[TFM]", tfm);

            File.WriteAllText(path, projectJson);

            return path;
        }

        /// <summary>
        /// Removes packages. Will do a couple of passes in case packages rely on each other
        /// </summary>
        /// <param name="project"></param>
        /// <param name="ids"></param>
        /// <returns></returns>
        private bool RemovePackages(Project project, IEnumerable<string> ids)
        {
            var retryCount = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var packages = new Queue<string>(ids);
            var maxRetry = packages.Count + 1;

            while (packages.Count > 0)
            {
                var package = packages.Dequeue();

                try
                {
                    _uninstaller.UninstallPackage(project, package, false);
                }
                catch (Exception e)
                {
                    if (e is InvalidOperationException)
                    {
                        Debug.WriteLine(e.Message);
                    }
                    else
                    {
                        Debug.WriteLine(e);
                    }

                    retryCount.AddOrUpdate(package, 1, (_, count) => count++);

                    if (retryCount[package] < maxRetry)
                    {
                        packages.Enqueue(package);
                    }
                }
            }

            return !retryCount.Values.Any(v => v >= maxRetry);
        }

        public bool HasPackageConfig(Projects projects)
        {
            foreach (Project project in projects)
            {
                if (GetPackageConfig(project) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static ProjectItem GetPackageConfig(Project project) => GetProjectItem(project.ProjectItems, "packages.config");

        private static void RefreshSolution(Solution sln)
        {
            var path = sln.FullName;
            sln.Close();
            sln.Open(path);
        }

        private void InstallPackages(Projects projects, IDictionary<string, IEnumerable<PackageConfigEntry>> installedPackages, ConverterUpdateViewModel model)
        {
            foreach (Project project in projects)
            {
                IEnumerable<PackageConfigEntry> packages;
                if (installedPackages.TryGetValue(project.FullName, out packages))
                {
                    model.Status = $"Adding packages back via project.json: {project.Name}";

                    foreach (var package in packages)
                    {
                        try
                        {
                            _installer.InstallPackage(null, project, package.Id, package.Version, false);
                        }
                        catch (Exception e)
                        {
                            Trace.WriteLine(e);
                        }
                    }

                    model.Count++;
                }
            }
        }

        private static ProjectItem GetProjectItem(ProjectItems items, string name)
        {
            if (items == null)
            {
                return null;
            }

            foreach (ProjectItem item in items)
            {
                if (string.Equals(name, Path.GetFileName(item.Name), StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }
    }
}
