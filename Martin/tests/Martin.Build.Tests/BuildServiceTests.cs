using Martin.Build;
using Martin.CodeGeneration;
using Martin.Build.BuildState;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Build.Tests;

public sealed class BuildServiceTests
{
    [Fact]
    public async Task Build_rejects_invalid_assembly_name_before_emitting_project()
    {
        var compilation = Compilation.Create(SyntaxTree.Parse("func main() { }", "main.martin"));

        var result = await new MartinBuildService().BuildAsync(compilation, new BuildOptions
        {
            AssemblyName = "not valid",
            OutputDirectory = Path.Combine(Path.GetTempPath(), "MartinBuildTests", Guid.NewGuid().ToString("N"))
        });

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MRT3016");
    }

    [Fact]
    public async Task Build_rejects_library_output_with_stable_unsupported_diagnostic()
    {
        var runner = new RecordingBuildRunner(new DotNetBuildResult { Started = true });
        var result = await new MartinBuildService(runner).BuildAsync(
            Compilation.Create(SyntaxTree.Parse("func helper() { }", "library.martin")),
            new BuildOptions
            {
                AssemblyName = "DeferredLibrary",
                OutputDirectory = Path.Combine(Path.GetTempPath(), "MartinBuildTests", Guid.NewGuid().ToString("N")),
                OutputKind = OutputKind.Library
            });

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "MRT3014" &&
            diagnostic.Message.Contains("Library", StringComparison.Ordinal));
        Assert.Null(runner.Request);
    }

    [Fact]
    public async Task Build_delegates_dotnet_process_to_runner()
    {
        var compilation = Compilation.Create(SyntaxTree.Parse("func main() { }", "main.martin"));
        var runner = new RecordingBuildRunner(new DotNetBuildResult
        {
            Started = false,
            StandardError = "startup failed"
        });

        var result = await new MartinBuildService(runner).BuildAsync(compilation, new BuildOptions
        {
            AssemblyName = "DelegationTest",
            OutputDirectory = Path.Combine(Path.GetTempPath(), "MartinBuildTests", Guid.NewGuid().ToString("N")),
            Configuration = BuildConfiguration.Release,
            KeepGeneratedFiles = true
        });

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MRT3011");
        Assert.NotNull(runner.Request);
        Assert.Equal(BuildConfiguration.Release, runner.Request.Configuration);
        Assert.Equal(Path.GetDirectoryName(runner.Request.ProjectPath), runner.Request.WorkingDirectory);
    }


    [Fact]
    public async Task Build_reports_cancelled_when_cancellation_is_requested_before_source_write()
    {
        var compilation = Compilation.Create(SyntaxTree.Parse("func main() { }", "main.martin"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await new MartinBuildService(new RecordingBuildRunner(new DotNetBuildResult { Started = true })).BuildAsync(compilation, new BuildOptions
        {
            AssemblyName = "CancelledBeforeWrite",
            OutputDirectory = Path.Combine(Path.GetTempPath(), "MartinBuildTests", Guid.NewGuid().ToString("N"))
        }, cancellation.Token);

        Assert.Equal(BuildStatus.Cancelled, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MRT3010");
    }

    [Fact]
    public async Task Build_cancellation_removes_work_and_staging_and_preserves_existing_output()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "MartinBuildTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        var existingOutput = Path.Combine(outputDirectory, "previous.txt");
        await File.WriteAllTextAsync(existingOutput, "previous successful output");

        var compilation = Compilation.Create(SyntaxTree.Parse("func main() { }", "main.martin"));
        var runner = new RecordingBuildRunner(new DotNetBuildResult
        {
            Started = true,
            WasCancelled = true,
            StandardOutput = "partial stdout",
            StandardError = "partial stderr"
        });

        var result = await new MartinBuildService(runner).BuildAsync(compilation, new BuildOptions
        {
            AssemblyName = "CancelledDuringDotNetBuild",
            OutputDirectory = outputDirectory,
            KeepGeneratedFiles = true
        });

        Assert.Equal(BuildStatus.Cancelled, result.Status);
        Assert.Equal("partial stdout", result.StandardOutput);
        Assert.Equal("partial stderr", result.StandardError);
        Assert.True(File.Exists(existingOutput));
        Assert.Equal("previous successful output", await File.ReadAllTextAsync(existingOutput));
        Assert.NotNull(runner.Request);
        Assert.False(Directory.Exists(runner.Request.WorkingDirectory));
    }


    [Fact]
    public async Task Build_publishes_valid_staging_output_on_first_successful_build()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "MartinBuildTests", Guid.NewGuid().ToString("N"));
        var runner = new PublishingBuildRunner();

        var result = await new MartinBuildService(runner).BuildAsync(Compilation.Create(SyntaxTree.Parse("func main() { }", "main.martin")), new BuildOptions
        {
            AssemblyName = "FirstPublish",
            OutputDirectory = outputDirectory,
            UseAppHost = false
        });

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(outputDirectory, "FirstPublish.dll")));
        Assert.True(File.Exists(Path.Combine(outputDirectory, "FirstPublish.deps.json")));
        Assert.True(File.Exists(Path.Combine(outputDirectory, "FirstPublish.runtimeconfig.json")));
        Assert.Contains(result.Artifacts, artifact => artifact.Path.StartsWith(outputDirectory, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Build_writes_project_build_state_before_publishing_staging()
    {
        var projectRoot = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "MartinBuildTests", Guid.NewGuid().ToString("N"))).FullName;
        var manifest = Path.Combine(projectRoot, "Martin.toml");
        var source = Path.Combine(projectRoot, "Sources", "main.martin");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        await File.WriteAllTextAsync(manifest, "[package]\nname = \"StatePublish\"\n");
        await File.WriteAllTextAsync(source, "func main() { }\n");
        var outputDirectory = Path.Combine(projectRoot, "bin");

        var result = await new MartinBuildService(new PublishingBuildRunner()).BuildAsync(
            Compilation.Create(SyntaxTree.Parse(await File.ReadAllTextAsync(source), source)),
            new BuildOptions
            {
                AssemblyName = "StatePublish",
                OutputDirectory = outputDirectory,
                UseAppHost = false,
                BuildStateInputs = new ProjectBuildStateInputs
                {
                    ProjectRoot = projectRoot,
                    ManifestPath = manifest,
                    SourceFiles = [source],
                    CompilerVersion = "compiler",
                    RuntimeVersion = "runtime"
                }
            });

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(outputDirectory, BuildStateStore.FileName)));
        var state = await File.ReadAllTextAsync(Path.Combine(outputDirectory, BuildStateStore.FileName));
        Assert.Contains("\"entryPointPath\": \"StatePublish.dll\"", state);
        Assert.Contains("\"relativePath\": \"StatePublish.dll\"", state);
    }

    [Fact]
    public async Task Build_state_creation_failure_preserves_previous_output()
    {
        var projectRoot = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "MartinBuildTests", Guid.NewGuid().ToString("N"))).FullName;
        var source = Path.Combine(projectRoot, "Sources", "main.martin");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        await File.WriteAllTextAsync(source, "func main() { }\n");
        var outputDirectory = Path.Combine(projectRoot, "bin");
        Directory.CreateDirectory(outputDirectory);
        var previous = Path.Combine(outputDirectory, "previous.txt");
        await File.WriteAllTextAsync(previous, "previous successful output");

        var result = await new MartinBuildService(new PublishingBuildRunner()).BuildAsync(
            Compilation.Create(SyntaxTree.Parse(await File.ReadAllTextAsync(source), source)),
            new BuildOptions
            {
                AssemblyName = "StateFailure",
                OutputDirectory = outputDirectory,
                UseAppHost = false,
                BuildStateInputs = new ProjectBuildStateInputs
                {
                    ProjectRoot = projectRoot,
                    ManifestPath = Path.Combine(projectRoot, "missing-Martin.toml"),
                    SourceFiles = [source],
                    CompilerVersion = "compiler",
                    RuntimeVersion = "runtime"
                }
            });

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MRT4515");
        Assert.True(File.Exists(previous));
        Assert.Equal("previous successful output", await File.ReadAllTextAsync(previous));
        Assert.False(File.Exists(Path.Combine(outputDirectory, "StateFailure.dll")));
        Assert.False(File.Exists(Path.Combine(outputDirectory, BuildStateStore.FileName)));
    }

    [Fact]
    public async Task Build_replaces_output_directory_instead_of_copying_over_stale_files()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "MartinBuildTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "stale.txt"), "old");
        var runner = new PublishingBuildRunner();

        var result = await new MartinBuildService(runner).BuildAsync(Compilation.Create(SyntaxTree.Parse("func main() { }", "main.martin")), new BuildOptions
        {
            AssemblyName = "ReplacementPublish",
            OutputDirectory = outputDirectory,
            UseAppHost = false
        });

        Assert.True(result.Success);
        Assert.False(File.Exists(Path.Combine(outputDirectory, "stale.txt")));
        Assert.True(File.Exists(Path.Combine(outputDirectory, "ReplacementPublish.dll")));
    }

    [Fact]
    public async Task Build_does_not_publish_when_staged_artifact_validation_fails()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "MartinBuildTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        var previous = Path.Combine(outputDirectory, "previous.txt");
        await File.WriteAllTextAsync(previous, "previous successful output");
        var runner = new PublishingBuildRunner(omitDependencyManifest: true);

        var result = await new MartinBuildService(runner).BuildAsync(Compilation.Create(SyntaxTree.Parse("func main() { }", "main.martin")), new BuildOptions
        {
            AssemblyName = "ValidationFailure",
            OutputDirectory = outputDirectory,
            UseAppHost = false
        });

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code is "MRT3100" or "MRT3200");
        Assert.True(File.Exists(previous));
        Assert.Equal("previous successful output", await File.ReadAllTextAsync(previous));
        Assert.False(File.Exists(Path.Combine(outputDirectory, "ValidationFailure.dll")));
    }

    sealed class RecordingBuildRunner(DotNetBuildResult result) : IDotNetBuildRunner
    {
        public DotNetBuildRequest? Request { get; private set; }

        public Task<DotNetBuildResult> RunAsync(DotNetBuildRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }

    sealed class PublishingBuildRunner(bool omitDependencyManifest = false) : IDotNetBuildRunner
    {
        public DotNetBuildRequest? Request { get; private set; }

        public async Task<DotNetBuildResult> RunAsync(DotNetBuildRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            var outputPath = request.AdditionalArguments
                .Select(argument => argument.StartsWith("-p:OutputPath=", StringComparison.Ordinal) ? argument[14..] : null)
                .First(argument => argument is not null)!;
            Directory.CreateDirectory(outputPath);

            var assemblyName = ReadAssemblyName(request.ProjectPath);

            await File.WriteAllTextAsync(Path.Combine(outputPath, assemblyName + ".dll"), "assembly", cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(outputPath, assemblyName + ".runtimeconfig.json"), "{}", cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(outputPath, assemblyName + ".pdb"), "pdb", cancellationToken);
            if (!omitDependencyManifest)
                await File.WriteAllTextAsync(Path.Combine(outputPath, assemblyName + ".deps.json"), "{}", cancellationToken);

            var runtime = FindRuntime();
            File.Copy(runtime, Path.Combine(outputPath, "Martin.Runtime.dll"));

            return new DotNetBuildResult { Started = true, Completed = true, ExitCode = 0 };
        }

        static string ReadAssemblyName(string projectPath)
        {
            var text = File.ReadAllText(projectPath);
            var start = text.IndexOf("<AssemblyName>", StringComparison.Ordinal) + "<AssemblyName>".Length;
            var end = text.IndexOf("</AssemblyName>", start, StringComparison.Ordinal);
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
