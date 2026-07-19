using Martin.Build.Artifacts;
using Xunit;

namespace Martin.Build.Tests;

public sealed class RuntimeDiscoveryTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "MartinRuntimeDiscoveryTests", Guid.NewGuid().ToString("N"));

    public RuntimeDiscoveryTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Explicit_runtime_path_takes_priority()
    {
        var explicitPath = CopyRuntime(Path.Combine(root, "explicit", "Martin.Runtime.dll"));
        CopyRuntime(Path.Combine(root, "tool", "Martin.Runtime.dll"));

        var result = Discover(explicitRuntimePath: explicitPath, toolDirectory: Path.Combine(root, "tool"));

        Assert.True(result.Success);
        Assert.Equal(Path.GetFullPath(explicitPath), result.Runtime!.AssemblyPath);
    }

    [Fact]
    public void Runtime_beside_tool_is_discovered()
    {
        var runtime = CopyRuntime(Path.Combine(root, "tool", "Martin.Runtime.dll"));

        var result = Discover(toolDirectory: Path.Combine(root, "tool"));

        Assert.True(result.Success);
        Assert.Equal(Path.GetFullPath(runtime), result.Runtime!.AssemblyPath);
    }

    [Fact]
    public void Configured_runtime_directory_is_discovered_after_tool_directory()
    {
        var runtime = CopyRuntime(Path.Combine(root, "configured", "Martin.Runtime.dll"));

        var result = Discover(toolDirectory: Path.Combine(root, "tool"), configuredRuntimeDirectory: Path.Combine(root, "configured"));

        Assert.True(result.Success);
        Assert.Equal(Path.GetFullPath(runtime), result.Runtime!.AssemblyPath);
    }

    [Fact]
    public void Multiple_candidates_use_stable_lookup_order()
    {
        var toolRuntime = CopyRuntime(Path.Combine(root, "tool", "Martin.Runtime.dll"));
        CopyRuntime(Path.Combine(root, "configured", "Martin.Runtime.dll"));
        CopyRuntime(Path.Combine(root, "known", "Martin.Runtime.dll"));

        var result = Discover(toolDirectory: Path.Combine(root, "tool"), configuredRuntimeDirectory: Path.Combine(root, "configured"), knownRuntimeDirectory: Path.Combine(root, "known"));

        Assert.True(result.Success);
        Assert.Equal(Path.GetFullPath(toolRuntime), result.Runtime!.AssemblyPath);
    }

    [Fact]
    public void Missing_runtime_reports_actionable_diagnostic()
    {
        var result = Discover(toolDirectory: Path.Combine(root, "missing"));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3220" && d.Message.Contains("RuntimePath", StringComparison.Ordinal));
    }

    [Fact]
    public void Corrupt_explicit_candidate_is_rejected()
    {
        var corrupt = Path.Combine(root, "Martin.Runtime.dll");
        File.WriteAllText(corrupt, "not an assembly");

        var result = Discover(explicitRuntimePath: corrupt);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3221" && d.Message.Contains(corrupt, StringComparison.Ordinal));
    }

    RuntimeDiscoveryResult Discover(string? explicitRuntimePath = null, string? toolDirectory = null, string? configuredRuntimeDirectory = null, string? knownRuntimeDirectory = null)
        => new RuntimeDiscovery().Discover(new RuntimeDiscoveryOptions
        {
            ExplicitRuntimePath = explicitRuntimePath,
            ToolDirectory = toolDirectory ?? Path.Combine(root, "empty-tool"),
            ConfiguredRuntimeDirectory = configuredRuntimeDirectory,
            KnownRuntimeDirectory = knownRuntimeDirectory,
            StagingRoot = root
        });

    static string CopyRuntime(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(typeof(Martin.Runtime.AssemblyMarker).Assembly.Location, path, overwrite: true);
        return path;
    }
}
