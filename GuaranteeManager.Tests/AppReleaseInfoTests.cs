using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class AppReleaseInfoTests
    {
        [Fact]
        public void VersionTag_UsesAssemblyInformationalVersion()
        {
            string informationalVersion = typeof(AppReleaseInfo).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion?
                .Split('+')[0]
                ?? throw new InvalidOperationException("Assembly informational version is missing.");

            string expected = informationalVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? informationalVersion
                : $"v{informationalVersion}";

            Assert.Equal(expected, AppReleaseInfo.VersionTag);
        }

        [Fact]
        public void RuntimeTag_UsesAssemblyMetadata()
        {
            string runtimeIdentifier = typeof(AppReleaseInfo).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => string.Equals(attribute.Key, "RuntimeIdentifier", StringComparison.OrdinalIgnoreCase))?
                .Value
                ?? throw new InvalidOperationException("RuntimeIdentifier metadata is missing.");

            Assert.Equal(runtimeIdentifier, AppReleaseInfo.RuntimeTag);
        }
    }
}
