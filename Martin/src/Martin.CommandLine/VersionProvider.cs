using System.Reflection;
using System.Runtime.Versioning;
using Martin.Compiler;
using Martin.ProjectSystem;
using Martin.Runtime;

namespace Martin.CommandLine;

public interface IVersionProvider
{
    string MartinCliVersion { get; }
    string MartinCompilerVersion { get; }
    string MartinRuntimeVersion { get; }
    string LanguageVersion { get; }
    string ManifestVersion { get; }
    string TargetFramework { get; }
}

public sealed class VersionProvider : IVersionProvider
{
    public string MartinCliVersion => InformationalVersion(Assembly.GetEntryAssembly() ?? typeof(VersionProvider).Assembly);

    public string MartinCompilerVersion => InformationalVersion(typeof(Compilation).Assembly);

    public string MartinRuntimeVersion => MartinRuntimeInfo.RuntimeVersion;

    public string LanguageVersion => MartinLanguageVersion.Current;

    public string ManifestVersion => ManifestParser.SupportedManifestVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string TargetFramework => TargetFrameworkName(typeof(Compilation).Assembly);

    static string InformationalVersion(Assembly assembly)
    {
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
            return informationalVersion;

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    static string TargetFrameworkName(Assembly assembly)
    {
        var frameworkName = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        if (string.IsNullOrWhiteSpace(frameworkName))
            return "unknown";

        const string netCoreAppPrefix = ".NETCoreApp,Version=v";
        if (frameworkName.StartsWith(netCoreAppPrefix, StringComparison.Ordinal))
            return "net" + frameworkName[netCoreAppPrefix.Length..];

        return frameworkName;
    }
}
