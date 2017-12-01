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
using System.Xml.Linq;

namespace NuGetPackageConfigConverter
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IPackageManagerConverter))]
    public class PackageManagerConverter : IPackageManagerConverter
    {
        private readonly IVsPackageInstaller _installer;
        private readonly IVsPackageUninstaller _uninstaller;
        private readonly IVsPackageRestorer _restorer;
        private readonly IConverterViewProvider _converterViewProvider;
        private readonly IVsPackageInstallerServices _services;

        [ImportingConstructor]
        public PackageManagerConverter(
            IConverterViewProvider converterViewProvider,
            IVsPackageInstallerServices services,
            IVsPackageInstaller installer,
            IVsPackageUninstaller uninstaller,
            IVsPackageRestorer restorer)
        {
            _converterViewProvider = converterViewProvider;
            _installer = installer;
            _services = services;
            _uninstaller = uninstaller;
            _restorer = restorer;
        }

        public bool NeedsConversion(Solution sln) => HasPackageConfig(sln) || HasProjectJson(sln);

        public Task ConvertAsync(Solution sln)
        {
            return _converterViewProvider.ShowAsync(sln, (model, token) =>
            {
                var projects = sln.GetProjects()
                    .Where(p => HasPackageConfig(p) || HasProjectJson(p))
                    .ToList();

                model.Total = projects.Count * 2 + 1;
                model.IsIndeterminate = false;
                model.Count = 1;

                var packages = RemoveAndCachePackages(projects, model, token);

                token.ThrowIfCancellationRequested();

                model.Status = "Reloading solution";
                RefreshSolution(sln, projects);

                InstallPackages(sln, packages, model, token);
            });
        }

        private IDictionary<string, IEnumerable<PackageConfigEntry>> RemoveAndCachePackages(IEnumerable<Project> projects, ConverterUpdateViewModel model, CancellationToken token)
        {
            var installedPackages = new Dictionary<string, IEnumerable<PackageConfigEntry>>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in projects)
            {
                token.ThrowIfCancellationRequested();

                model.Status = $"Removing old package format for '{project.Name}'";

                _restorer.RestorePackages(project);

                var packages = _services.GetInstalledPackages(project)
                    .Select(p => new PackageConfigEntry(p.Id, p.VersionString))
                    .ToArray();

                installedPackages.Add(project.FullName, packages);
                _restorer.RestorePackages(project);

                RemovePackages(project, packages.Select(p => p.Id), token);
                RemoveDependencyFiles(project);

                project.Save();

                model.Count++;
            }

            return installedPackages;
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

                    retryCount.AddOrUpdate(package, 1, (_, count) => count + 1);

                    if (retryCount[package] < maxRetry)
                    {
                        packages.Enqueue(package);
                    }
                }
            }

            return !retryCount.Values.Any(v => v >= maxRetry);
        }

        private static bool HasPackageConfig(Solution sln) => sln.GetProjects().Any(p => HasPackageConfig(p));

        private static bool HasPackageConfig(Project project) => GetPackageConfig(project) != null;

        private static bool HasProjectJson(Solution sln) => sln.GetProjects().Any(p => HasProjectJson(p));

        private static bool HasProjectJson(Project project) => GetProjectJson(project) != null;

        private static ProjectItem GetPackageConfig(Project project) => GetProjectItem(project.ProjectItems, "packages.config");

        private static void RemoveDependencyFiles(Project project)
        {
            GetPackageConfig(project)?.Delete();

            var projectJson = GetProjectJson(project);

            if (projectJson != null)
            {
                var file = Path.Combine(Path.GetDirectoryName(projectJson.FileNames[0]), "project.lock.json");

                projectJson.Delete();

                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        private static void RefreshSolution(Solution sln, IEnumerable<Project> projects)
        {
            var projectPaths = projects.Select(p => p.FullName).ToList();
            var path = sln.FullName;

            sln.Close();

            foreach (var project in projectPaths)
            {
                AddRestoreProjectStyle(project);
            }

            sln.Open(path);
        }

        private static void AddRestoreProjectStyle(string path)
        {
            const string NS = "http://schemas.microsoft.com/developer/msbuild/2003";
            var doc = XDocument.Load(path);
            var properties = doc.Descendants(XName.Get("PropertyGroup", NS)).FirstOrDefault();
            properties.LastNode.AddAfterSelf(new XElement(XName.Get("RestoreProjectStyle", NS), "PackageReference"));

            doc.Save(path);
        }

        private void InstallPackages(Solution sln, IDictionary<string, IEnumerable<PackageConfigEntry>> installedPackages, ConverterUpdateViewModel model, CancellationToken token)
        {
            foreach (var project in sln.GetProjects())
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    if (installedPackages.TryGetValue(project.FullName, out var packages))
                    {
                        model.Status = $"Adding packages: {project.Name}";

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

            return GetProjectItem(items, "project.json");
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
