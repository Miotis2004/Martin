using System.Reflection;

[assembly: AssemblyMetadata("MartinRuntimeCompatibilityMajor", "1")]
[assembly: AssemblyMetadata("MartinRuntimeCompatibilityMinor", "0")]

namespace Martin.Runtime;

public static class MartinRuntimeInfo
{
    public static string RuntimeVersion => typeof(MartinRuntimeInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion
        ?? typeof(MartinRuntimeInfo).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public const int CompatibilityMajor = 1;
    public const int CompatibilityMinor = 0;
}
