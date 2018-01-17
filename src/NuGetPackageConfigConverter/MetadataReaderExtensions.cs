using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection.Metadata;
using System.Security.Cryptography;

namespace NuGetPackageConfigConverter
{
    public static class MetadataReaderExtensions
    {
        public static IReadOnlyCollection<AssemblyInformation> GetAssemblyReferences(this AssemblyInformationProvider provider, Compilation compilation)
        {
            using (var ms = new MemoryStream())
            {
                compilation.Emit(ms);
                ms.Position = 0;

                return provider.GetAssemblyReferences(ms);
            }
        }

        public static IReadOnlyCollection<AssemblyInformation> GetAssemblyReferences(this AssemblyInformationProvider provider, string path)
        {
            using (var fs = File.OpenRead(path))
            {
                return provider.GetAssemblyReferences(fs);
            }
        }

        public static AssemblyInformation GetAssemblyName(this AssemblyInformationProvider provider, string path)
        {
            using (var fs = File.OpenRead(path))
            {
                return provider.GetAssemblyName(fs);
            }
        }

        public static AssemblyInformation FormatAssemblyInfo(this MetadataReader metadataReader)
        {
            return metadataReader.FormatAssemblyInfo(metadataReader.GetAssemblyDefinition());
        }

        public static AssemblyInformation FormatAssemblyInfo(this MetadataReader metadataReader, AssemblyReference assemblyReference)
        {
            var name = metadataReader.GetString(assemblyReference.Name);

            return metadataReader.FormatAssemblyInfo(name, assemblyReference.Culture, assemblyReference.PublicKeyOrToken, assemblyReference.Version);
        }

        public static AssemblyInformation FormatAssemblyInfo(this MetadataReader metadataReader, AssemblyDefinition assemblyDefinition)
        {
            var name = metadataReader.GetString(assemblyDefinition.Name);

            return metadataReader.FormatAssemblyInfo(name, assemblyDefinition.Culture, assemblyDefinition.PublicKey, assemblyDefinition.Version);
        }

        private static AssemblyInformation FormatAssemblyInfo(this MetadataReader metadataReader, string name, StringHandle cultureHandle, BlobHandle publicKeyTokenHandle, Version version)
        {
            var culture = cultureHandle.IsNil
                ? "neutral"
                : metadataReader.GetString(cultureHandle);

            var publicKeyToken = publicKeyTokenHandle.IsNil
                ? "null"
                : metadataReader.FormatPublicKeyToken(publicKeyTokenHandle);

            return new AssemblyInformation($"{name}, Version={version}, Culture={culture}, PublicKeyToken={publicKeyToken}");
        }

        /// <summary>
        /// Convert a blob referencing a public key token from a PE file into a human-readable string.
        /// 
        /// If there are no bytes, the return will be 'null'
        /// If the length is greater than 8, it is a strong name signed assembly
        /// Otherwise, the key is the byte sequence
        /// </summary>
        /// <param name="metadataReader"></param>
        /// <param name="handle"></param>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Security.Cryptography", "CA5354:SHA1CannotBeUsed", Justification = "Public key tokens are calculated using a SHA-1 hash.")]
        private static string FormatPublicKeyToken(this MetadataReader metadataReader, BlobHandle handle)
        {
            byte[] bytes = metadataReader.GetBlobBytes(handle);

            if (bytes == null || bytes.Length <= 0)
            {
                return "null";
            }

            if (bytes.Length > 8)  // Strong named assembly
            {
                // Get the public key token, which is the last 8 bytes of the SHA-1 hash of the public key 
                using (var sha1 = SHA1.Create())
                {
                    var token = sha1.ComputeHash(bytes);

                    bytes = new byte[8];
                    int count = 0;
                    for (int i = token.Length - 1; i >= token.Length - 8; i--)
                    {
                        bytes[count] = token[i];
                        count++;
                    }
                }
            }

            // Convert bytes to string, but we don't want the '-' characters and need it to be lower case
            return BitConverter.ToString(bytes)
                .Replace("-", "")
                .ToLowerInvariant();
        }
    }
}
