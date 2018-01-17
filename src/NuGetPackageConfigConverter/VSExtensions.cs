using EnvDTE;
using System;
using System.Collections.Generic;

namespace NuGetPackageConfigConverter
{
    internal static class VSExtensions
    {
        private const string vsProjectKindSolutionFolder = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";

        public static IEnumerable<Project> GetProjects(this Solution sln)
        {
            foreach (Project project in sln.Projects)
            {
                if (string.Equals(project.Kind, vsProjectKindSolutionFolder, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var prj in GetSolutionFolderProjects(project))
                    {
                        if (!string.IsNullOrEmpty(GetFullName(prj)))
                        {
                            yield return prj;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(GetFullName(project)))
                {
                    yield return project;
                }
            }
        }

        public static string GetFullName(this Project project)
        {
            try
            {
                return project.FullName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IEnumerable<Project> GetSolutionFolderProjects(Project solutionFolder)
        {
            foreach (ProjectItem project in solutionFolder.ProjectItems)
            {
                var subProject = project.SubProject;

                if (subProject == null)
                {
                    continue;
                }

                if (subProject.Kind == vsProjectKindSolutionFolder)
                {
                    foreach (var prj in GetSolutionFolderProjects(subProject))
                    {
                        yield return prj;
                    }
                }
                else
                {
                    yield return subProject;
                }
            }
        }
    }
}
