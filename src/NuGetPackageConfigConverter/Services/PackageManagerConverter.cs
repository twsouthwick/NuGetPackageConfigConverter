using EnvDTE;
using NuGet.VisualStudio;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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

        public bool NeedsConversion(Solution sln) => HasPackageConfig(sln) || !HasProjectJson(sln);

        public Task ConvertAsync(Solution sln)
        {
            return _converterViewProvider.ShowAsync(sln, (model, token) =>
            {
                var projects = sln.GetProjects()
                    .Select(p => new
                    {
                        Project = p,
                        Config = GetPackageConfig(p),
                        ProjectJson = GetProjectJson(p)
                    })
                    .ToList();

                var items = projects
                    .Where(p => p.Config != null)
                    .ToDictionary(p => p.Project, p => p.Config);

                var needsProjectJson = projects
                    .Where(p => p.ProjectJson == null)
                    .Select(p => p.Project);

                model.Total = items.Count * 2 + 1;
                model.IsIndeterminate = false;
                model.Count = 1;

                var packages = RemoveAndCachePackages(items, model, token);

                token.ThrowIfCancellationRequested();

                AddProjectJson(needsProjectJson, token);

                model.Status = "Reloading solution";
                RefreshSolution(sln);

                InstallPackages(sln, packages, model, token);
            });
        }

        private void AddProjectJson(IEnumerable<Project> projects, CancellationToken token)
        {
            foreach (var project in projects)
            {
                token.ThrowIfCancellationRequested();

                var path = CreateProjectJson(project);
                project.ProjectItems.AddFromFile(path);
                project.Save();
            }
        }

        private IDictionary<string, IEnumerable<PackageConfigEntry>> RemoveAndCachePackages(IEnumerable<KeyValuePair<Project, ProjectItem>> items, ConverterUpdateViewModel model, CancellationToken token)
        {
            var installedPackages = new Dictionary<string, IEnumerable<PackageConfigEntry>>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                token.ThrowIfCancellationRequested();

                var project = item.Key;
                var config = item.Value;

                model.Status = $"Removing package.config: {project.Name}";

                var packages = PackageConfigEntry.ParseFile(config.FileNames[0]);

                if (packages.Any())
                {
                    installedPackages.Add(project.FullName, packages);
                    _restorer.RestorePackages(project);

                    if (!RemovePackages(project, packages.Select(p => p.Id), token))
                    {
                        // Add warning that forcing deletion of package.config
                        config.Delete();
                    }
                }
                else
                {
                    config.Delete();
                }

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
        /// <param name="token"></param>
        /// <returns></returns>
        private bool RemovePackages(Project project, IEnumerable<string> ids, CancellationToken token)
        {
            var retryCount = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var packages = new Queue<string>(ids);
            var maxRetry = packages.Count + 1;

            while (packages.Count > 0)
            {
                token.ThrowIfCancellationRequested();

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

        private static bool HasPackageConfig(Solution sln)
        {
            foreach (var project in sln.GetProjects())
            {
                if (GetPackageConfig(project) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasProjectJson(Solution sln) => sln.GetProjects().All(p => GetProjectJson(p) != null);

        private static ProjectItem GetPackageConfig(Project project) => GetProjectItem(project.ProjectItems, "packages.config");

        private static void RefreshSolution(Solution sln)
        {
            var path = sln.FullName;
            sln.Close();
            sln.Open(path);
        }

        private void InstallPackages(Solution sln, IDictionary<string, IEnumerable<PackageConfigEntry>> installedPackages, ConverterUpdateViewModel model, CancellationToken token)
        {
            foreach (var project in sln.GetProjects())
            {
                token.ThrowIfCancellationRequested();

                try
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
                catch (NotImplementedException e)
                {
                    Trace.WriteLine(e);
                }
            }
        }

        private static ProjectItem GetProjectJson(Project project)
        {
            var items = project?.ProjectItems;

            if (project == null || items == null)
            {
                return null;
            }

            return GetProjectItem(items, "project.json")
                ?? GetProjectItem(items, $"{project.Name}.project.json");
        }

        private static ProjectItem GetProjectItem(ProjectItems items, string name)
        {
            if (items == null)
            {
                return null;
            }

            foreach (ProjectItem item in items)
            {
                if (string.Equals(name, item.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }
    }
}
