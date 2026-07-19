using System.Collections.Immutable;
using System.Text.Json;
using Martin.Build;
using Martin.Cli;
using Martin.CommandLine;
using Martin.Compiler;
using Martin.Compiler.Diagnostics;
using Martin.ProjectSystem;
using Xunit;

namespace Martin.Cli.Tests;

public sealed class CommandHandlerTests
{
    [Fact]
    public async Task BuildHandlerUsesInjectedServicesAndWritesSuccess()
    {
        var console = new TestConsole();
        var project = Project();
        var loader = new StubProjectLoader(new ProjectLoadResult { Project = project });
        var build = new StubBuildService(new BuildResult { Success = true, OutputDirectory = "/tmp/out" });
        var handler = new BuildCommandHandler(console, Diagnostics(console), loader, build);

        var exitCode = await handler.HandleAsync(new BuildCommandOptions(null, null, null, BuildConfiguration.Debug, Verbosity.Normal, KeepGenerated: true), CancellationToken.None);

        Assert.Equal((int)MartinExitCode.Success, exitCode);
        Assert.Single(build.Calls);
        Assert.Equal(Path.Combine(project.RootDirectory, "bin", "Debug", "net8.0"), build.Calls[0].OutputDirectory);
        Assert.True(build.Calls[0].KeepGeneratedFiles);
        Assert.True(build.Calls[0].UseAppHost);
        Assert.True(build.Calls[0].EmitPortablePdb);
        Assert.Contains("Building sample 1.0.0", console.StandardOutput);
        Assert.Contains("Build succeeded", console.StandardOutput);
        Assert.Equal(string.Empty, console.StandardError);
    }

    [Fact]
    public async Task BuildHandlerMapsCompilationDiagnosticsToCompilationFailed()
    {
        var console = new TestConsole();
        var loader = new StubProjectLoader(new ProjectLoadResult { Project = Project() });
        var diagnostic = new Diagnostic("MRT2001", DiagnosticSeverity.Error, "Bad source", default);
        var build = new StubBuildService(new BuildResult { Diagnostics = ImmutableArray.Create(diagnostic) });
        var handler = new BuildCommandHandler(console, Diagnostics(console), loader, build);

        var exitCode = await handler.HandleAsync(new BuildCommandOptions(null, null, null, BuildConfiguration.Debug, Verbosity.Quiet, KeepGenerated: false), CancellationToken.None);

        Assert.Equal((int)MartinExitCode.CompilationFailed, exitCode);
        Assert.Contains("MRT2001", console.StandardError);
    }

    [Fact]
    public async Task BuildHandlerSuppressesStatusOutputInJsonMode()
    {
        var console = new TestConsole();
        var project = Project();
        var loader = new StubProjectLoader(new ProjectLoadResult { Project = project });
        var build = new StubBuildService(new BuildResult { Success = true, OutputDirectory = "/tmp/out" });
        var handler = new BuildCommandHandler(console, Diagnostics(console), loader, build);

        var exitCode = await handler.HandleAsync(new BuildCommandOptions(null, null, null, BuildConfiguration.Debug, Verbosity.Normal, KeepGenerated: false, DiagnosticFormat: DiagnosticFormat.Json), CancellationToken.None);

        Assert.Equal((int)MartinExitCode.Success, exitCode);
        Assert.Equal(string.Empty, console.StandardOutput);
        Assert.Equal(string.Empty, console.StandardError);
    }

    [Fact]
    public async Task BuildHandlerJsonFailureEmitsOnlyJsonEnvelope()
    {
        var console = new TestConsole();
        var loader = new StubProjectLoader(new ProjectLoadResult { Project = Project() });
        var diagnostic = new Diagnostic("MRT2001", DiagnosticSeverity.Error, "Bad source", default);
        var build = new StubBuildService(new BuildResult { Diagnostics = ImmutableArray.Create(diagnostic) });
        var handler = new BuildCommandHandler(console, Diagnostics(console), loader, build);

        var exitCode = await handler.HandleAsync(new BuildCommandOptions(null, null, null, BuildConfiguration.Debug, Verbosity.Normal, KeepGenerated: false, DiagnosticFormat: DiagnosticFormat.Json, Color: ColorMode.Always), CancellationToken.None);

        Assert.Equal((int)MartinExitCode.CompilationFailed, exitCode);
        Assert.Equal(string.Empty, console.StandardOutput);
        Assert.DoesNotContain("Building", console.StandardError);
        Assert.DoesNotContain("Build succeeded", console.StandardError);
        // Assert.DoesNotContain("\u001b[", console.StandardError);
        using var document = JsonDocument.Parse(console.StandardError);
        Assert.True(document.RootElement.TryGetProperty("diagnostics", out var diagnostics));
        Assert.Equal(JsonValueKind.Array, diagnostics.ValueKind);
    }

