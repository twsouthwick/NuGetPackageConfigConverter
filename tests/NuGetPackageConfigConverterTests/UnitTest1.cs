using System.Linq;
using Xunit;

namespace NuGetPackageConfigConverter
{
    public class AssemblyInformationProviderTests
    {
        [Fact]
        public void GetAssemblyName()
        {
            var assembly = typeof(object).Assembly;
            var expected = assembly.GetName();

            var provider = new AssemblyInformationProvider();
            var name = provider.GetAssemblyName(assembly.Location);

            Assert.Equal(expected.FullName, name.Name);
        }

        [Fact]
        public void GetReferencedAssemblies()
        {
            var assembly = typeof(object).Assembly;
            var expected = assembly.GetReferencedAssemblies()
                .Select(a => new AssemblyInformation(a))
                .ToList();

            var provider = new AssemblyInformationProvider();
            var actual = provider.GetAssemblyReferences(assembly.Location);

            Assert.Equal(expected.Count, actual.Count);
            Assert.Equal(expected.OrderBy(t => t.Name), actual.OrderBy(t => t.Name));
        }
    }
}
