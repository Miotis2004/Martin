using Martin.Build.Artifacts;
using Martin.Runtime;
using Xunit;

namespace Martin.Build.Tests;

public sealed class BuildArtifactValidatorTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "MartinArtifactValidatorTests", Guid.NewGuid().ToString("N"));

    public BuildArtifactValidatorTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Validate_accepts_complete_valid_set()
    {
        var result = Validate(CompleteArtifacts());

        Assert.True(result.Success);
    }

    [Theory]
    [InlineData(BuildArtifactKind.ManagedAssembly, "MRT3200")]
    [InlineData(BuildArtifactKind.RuntimeConfiguration, "MRT3200")]
    [InlineData(BuildArtifactKind.DependencyManifest, "MRT3200")]
    [InlineData(BuildArtifactKind.AppHost, "MRT3200")]
    [InlineData(BuildArtifactKind.PortablePdb, "MRT3200")]
    [InlineData(BuildArtifactKind.RuntimeLibrary, "MRT3200")]
    public void Validate_reports_missing_required_artifacts(BuildArtifactKind missingKind, string code)
    {
        var artifacts = CompleteArtifacts().Where(a => a.Kind != missingKind).ToArray();

        var result = Validate(artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void Validate_reports_unexpected_app_host_when_disabled()
    {
        var result = Validate(CompleteArtifacts(), Options() with { UseAppHost = false });

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3203");
    }

    [Fact]
    public void Validate_reports_unexpected_pdb_when_disabled()
    {
        var result = Validate(CompleteArtifacts(), Options() with { EmitPortablePdb = false });

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3205");
    }

    [Fact]
    public void Validate_reports_duplicate_single_instance_artifacts()
    {
        var artifacts = CompleteArtifacts().Append(new BuildArtifact(BuildArtifactKind.ManagedAssembly, Write("Demo.copy.dll"))).ToArray();

        var result = Validate(artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3209" && d.Message.Contains("ManagedAssembly", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_reports_managed_assembly_name_mismatch()
    {
        var artifacts = CompleteArtifacts().Select(a => a.Kind == BuildArtifactKind.ManagedAssembly ? a with { Path = Write("Other.dll") } : a).ToArray();

        var result = Validate(artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3201");
    }


    [Fact]
    public void Validate_reports_runtime_compatibility_mismatch()
    {
        var expected = RuntimeDescriptor.FromPath(RuntimePath(), root) with { CompatibilityMajor = RuntimeCompatibility.CompilerCompatibilityMajor + 1 };

        var result = new BuildArtifactValidator().Validate(Options(), expected, [.. CompleteArtifacts()]);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3225");
    }

    [Fact]
    public void Validate_reports_staged_runtime_mismatch()
    {
        var artifacts = CompleteArtifacts();
        var stagedRuntime = Path.Combine(root, "staged-corrupt", "Martin.Runtime.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(stagedRuntime)!);
        File.WriteAllText(stagedRuntime, "not a runtime");
        artifacts = artifacts.Select(a => a.Kind == BuildArtifactKind.RuntimeLibrary ? a with { Path = stagedRuntime } : a).ToArray();

        var result = Validate(artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3222");
    }

    [Fact]
    public void Validate_reports_path_outside_staging_root()
    {
        var outside = Path.Combine(Path.GetTempPath(), "MartinArtifactValidatorOutside", Guid.NewGuid().ToString("N"), "outside.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outside)!);
        File.WriteAllText(outside, "outside");
        try
        {
            var artifacts = CompleteArtifacts().Append(new BuildArtifact(BuildArtifactKind.Other, outside)).ToArray();

            var result = Validate(artifacts);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Code == "MRT3207");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(outside)!, recursive: true);
        }
    }

    [Fact]
    public void Validate_reports_unreadable_file()
    {
        var missing = Path.Combine(root, "missing.txt");
        var artifacts = CompleteArtifacts().Append(new BuildArtifact(BuildArtifactKind.Other, missing)).ToArray();

        var result = Validate(artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3208");
    }

    BuildArtifact[] CompleteArtifacts() =>
    [
        new(BuildArtifactKind.AppHost, Write("Demo")),
        new(BuildArtifactKind.ManagedAssembly, Write("Demo.dll")),
        new(BuildArtifactKind.RuntimeConfiguration, Write("Demo.runtimeconfig.json")),
        new(BuildArtifactKind.DependencyManifest, Write("Demo.deps.json")),
        new(BuildArtifactKind.PortablePdb, Write("Demo.pdb")),
        new(BuildArtifactKind.RuntimeLibrary, RuntimePath()),
    ];

    ArtifactValidationResult Validate(IEnumerable<BuildArtifact> artifacts, BuildOptions? options = null)
        => new BuildArtifactValidator().Validate(options ?? Options(), RuntimeDescriptor.FromPath(RuntimePath(), root), [.. artifacts]);

    string RuntimePath()
    {
        var runtime = typeof(Martin.Runtime.AssemblyMarker).Assembly.Location;
        var path = Path.Combine(root, "Martin.Runtime.dll");
        if (!File.Exists(path))
            File.Copy(runtime, path);
        return path;
    }

    string Write(string fileName)
    {
        var path = Path.Combine(root, fileName);
        File.WriteAllText(path, fileName);
        return path;
    }

    static BuildOptions Options() => new()
    {
        OutputDirectory = "ignored",
        AssemblyName = "Demo"
    };
}
