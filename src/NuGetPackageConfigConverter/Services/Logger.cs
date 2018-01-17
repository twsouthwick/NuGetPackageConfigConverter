using EnvDTE;
using System.ComponentModel.Composition;
using System.IO;

namespace NuGetPackageConfigConverter
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(ILogger))]
    public class Logger : ILogger
    {
        private StreamWriter logfile;

        public void Init(Solution sln)
        {
            var folderpath = Path.Combine(Path.GetTempPath(), "NugetPackageConfigConverter");
            Directory.CreateDirectory(folderpath);
            var path = Path.Combine(folderpath, sln.FullName + ".log");
            logfile = File.CreateText(path);
        }

        public void Log(string txt)
        {
            logfile.WriteLine(txt);
            logfile.Flush();
        }

        public void Close()
        {
            logfile.Close();
        }
    }
}
