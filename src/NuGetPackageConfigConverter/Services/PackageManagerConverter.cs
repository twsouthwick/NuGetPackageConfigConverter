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
        private ILogger logger;

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

        public Task ConvertAsync(Solution sln,ILogger log)
        {
            logger= log;
            logger.Init(sln);
            return _converterViewProvider.ShowAsync(sln, (model, token) =>
            {
                model.Phase = "1/6: Get Projects";
                var projects = sln.GetProjects()
                    .Where(p => HasPackageConfig(p) || HasProjectJson(p))
                    .ToList();

                model.Total = projects.Count * 2 + 1;
                model.IsIndeterminate = false;
                model.Count = 1;

                model.Phase = "2/6: Restore packages in the projects";
                RestoreAll(projects, model);

                model.Phase = "3/6: Remove and cache Packages";
                logger.Log(model.Phase);
                var packages = RemoveAndCachePackages(projects, model, token);
                token.ThrowIfCancellationRequested();

                model.Phase = "4/6: Remove old dependencyfiles";
                logger.Log(model.Phase);
                RemoveDependencyFiles(projects, model);

                System.Threading.Thread.Sleep(1000);

                model.Phase = "5/6: Add new 'use packagereference' property to projectfiles";
                logger.Log(model.Phase);
                RefreshSolution(sln, projects, model);

                System.Threading.Thread.Sleep(10000); // Just to make sure all is really reloaded, can't find any way to check this.

                model.Phase = "6/6: Add packages as packagereferences to projectfiles";
                logger.Log(model.Phase);
                var updatedProjects = sln.GetProjects();
                InstallPackages(updatedProjects, packages, model, token);

                logger.Log("Finished");

            });
        }

        private void RestoreAll(IEnumerable<Project> projects, ConverterUpdateViewModel model)
        {
            foreach (var project in projects)
            {
                _restorer.RestorePackages(project);
            }
        }

        private IDictionary<string, IEnumerable<PackageConfigEntry>> RemoveAndCachePackages(
            IEnumerable<Project> projects, ConverterUpdateViewModel model, CancellationToken token)
        {
            var installedPackages =
                new Dictionary<string, IEnumerable<PackageConfigEntry>>(StringComparer.OrdinalIgnoreCase);
            var projectList = projects.ToList();
            int total = projectList.Count;
            foreach (var project in projectList)
            {
                token.ThrowIfCancellationRequested();

                model.Status =
                    $"{model.Count}/{total}  Retrieving and removing old package format for '{project.Name}'";
                logger.Log(model.Status);

                var packages = _services.GetInstalledPackages(project)
                    .Select(p => new PackageConfigEntry(p.Id, p.VersionString))
                    .ToArray();
                var fullname = project.GetFullName();
                if (fullname != null)
                {
                    installedPackages.Add(fullname, packages);
                    logger.Log($"{project.Name} added {packages.Length} packages");
                    RemovePackages(project, packages.Select(p => p.Id), token, model);
                }
                else
                {
                    model.Log = $"{project.Name} not modified, missing fullname";
                    logger.Log(model.Log);
                }

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
        /// <param name="model"></param>
        /// <returns></returns>
        private bool RemovePackages(Project project, IEnumerable<string> ids, CancellationToken token,
            ConverterUpdateViewModel model)
        {
            var retryCount = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var packages = new Queue<string>(ids);
            var maxRetry = packages.Count + 1;
            int maxAttempts = maxRetry * packages.Count;
            int counter = 0;
            logger.Log($"{project.Name}: Packages: {packages.Count}, maxattempts: {maxAttempts}");
            while (packages.Count > 0 && counter < maxAttempts)
            {
                counter++;
                token.ThrowIfCancellationRequested();

                var package = packages.Dequeue();

                try
                {
                    model.Log = $"Uninstalling {package}";
                    logger.Log(model.Log + $" (counter = {counter})");

                    _uninstaller.UninstallPackage(project, package, false);
                    model.Log = $"Uninstalled {package}";
                    logger.Log(model.Log);
                    counter = 0;


                }
                catch (Exception e)
                {
                    if (e is InvalidOperationException)
                    {
                        model.Log = $"Invalid operation exception when uninstalling {package} ";
                        logger.Log(model.Log);
                        Debug.WriteLine(e.Message);
                    }
                    else
                    {
                        model.Log = $"Exception uninstalling {package} ";
                        logger.Log(model.Log);
                        Debug.WriteLine(e);
                    }

                    retryCount.AddOrUpdate(package, 1, (_, count) => count + 1);

                    if (retryCount[package] < maxRetry)
                    {
                        model.Log = $"{package} added back to queue";
                        logger.Log(model.Log);
                        packages.Enqueue(package);
                    }
                }
            }

            if (counter == maxAttempts)
            {
                model.Log = $"Could not uninstall all packages in {project.Name}";
                logger.Log(model.Log);
                System.Threading.Thread.Sleep(2000);
            }

            return !retryCount.Values.Any(v => v >= maxRetry);
        }

        private static bool HasPackageConfig(Solution sln) => sln.GetProjects().Any(HasPackageConfig);

        private static bool HasPackageConfig(Project project) => GetPackageConfig(project) != null;

        private static bool HasProjectJson(Solution sln) => sln.GetProjects().Any(HasProjectJson);

        private static bool HasProjectJson(Project project) => GetProjectJson(project) != null;

        private static ProjectItem GetPackageConfig(Project project) =>
            GetProjectItem(project.ProjectItems, "packages.config");

        private void RemoveDependencyFiles(IEnumerable<Project> projects, ConverterUpdateViewModel model)
        {

            foreach (var project in projects)
            {
                model.Status = $"Removing dependency files for '{project.Name}'";
                logger.Log(model.Status);
                RemoveDependencyFiles(project);
            }
        }

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

            project.Save();
        }

        private void RefreshSolution(Solution sln, IEnumerable<Project> projects, ConverterUpdateViewModel model)
        {
            try
            {
                var projectInfos = projects.Select(p => new ProjectInfo(p.GetFullName(), p.Name)).ToList();
                var slnPath = sln.FullName;
                logger.Log("Closing solution");
                sln.Close();
                int counter = 0;
                int total = projectInfos.Count(p => !string.IsNullOrEmpty(p.FullName));
                foreach (var project in projectInfos.Where(p => !string.IsNullOrEmpty(p.FullName)))
                {
                    counter++;
                    model.Status = $"Fixing restore style in'{project.Name}/{project.FullName}'   ({counter}/{total}";
                    logger.Log(model.Status);
                    AddRestoreProjectStyle(project.FullName, project, model);
                }

                sln.Open(slnPath);
                logger.Log("Reopen the solution");
            }
            catch (Exception e)
            {
                model.Log = $"Exception while working with restore style property.  Do this manually.";
                logger.Log(model.Log);
                logger.Log(e.ToString());
            }
        }

        private void AddRestoreProjectStyle(string path, ProjectInfo project, ConverterUpdateViewModel model)
        {
            try
            {
                const string NS = "http://schemas.microsoft.com/developer/msbuild/2003";
                var doc = XDocument.Load(path);
                var properties = doc.Descendants(XName.Get("PropertyGroup", NS)).FirstOrDefault();
                properties?.LastNode.AddAfterSelf(
                    new XElement(XName.Get("RestoreProjectStyle", NS), "PackageReference"));
                logger.Log($"Fixed projectstyle in ");
                doc.Save(path);
            }
            catch (Exception e)
            {
                model.Log = $"Exception: Project {project.Name}/{project.FullName},  Exception: {e.Message},{e}";
                logger.Log(model.Log);
            }
        }

        private void InstallPackages(IEnumerable<Project> projects,
            IDictionary<string, IEnumerable<PackageConfigEntry>> installedPackages, ConverterUpdateViewModel model,
            CancellationToken token)
        {
            var actualProjects = projects.Where(p => p.GetFullName() != null).ToList();
            logger.Log($"Projects: {actualProjects.Count}    Retrived packages: {installedPackages.Count}");
            int countProjects = 0;
            foreach (var project in actualProjects)
            {
                countProjects++;
                token.ThrowIfCancellationRequested();
                var exist = installedPackages.TryGetValue(project.GetFullName(), out var packages);
                var packagecount = (exist) ? packages.Count() : 0;
                logger.Log(
                    $"({countProjects}/{actualProjects.Count})  Project {project.Name}/{project.GetFullName()}, packages to install: {packagecount} ");
                if (packagecount == 0)
                    continue;
                try
                {

                    model.Status =
                        $"({countProjects}/{actualProjects.Count})  Adding PackageReferences: {project.Name}";
                    int counter = 0;
                    int countPackages = packages.Count();
                    foreach (var package in packages)
                    {
                        try
                        {
                            _installer.InstallPackage(null, project, package.Id, package.Version, false);
                            counter++;
                            model.Log = $"({counter}/{countPackages}) Added package {package.Id}";
                            logger.Log(model.Log);
                        }
                        catch (Exception e)
                        {
                            model.Log = $"Exception installing {package.Id} ({e})";
                            logger.Log(model.Log);
                        }
                    }

                    model.Status =
                        $"Added PackageReferences to {project.Name}/{project.FullName},  {counter} out of {packages.Count()} included";
                    logger.Log(model.Status);
                    model.Count++;


                    System.Threading.Thread.Sleep(1000);
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