    [Fact]
    public void CleanHandlerScopesDryRunToRequestedConfiguration()
    {
        var console = new TestConsole();
        var project = Project();
        var handler = new CleanCommandHandler(console, Diagnostics(console), new StubProjectLoader(new ProjectLoadResult { Project = project }), new MartinProjectCleaner());

        var exitCode = handler.Handle(new CleanCommandOptions(null, null, DryRun: true, Verbose: false, Configuration: "release"));

        Assert.Equal((int)MartinExitCode.Success, exitCode);
        Assert.Contains(Path.Combine(project.RootDirectory, "bin", "Release", "net8.0"), console.StandardOutput);
        Assert.Contains(Path.Combine(project.RootDirectory, "obj", "Release", "net8.0"), console.StandardOutput);
        Assert.DoesNotContain(Path.Combine(project.RootDirectory, "bin", "Debug", "net8.0"), console.StandardOutput);
    }

    [Fact]
    public void FormatHandlerRejectsMissingFileWithoutStartingProcess()
    {
        var console = new TestConsole();
        var handler = new FormatCommandHandler(console, Diagnostics(console), new StubProjectLoader(new ProjectLoadResult { Project = Project() }), new Martin.LanguageServices.MartinLanguageService());
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".martin");

        var exitCode = handler.Handle(new FormatCommandOptions(null, null, Check: false, Stdout: false, [missing]));

        Assert.Equal((int)MartinExitCode.InvalidArguments, exitCode);
        Assert.Contains("MRT4601", console.StandardError);
    }

    [Fact]
    public void VersionHandlerUsesInjectedVersionProvider()
    {
        var console = new TestConsole();
        var handler = new VersionCommandHandler(console, new StubVersions());

        var exitCode = handler.Handle(new VersionCommandOptions());

        Assert.Equal((int)MartinExitCode.Success, exitCode);
        Assert.Contains("Martin CLI: cli-test", console.StandardOutput);
        Assert.Contains("Martin compiler: compiler-test", console.StandardOutput);
        Assert.Contains("Language version: language-test", console.StandardOutput);
        Assert.Contains("Manifest version: manifest-test", console.StandardOutput);
    }

    static CommandDiagnosticService Diagnostics(TestConsole console) => new(console, new HumanDiagnosticRenderer());

    static MartinProject Project() => new() {
        RootDirectory = Path.Combine(Path.GetTempPath(), "martin-cli-handler-tests"),
        ManifestPath = Path.Combine(Path.GetTempPath(), "martin-cli-handler-tests", "Martin.toml"),
        Manifest = new MartinManifest {
            ManifestVersion = 1,
            Package = new PackageSection("sample", "1.0.0"),
            Target = new TargetSection("exe", "net8.0", "main")
        },
        SourceFiles = ImmutableArray.Create(Path.Combine(Path.GetTempPath(), "main.martin")), TestFiles = []
    };

    sealed class StubProjectLoader(ProjectLoadResult result) : IProjectLoaderService
    {
        public ProjectLoadResult Load(string? projectPath, string? manifestPath, CancellationToken cancellationToken = default) => result;
    }

    sealed class StubBuildService(BuildResult result) : ICliBuildService
    {
        public List<(string OutputDirectory, BuildConfiguration Configuration, bool KeepGeneratedFiles, bool UseAppHost, bool EmitPortablePdb)> Calls { get; } = [];
        public Task<BuildResult> BuildProjectAsync(MartinProject project, string outputDirectory, BuildConfiguration configuration, bool keepGeneratedFiles, bool useAppHost, bool emitPortablePdb, CancellationToken cancellationToken)
        {
            Calls.Add((outputDirectory, configuration, keepGeneratedFiles, useAppHost, emitPortablePdb));
            return Task.FromResult(result);
        }
    }

    sealed class StubVersions : IVersionProvider
    {
        public string MartinCliVersion => "cli-test";
        public string MartinCompilerVersion => "compiler-test";
        public string MartinRuntimeVersion => "runtime-test";
        public string LanguageVersion => "language-test";
        public string ManifestVersion => "manifest-test";
        public string TargetFramework => "tfm-test";
    }

    sealed class TestConsole : IMartinConsole
    {
        readonly StringWriter output = new();
        readonly StringWriter error = new();
        public TextWriter Out => output;
        public TextWriter Error => error;
        public bool IsOutputRedirected => true;
        public bool IsErrorRedirected => true;
        public bool SupportsColor => false;
        public string StandardOutput => output.ToString();
        public string StandardError => error.ToString();
    }
}
