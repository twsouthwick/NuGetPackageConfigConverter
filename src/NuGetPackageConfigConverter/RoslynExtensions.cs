using Microsoft.CodeAnalysis;
using System;
using System.Linq;

namespace NuGetPackageConfigConverter
{
    public static class RoslynExtensions
    {
        /// <summary>
        /// Gets a Roslyn based <see cref="Project"/> from a Visual Studio <see cref="EnvDTE.Project"/>
        /// </summary>
        /// <param name="workspace">The workspace to find the project</param>
        /// <param name="project">The project to convert</param>
        /// <returns>A Roslyn based project</returns>
        public static Project GetProject(this Workspace workspace, EnvDTE.Project project)
        {
            return workspace.GetProject(project.FullName);
        }

        /// <summary>
        /// Get a Roslyn based project from a path
        /// </summary>
        /// <param name="workspace">The workspace to find the project</param>
        /// <param name="path">Path to project</param>
        /// <returns>A Roslyn based project</returns>
        public static Project GetProject(this Workspace workspace, string path)
        {
            return workspace.CurrentSolution
                .Projects
                .FirstOrDefault(p => string.Equals(p.FilePath, path, StringComparison.OrdinalIgnoreCase));
        }
    }
}
