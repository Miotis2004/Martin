using Martin.Build.BuildState;
using Xunit;

namespace Martin.Build.Tests;

public sealed class BuildStateStoreTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "MartinBuildStateStoreTests", Guid.NewGuid().ToString("N"));

    public BuildStateStoreTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void CreateDocument_records_inputs_and_artifacts_in_stable_order()
    {
        var manifest = Write("Martin.toml", "[package]\nname = \"Demo\"\n");
        var sourceB = Write(Path.Combine("Sources", "b.martin"), "func b() {}\n");
        var sourceA = Write(Path.Combine("Sources", "a.martin"), "func a() {}\n");
        var output = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
        var app = Write(Path.Combine("bin", OperatingSystem.IsWindows() ? "Demo.exe" : "Demo"), "app");
        var dll = Write(Path.Combine("bin", "Demo.dll"), "dll");
        var runtime = Write(Path.Combine("bin", "Martin.Runtime.dll"), "runtime");

        var document = new BuildStateStore().CreateDocument(new BuildStateInputs {
            ProjectRoot = root,
            ManifestPath = manifest,
            SourceFiles = [sourceB, sourceA],
            CompilerVersion = "compiler",
            RuntimeVersion = "runtime",
            Configuration = BuildConfiguration.Release,
            TargetFramework = "net8.0",
            AssemblyName = "Demo"
        },
                                                            new BuildResult { Success = true, OutputDirectory = output, EntryPointPath = app, Artifacts = [new(BuildArtifactKind.ManagedAssembly, dll), new(BuildArtifactKind.AppHost, app), new(BuildArtifactKind.RuntimeLibrary, runtime)] });

        Assert.Equal(BuildStateDocument.CurrentSchemaVersion, document.SchemaVersion);
        Assert.Equal("Sources/a.martin", document.Sources[0].RelativePath);
        Assert.Equal("Sources/b.martin", document.Sources[1].RelativePath);
        Assert.Equal(3, document.Artifacts.Length);
        Assert.Equal(OperatingSystem.IsWindows() ? "Demo.exe" : "Demo", document.EntryPointPath);
        Assert.False(string.IsNullOrWhiteSpace(document.ManifestSha256));
        Assert.False(string.IsNullOrWhiteSpace(document.RuntimeSha256));
    }

    [Fact]
    public void Write_replaces_state_atomically_with_serialized_schema()
    {
        var output = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
        var document = new BuildStateDocument {
            ProjectRoot = root,
            ManifestPath = Path.Combine(root, "Martin.toml"),
            ManifestSha256 = "abc",
            CompilerVersion = "compiler",
            RuntimeVersion = "runtime",
            RuntimeSha256 = "def",
            Configuration = BuildConfiguration.Debug,
            TargetFramework = "net8.0",
            AssemblyName = "Demo",
            EntryPointPath = "Demo"
        };

        var result = new BuildStateStore().Write(document, output);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(output, BuildStateStore.FileName)));
        Assert.Contains("\"schemaVersion\": 1", File.ReadAllText(Path.Combine(output, BuildStateStore.FileName)));
    }

    [Fact]
    public void Freshness_checker_accepts_current_state_and_reports_entry_point()
    {
        var manifest = Write("Martin.toml", "[package]\nname = \"Demo\"\n");
        var source = Write(Path.Combine("Sources", "main.martin"), "func main() {}\n");
        var output = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
        var app = Write(Path.Combine("bin", OperatingSystem.IsWindows() ? "Demo.exe" : "Demo"), "app");
        var runtime = Write(Path.Combine("bin", "Martin.Runtime.dll"), "runtime");
        var store = new BuildStateStore();
        var inputs = new BuildStateInputs { ProjectRoot = root, ManifestPath = manifest, SourceFiles = [source], CompilerVersion = "compiler", RuntimeVersion = "runtime", Configuration = BuildConfiguration.Debug, TargetFramework = "net8.0", AssemblyName = "Demo" };
        var document = store.CreateDocument(inputs, new BuildResult { Success = true, OutputDirectory = output, EntryPointPath = app, Artifacts = [new(BuildArtifactKind.AppHost, app), new(BuildArtifactKind.RuntimeLibrary, runtime)] });
        store.Write(document, output);

        var result = new BuildFreshnessChecker().Check(output, ToCheckInputs(inputs) with { RuntimeSha256 = document.RuntimeSha256 });

        Assert.True(result.IsFresh);
        Assert.Equal(app, result.EntryPointPath);
    }

    [Fact]
    public void Freshness_checker_rejects_current_runtime_hash_mismatch()
    {
        var manifest = Write("Martin.toml", "[package]\nname = \"Demo\"\n");
        var source = Write(Path.Combine("Sources", "main.martin"), "func main() {}\n");
        var output = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
        var app = Write(Path.Combine("bin", OperatingSystem.IsWindows() ? "Demo.exe" : "Demo"), "app");
        var runtime = Write(Path.Combine("bin", "Martin.Runtime.dll"), "runtime");
        var inputs = new BuildStateInputs { ProjectRoot = root, ManifestPath = manifest, SourceFiles = [source], CompilerVersion = "compiler", RuntimeVersion = "runtime", Configuration = BuildConfiguration.Debug, TargetFramework = "net8.0", AssemblyName = "Demo" };
        var store = new BuildStateStore();
        var document = store.CreateDocument(inputs, new BuildResult { Success = true, OutputDirectory = output, EntryPointPath = app, Artifacts = [new(BuildArtifactKind.AppHost, app), new(BuildArtifactKind.RuntimeLibrary, runtime)] });
        store.Write(document, output);

        var result = new BuildFreshnessChecker().Check(output, ToCheckInputs(inputs) with { RuntimeSha256 = "different-runtime" });

        Assert.False(result.IsFresh);
        Assert.Equal(BuildFreshnessStatus.RuntimeChanged, result.Status);
        Assert.Equal("runtime changed", result.Reason);
    }

    [Fact]
    public void Freshness_checker_rejects_changed_source()
    {
        var manifest = Write("Martin.toml", "[package]\nname = \"Demo\"\n");
        var source = Write(Path.Combine("Sources", "main.martin"), "func main() {}\n");
        var output = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
        var app = Write(Path.Combine("bin", OperatingSystem.IsWindows() ? "Demo.exe" : "Demo"), "app");
        var inputs = new BuildStateInputs { ProjectRoot = root, ManifestPath = manifest, SourceFiles = [source], CompilerVersion = "compiler", RuntimeVersion = "runtime", Configuration = BuildConfiguration.Debug, TargetFramework = "net8.0", AssemblyName = "Demo" };
        var store = new BuildStateStore();
        store.Write(store.CreateDocument(inputs, new BuildResult { Success = true, OutputDirectory = output, EntryPointPath = app, Artifacts = [new(BuildArtifactKind.AppHost, app)] }), output);
        File.WriteAllText(source, "func main() { print(1) }\n");

        var result = new BuildFreshnessChecker().Check(output, ToCheckInputs(inputs));

        Assert.False(result.IsFresh);
        Assert.Equal(BuildFreshnessStatus.SourceChanged, result.Status);
        Assert.Contains("Sources/main.martin", result.Reason);
    }

    [Fact]
    public void Freshness_checker_rejects_configuration_mismatch_and_missing_entry_point()
    {
        var manifest = Write("Martin.toml", "[package]\nname = \"Demo\"\n");
        var source = Write(Path.Combine("Sources", "main.martin"), "func main() {}\n");
        var output = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
        var app = Write(Path.Combine("bin", OperatingSystem.IsWindows() ? "Demo.exe" : "Demo"), "app");
        var inputs = new BuildStateInputs { ProjectRoot = root, ManifestPath = manifest, SourceFiles = [source], CompilerVersion = "compiler", RuntimeVersion = "runtime", Configuration = BuildConfiguration.Debug, TargetFramework = "net8.0", AssemblyName = "Demo" };
        var store = new BuildStateStore();
        store.Write(store.CreateDocument(inputs, new BuildResult { Success = true, OutputDirectory = output, EntryPointPath = app, Artifacts = [new(BuildArtifactKind.AppHost, app)] }), output);

        var releaseInputs = ToCheckInputs(inputs) with { Configuration = BuildConfiguration.Release };
        Assert.Equal(BuildFreshnessStatus.ConfigurationChanged, new BuildFreshnessChecker().Check(output, releaseInputs).Status);

        File.Delete(app);
        Assert.Equal(BuildFreshnessStatus.ArtifactMissing, new BuildFreshnessChecker().Check(output, ToCheckInputs(inputs)).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../outside/app.exe")]
    [InlineData("../../outside.dll")]
    [InlineData("C:/outside/app.exe")]
    [InlineData("C:\\outside\\app.exe")]
    [InlineData("/opt/outside/app")]
    [InlineData("//server/share/app.exe")]
    [InlineData("nested/../Demo")]
    [InlineData("nested//Demo")]
    [InlineData(".")]
    public void Freshness_checker_rejects_invalid_entry_point_state_paths(string entryPointPath)
    {
        var manifest = Write("Martin.toml", "[package]\nname = \"Demo\"\n");
        var source = Write(Path.Combine("Sources", "main.martin"), "func main() {}\n");
        var output = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
        var app = Write(Path.Combine("bin", OperatingSystem.IsWindows() ? "Demo.exe" : "Demo"), "app");
        var store = new BuildStateStore();
        var inputs = new BuildStateInputs { ProjectRoot = root, ManifestPath = manifest, SourceFiles = [source], CompilerVersion = "compiler", RuntimeVersion = "runtime", Configuration = BuildConfiguration.Debug, TargetFramework = "net8.0", AssemblyName = "Demo" };
        var document = store.CreateDocument(inputs, new BuildResult { Success = true, OutputDirectory = output, EntryPointPath = app, Artifacts = [new(BuildArtifactKind.AppHost, app)] }) with { EntryPointPath = entryPointPath };
        store.Write(document, output);

        var result = new BuildFreshnessChecker().Check(output, ToCheckInputs(inputs));

        Assert.False(result.IsFresh);
        Assert.Equal(BuildFreshnessStatus.InvalidState, result.Status);
    }

    [Theory]
    [InlineData("../outside/app.exe")]
    [InlineData("C:/outside/app.exe")]
    [InlineData("/opt/outside/app")]
    [InlineData("//server/share/app.exe")]
    [InlineData("nested/../Demo")]
    public void Freshness_checker_rejects_invalid_artifact_state_paths(string artifactPath)
    {
        var manifest = Write("Martin.toml", "[package]\nname = \"Demo\"\n");
        var source = Write(Path.Combine("Sources", "main.martin"), "func main() {}\n");
        var output = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
        var app = Write(Path.Combine("bin", OperatingSystem.IsWindows() ? "Demo.exe" : "Demo"), "app");
        var store = new BuildStateStore();
        var inputs = new BuildStateInputs { ProjectRoot = root, ManifestPath = manifest, SourceFiles = [source], CompilerVersion = "compiler", RuntimeVersion = "runtime", Configuration = BuildConfiguration.Debug, TargetFramework = "net8.0", AssemblyName = "Demo" };
        var document = store.CreateDocument(inputs, new BuildResult { Success = true, OutputDirectory = output, EntryPointPath = app, Artifacts = [new(BuildArtifactKind.AppHost, app)] }) with {
            Artifacts = [new BuildArtifactState { Kind = BuildArtifactKind.AppHost, RelativePath = artifactPath, Sha256 = "abc", Length = 3 }]
        };
        store.Write(document, output);

        var result = new BuildFreshnessChecker().Check(output, ToCheckInputs(inputs));

        Assert.False(result.IsFresh);
        Assert.Equal(BuildFreshnessStatus.InvalidState, result.Status);
    }

    [Fact]
    public async Task Create_write_and_freshness_honor_cancellation()
    {
        var manifest = Write("Martin.toml", "[package]\nname = \"Demo\"\n");
        var source = Write(Path.Combine("Sources", "main.martin"), "func main() {}\n");
        var output = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
        var app = Write(Path.Combine("bin", OperatingSystem.IsWindows() ? "Demo.exe" : "Demo"), "app");
        var inputs = new BuildStateInputs { ProjectRoot = root, ManifestPath = manifest, SourceFiles = [source], CompilerVersion = "compiler", RuntimeVersion = "runtime", Configuration = BuildConfiguration.Debug, TargetFramework = "net8.0", AssemblyName = "Demo" };
        var store = new BuildStateStore();
        var buildResult = new BuildResult { Success = true, OutputDirectory = output, EntryPointPath = app, Artifacts = [new(BuildArtifactKind.AppHost, app)] };
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.Throws<OperationCanceledException>(() => store.CreateDocument(inputs, buildResult, cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => store.Write(new BuildStateDocument { ProjectRoot = root, ManifestPath = manifest, ManifestSha256 = "abc", CompilerVersion = "compiler", RuntimeVersion = "runtime", RuntimeSha256 = "runtime", Configuration = BuildConfiguration.Debug, TargetFramework = "net8.0", AssemblyName = "Demo", EntryPointPath = "Demo" }, output, cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => new BuildFreshnessChecker().Check(output, ToCheckInputs(inputs), cancellation.Token));
    }

    static BuildFreshnessCheckInputs ToCheckInputs(BuildStateInputs inputs) => new() {
        ProjectRoot = inputs.ProjectRoot,
        ManifestPath = inputs.ManifestPath,
        SourceFiles = inputs.SourceFiles,
        CompilerVersion = inputs.CompilerVersion,
        RuntimeVersion = inputs.RuntimeVersion,
        RuntimeSha256 = string.Empty,
        Configuration = inputs.Configuration,
        TargetFramework = inputs.TargetFramework,
        AssemblyName = inputs.AssemblyName
    };

    string Write(string relativePath, string contents)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }
}
