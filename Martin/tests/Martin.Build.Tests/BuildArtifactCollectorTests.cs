using Martin.Build.Artifacts;
using Xunit;

namespace Martin.Build.Tests;

public sealed class BuildArtifactCollectorTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "MartinArtifactCollectorTests", Guid.NewGuid().ToString("N"));

    public BuildArtifactCollectorTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Collect_classifies_complete_console_app_with_app_host()
    {
        Write("Demo");
        Write("Demo.dll");
        Write("Demo.runtimeconfig.json");
        Write("Demo.deps.json");
        Write("Demo.pdb");
        Write("Martin.Runtime.dll");
        Write("Martin.Runtime.pdb");
        Write("extra.txt");

        var result = new BuildArtifactCollector().Collect(root, Options());

        Assert.True(result.Success);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.AppHost);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.ManagedAssembly);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.RuntimeConfiguration);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.DependencyManifest);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.PortablePdb);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.RuntimeLibrary);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.Other);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.Other && Path.GetFileName(a.Path) == "Martin.Runtime.pdb");
    }

    [Fact]
    public void Collect_does_not_require_app_host_or_pdb_when_disabled()
    {
        Write("Demo.dll");
        Write("Demo.runtimeconfig.json");
        Write("Demo.deps.json");
        Write("Martin.Runtime.dll");

        var result = new BuildArtifactCollector().Collect(root, Options() with { UseAppHost = false, EmitPortablePdb = false });

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Artifacts, a => a.Kind == BuildArtifactKind.AppHost);
        Assert.DoesNotContain(result.Artifacts, a => a.Kind == BuildArtifactKind.PortablePdb);
    }

    [Fact]
    public void Collect_reports_missing_required_artifact()
    {
        Write("Demo.dll");

        var result = new BuildArtifactCollector().Collect(root, Options());

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3100" && d.Message.Contains("RuntimeConfiguration", StringComparison.Ordinal));
    }

    [Fact]
    public void Collect_reports_duplicate_single_instance_artifact()
    {
        Write("Demo.dll");
        Write("Demo.exe");
        Write("Demo.runtimeconfig.json");
        Write("Demo.deps.json");
        Write("Demo.pdb");
        Write("Martin.Runtime.dll");
        Write("Demo");

        var result = new BuildArtifactCollector().Collect(root, Options());

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3101" && d.Message.Contains("AppHost", StringComparison.Ordinal));
    }

    [Fact]
    public void Collect_returns_stable_ordering_and_generated_artifacts()
    {
        Write("z.txt");
        Write("Demo.runtimeconfig.json");
        Write("Martin.Runtime.dll");
        Write("Demo.pdb");
        Write("Demo.deps.json");
        Write("Demo.dll");
        Write("Demo");
        var source = Write("Program.g.cs");
        var project = Write("GeneratedProgram.csproj");

        var first = new BuildArtifactCollector().Collect(root, Options(), source, project);
        var second = new BuildArtifactCollector().Collect(root, Options(), source, project);

        Assert.True(first.Success);
        Assert.Equal(first.Artifacts.Select(a => (a.Kind, a.Path)), second.Artifacts.Select(a => (a.Kind, a.Path)));
        Assert.Equal(first.Artifacts.Select(a => a.Path).Order(StringComparer.Ordinal), first.Artifacts.Select(a => a.Path));
        Assert.Contains(first.Artifacts, a => a.Kind == BuildArtifactKind.GeneratedSource);
        Assert.Contains(first.Artifacts, a => a.Kind == BuildArtifactKind.GeneratedProject);
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
