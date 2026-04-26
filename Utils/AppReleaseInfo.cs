using System;
using System.Linq;
using System.Reflection;

namespace GuaranteeManager
{
    public static class AppReleaseInfo
    {
        private static readonly Assembly CurrentAssembly = typeof(AppReleaseInfo).Assembly;

        public static string VersionTag { get; } = BuildVersionTag();

        public static string RuntimeTag { get; } = BuildRuntimeTag();

        private static string BuildVersionTag()
        {
            string? informationalVersion = CurrentAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            string baseVersion = string.IsNullOrWhiteSpace(informationalVersion)
                ? CurrentAssembly.GetName().Version is Version version
                    ? $"{version.Major}.{version.Minor}.{version.Build}"
                    : "0.0.0"
                : informationalVersion.Split('+')[0];

            return baseVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? baseVersion
                : $"v{baseVersion}";
        }

        private static string BuildRuntimeTag()
        {
            return CurrentAssembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => string.Equals(attribute.Key, "RuntimeIdentifier", StringComparison.OrdinalIgnoreCase))?
                .Value
                ?? "win-x64";
        }
    }
}
