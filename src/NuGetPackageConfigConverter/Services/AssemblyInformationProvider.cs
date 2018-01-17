using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace NuGetPackageConfigConverter
{
    public sealed class AssemblyInformationProvider
    {
        public IReadOnlyCollection<AssemblyInformation> GetAssemblyReferences(Stream stream)
        {
            using (var reader = new PEReader(stream))
            {
                var metadataReader = reader.GetMetadataReader();

                return metadataReader.AssemblyReferences
                    .Select(metadataReader.GetAssemblyReference)
                    .Select(metadataReader.FormatAssemblyInfo)
                    .ToArray();
            }

        }

        public AssemblyInformation GetAssemblyName(Stream stream)
        {
            using (var reader = new PEReader(stream))
            {
                var m = reader.GetMetadataReader();
                return m.FormatAssemblyInfo();
            }
        }
    }
}
