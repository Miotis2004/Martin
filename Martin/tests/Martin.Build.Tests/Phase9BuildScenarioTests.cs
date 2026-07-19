using Martin.Build;
using Martin.CodeGeneration;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Build.Tests;

public sealed class Phase9BuildScenarioTests
{
    [Fact]
    public async Task SuccessfulBuildProducesValidatedArtifacts()
    {
        using var test = new BuildScenario();

        var result = await test.BuildAsync(useAppHost: false);

        Assert.True(result.Success);
        Assert.Equal(BuildStatus.Succeeded, result.Status);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.ManagedAssembly);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.DependencyManifest);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.RuntimeConfiguration);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.RuntimeLibrary);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.PortablePdb);
    }

    [Fact]
    public async Task CompilerErrorsPreventDotNetBuild()
    {
        using var test = new BuildScenario(source: "func main() { missing }");

        var result = await test.BuildAsync();

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code.StartsWith("MRT", StringComparison.Ordinal));
        Assert.Null(test.Runner.Request);
    }

    [Fact]
    public async Task DotNetBuildFailurePreservesOutput()
    {
        using var test = new BuildScenario(runner: new ScenarioBuildRunner { ExitCode = 1 });
        await File.WriteAllTextAsync(Path.Combine(test.OutputDirectory, "previous.txt"), "previous");

        var result = await test.BuildAsync();

        Assert.False(result.Success);
        Assert.True(File.Exists(Path.Combine(test.OutputDirectory, "previous.txt")));
        Assert.Equal("previous", await File.ReadAllTextAsync(Path.Combine(test.OutputDirectory, "previous.txt")));
    }

    [Fact]
    public async Task CancellationBeforeProcessStartReturnsCancelled()
    {
        using var test = new BuildScenario(runner: new ScenarioBuildRunner { CancelBeforeStart = true });

        var result = await test.BuildAsync();

        Assert.Equal(BuildStatus.Cancelled, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3010");
        Assert.NotNull(test.Runner.Request);
    }

    [Fact]
    public async Task CancellationDeletesTemporaryOutputAndPreservesPreviousOutput()
    {
        using var test = new BuildScenario(runner: new ScenarioBuildRunner { WasCancelled = true });
        await File.WriteAllTextAsync(Path.Combine(test.OutputDirectory, "previous.txt"), "previous");

        var result = await test.BuildAsync(keepGeneratedFiles: true);

        Assert.Equal(BuildStatus.Cancelled, result.Status);
        Assert.True(File.Exists(Path.Combine(test.OutputDirectory, "previous.txt")));
        Assert.NotNull(test.Runner.Request);
        Assert.False(Directory.Exists(test.Runner.Request.WorkingDirectory));
    }

    [Fact]
    public async Task StagingAndPublicationReplaceStaleArtifactsAtomically()
    {
        using var test = new BuildScenario();
        await File.WriteAllTextAsync(Path.Combine(test.OutputDirectory, "stale.txt"), "stale");

        var result = await test.BuildAsync(useAppHost: false);

        Assert.True(result.Success);
        Assert.False(File.Exists(Path.Combine(test.OutputDirectory, "stale.txt")));
        Assert.True(File.Exists(Path.Combine(test.OutputDirectory, test.AssemblyName + ".dll")));
        Assert.True(test.Runner.OutputPath is not null);
        Assert.NotEqual(Path.GetFullPath(test.OutputDirectory), Path.GetFullPath(test.Runner.OutputPath));
    }

    [Fact]
    public async Task InvalidArtifactsAreNotPublished()
    {
        using var test = new BuildScenario(runner: new ScenarioBuildRunner { OmitDependencyManifest = true });
        await File.WriteAllTextAsync(Path.Combine(test.OutputDirectory, "previous.txt"), "previous");

        var result = await test.BuildAsync(useAppHost: false);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code is "MRT3100" or "MRT3200");
        Assert.True(File.Exists(Path.Combine(test.OutputDirectory, "previous.txt")));
        Assert.False(File.Exists(Path.Combine(test.OutputDirectory, test.AssemblyName + ".dll")));
    }

    [Fact]
    public async Task AppHostEnabledProducesAppHost()
    {
        using var test = new BuildScenario();

        var result = await test.BuildAsync(useAppHost: true);

        Assert.True(result.Success);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.AppHost);
        Assert.EndsWith(test.AssemblyName + (OperatingSystem.IsWindows() ? ".exe" : string.Empty), result.EntryPointPath!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AppHostDisabledUsesManagedAssembly()
    {
        using var test = new BuildScenario();

        var result = await test.BuildAsync(useAppHost: false);

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Artifacts, a => a.Kind == BuildArtifactKind.AppHost);
        Assert.EndsWith(test.AssemblyName + ".dll", result.EntryPointPath!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PdbOptionsControlPortablePdbPublication()
    {
        using var withPdb = new BuildScenario();
        using var withoutPdb = new BuildScenario();

        var debug = await withPdb.BuildAsync(emitPortablePdb: true, useAppHost: false);
        var omitted = await withoutPdb.BuildAsync(emitPortablePdb: false, useAppHost: false);

        Assert.True(debug.Success);
        Assert.Contains(debug.Artifacts, a => a.Kind == BuildArtifactKind.PortablePdb);
        Assert.True(omitted.Success);
        Assert.DoesNotContain(omitted.Artifacts, a => a.Kind == BuildArtifactKind.PortablePdb);
    }

    [Fact]
    public async Task ReleaseBuildProducesExpectedArtifacts()
    {
        using var test = new BuildScenario();

        var result = await test.BuildAsync(configuration: BuildConfiguration.Release, useAppHost: false);

        Assert.True(result.Success);
        Assert.Equal(BuildConfiguration.Release, test.Runner.Request!.Configuration);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.ManagedAssembly);
        Assert.Contains(result.Artifacts, a => a.Kind == BuildArtifactKind.DependencyManifest);
    }

    [Fact]
    public async Task GeneratedFilesRetentionFollowsOption()
    {
        using var retained = new BuildScenario();
        using var deleted = new BuildScenario();

        var kept = await retained.BuildAsync(keepGeneratedFiles: true, useAppHost: false);
        var notKept = await deleted.BuildAsync(keepGeneratedFiles: false, useAppHost: false);

        Assert.True(kept.Success);
        Assert.True(File.Exists(Path.Combine(retained.OutputDirectory, "Program.g.cs")));
        Assert.True(File.Exists(Path.Combine(retained.OutputDirectory, "GeneratedProgram.csproj")));
        Assert.True(notKept.Success);
        Assert.False(File.Exists(Path.Combine(deleted.OutputDirectory, "Program.g.cs")));
        Assert.False(File.Exists(Path.Combine(deleted.OutputDirectory, "GeneratedProgram.csproj")));
    }

    [Fact]
    public async Task GeneratedSourceAndArtifactOrderingAreDeterministic()
    {
        using var first = new BuildScenario();
        using var second = new BuildScenario();

        var firstResult = await first.BuildAsync(useAppHost: false);
        var secondResult = await second.BuildAsync(useAppHost: false);

        Assert.True(firstResult.Success);
        Assert.True(secondResult.Success);
        Assert.Equal(first.Runner.GeneratedSource, second.Runner.GeneratedSource);
        Assert.Equal(firstResult.Artifacts.Select(a => a.Kind), firstResult.Artifacts.OrderBy(a => a.Path, StringComparer.Ordinal).Select(a => a.Kind));
    }

    [Fact]
    public async Task RepeatedBuildsDoNotAccumulateStaleFiles()
    {
        using var test = new BuildScenario();

        var first = await test.BuildAsync(useAppHost: false);
        await File.WriteAllTextAsync(Path.Combine(test.OutputDirectory, "old.tmp"), "old");
        var second = await test.BuildAsync(useAppHost: false);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.False(File.Exists(Path.Combine(test.OutputDirectory, "old.tmp")));
    }

    [Fact]
    public async Task LibraryOutputIsExplicitlyRejected()
    {
        using var test = new BuildScenario();

        var result = await test.BuildAsync(outputKind: OutputKind.Library);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3014");
        Assert.Null(test.Runner.Request);
    }

    sealed class BuildScenario : IDisposable
    {
        readonly string root = Path.Combine(Path.GetTempPath(), "MartinBuildTests", Guid.NewGuid().ToString("N"));
        readonly Compilation compilation;

        public BuildScenario(string source = "func main() { }", ScenarioBuildRunner? runner = null)
        {
            AssemblyName = "Phase9Build" + Guid.NewGuid().ToString("N")[..8];
            OutputDirectory = Path.Combine(root, "out");
            Directory.CreateDirectory(OutputDirectory);
            compilation = Compilation.Create(SyntaxTree.Parse(source, "main.martin"));
            Runner = runner ?? new ScenarioBuildRunner();
        }

        public string AssemblyName { get; }
        public string OutputDirectory { get; }
        public ScenarioBuildRunner Runner { get; }

        public Task<BuildResult> BuildAsync(
            bool useAppHost = true,
            bool emitPortablePdb = true,
            bool keepGeneratedFiles = false,
            OutputKind outputKind = OutputKind.ConsoleApplication,
            BuildConfiguration configuration = BuildConfiguration.Debug) =>
            new MartinBuildService(Runner).BuildAsync(compilation, new BuildOptions
            {
                AssemblyName = AssemblyName,
                OutputDirectory = OutputDirectory,
                UseAppHost = useAppHost,
                EmitPortablePdb = emitPortablePdb,
                KeepGeneratedFiles = keepGeneratedFiles,
                OutputKind = outputKind,
                Configuration = configuration
            });

        public void Dispose()
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); } catch { }
        }
    }

    sealed class ScenarioBuildRunner : IDotNetBuildRunner
    {
        public DotNetBuildRequest? Request { get; private set; }
        public string? OutputPath { get; private set; }
        public string? GeneratedSource { get; private set; }
        public bool OmitDependencyManifest { get; init; }
        public bool WasCancelled { get; init; }
        public bool CancelBeforeStart { get; init; }
        public int ExitCode { get; init; }

        public async Task<DotNetBuildResult> RunAsync(DotNetBuildRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            GeneratedSource = await File.ReadAllTextAsync(Path.Combine(request.WorkingDirectory, "..", "generated", "Program.g.cs"), cancellationToken);

            if (CancelBeforeStart)
                return new DotNetBuildResult { Started = false, WasCancelled = true };
            if (WasCancelled)
                return new DotNetBuildResult { Started = true, WasCancelled = true };
            if (ExitCode != 0)
                return new DotNetBuildResult { Started = true, Completed = true, ExitCode = ExitCode, StandardError = "generated build failed" };

            OutputPath = request.AdditionalArguments.Single(a => a.StartsWith("-p:OutputPath=", StringComparison.Ordinal))[14..];
            Directory.CreateDirectory(OutputPath);

            var project = await File.ReadAllTextAsync(request.ProjectPath, cancellationToken);
            var assemblyName = Between(project, "<AssemblyName>", "</AssemblyName>");
            var useAppHost = bool.Parse(Between(project, "<UseAppHost>", "</UseAppHost>"));
            var emitPdb = Between(project, "<DebugType>", "</DebugType>") == "portable";

            await File.WriteAllTextAsync(Path.Combine(OutputPath, assemblyName + ".dll"), "assembly", cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(OutputPath, assemblyName + ".runtimeconfig.json"), "{}", cancellationToken);
            if (!OmitDependencyManifest)
                await File.WriteAllTextAsync(Path.Combine(OutputPath, assemblyName + ".deps.json"), "{}", cancellationToken);
            if (emitPdb)
                await File.WriteAllTextAsync(Path.Combine(OutputPath, assemblyName + ".pdb"), "pdb", cancellationToken);
            if (useAppHost)
                await File.WriteAllTextAsync(Path.Combine(OutputPath, assemblyName + (OperatingSystem.IsWindows() ? ".exe" : string.Empty)), "apphost", cancellationToken);

            File.Copy(FindRuntime(), Path.Combine(OutputPath, "Martin.Runtime.dll"));

            return new DotNetBuildResult { Started = true, Completed = true, ExitCode = 0 };
        }

        static string Between(string text, string startMarker, string endMarker)
        {
            var start = text.IndexOf(startMarker, StringComparison.Ordinal) + startMarker.Length;
            var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
            return text[start..end];
        }

        static string FindRuntime()
        {
            var baseDirectory = AppContext.BaseDirectory;
            var direct = Path.Combine(baseDirectory, "Martin.Runtime.dll");
            if (File.Exists(direct))
                return direct;
            return Directory.EnumerateFiles(Path.GetFullPath(Path.Combine(baseDirectory, "../../../../")), "Martin.Runtime.dll", SearchOption.AllDirectories).First();
        }
    }
}
