using System;
using System.Reflection;

namespace NuGetPackageConfigConverter
{
    public struct AssemblyInformation
    {
        public AssemblyInformation(AssemblyName name)
        {
            Name = name.FullName;
        }

        internal AssemblyInformation(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public override bool Equals(object obj)
        {
            if (obj is AssemblyInformation other)
            {
                return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
    }
}
