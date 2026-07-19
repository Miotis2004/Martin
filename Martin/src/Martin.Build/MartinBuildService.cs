using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Martin.CodeGeneration;
using Martin.Build.Artifacts;
using Martin.Build.BuildState;
using Martin.Build.Diagnostics;
using Martin.Compiler;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Text;

namespace Martin.Build;

public sealed class MartinBuildService : IMartinBuildService
{
    readonly IDotNetBuildRunner buildRunner;

    public MartinBuildService()
        : this(new DotNetBuildRunner())
    {
    }

    public MartinBuildService(IDotNetBuildRunner buildRunner)
    {
        this.buildRunner = buildRunner ?? throw new ArgumentNullException(nameof(buildRunner));
    }

    public async Task<BuildResult> BuildAsync(Compilation compilation, BuildOptions options, CancellationToken cancellationToken = default)
    {
        var validationDiagnostics = Validate(options);
        if (validationDiagnostics.Length > 0)
            return new() { Diagnostics = validationDiagnostics };

        var program = compilation.BindProgram();
        if (program.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            return new() { Diagnostics = program.Diagnostics };


        CodeGenerationResult emit;
        try
        {
            var loweredProgram = new CSharpLowerer().Lower(program, cancellationToken);
            emit = new CSharpEmitter().Emit(new() { Program = loweredProgram, AssemblyName = options.AssemblyName, OutputKind = options.OutputKind });
            if (!emit.Success)
                return new() { Diagnostics = emit.Diagnostics };
        }
        catch (OperationCanceledException)
        {
            return new() { Status = BuildStatus.Cancelled, Diagnostics = [BuildDiagnostics.BuildCancelled()] };
        }

        var work = Path.Combine(
            Path.GetTempPath(),
            "Martin",
            "builds",
            Guid.NewGuid().ToString("N"));

        var generated = Path.Combine(work, "generated");
        var project = Path.Combine(work, "project");
        var dotnetOutput = Path.Combine(work, "dotnet-output");
        var staging = Path.Combine(work, "staging");

        var src = Path.Combine(generated, "Program.g.cs");
        var proj = Path.Combine(project, "GeneratedProgram.csproj");

        // This must be outside and before the try block.
        var deleteWorkDirectory = !options.KeepGeneratedFiles;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            Directory.CreateDirectory(generated);
            Directory.CreateDirectory(project);
            Directory.CreateDirectory(dotnetOutput);
            Directory.CreateDirectory(staging);

            await File.WriteAllTextAsync(src, emit.GeneratedSource, cancellationToken);
            var runtimeDiscovery = new RuntimeDiscovery().Discover(options);
            if (!runtimeDiscovery.Success || runtimeDiscovery.Runtime is null)
                return new() { Diagnostics = runtimeDiscovery.Diagnostics };

            var runtime = runtimeDiscovery.Runtime;
            await File.WriteAllTextAsync(proj, ProjectXml(options, runtime.AssemblyPath), cancellationToken);

            var build = await buildRunner.RunAsync(
                new DotNetBuildRequest
                {
                    ProjectPath = proj,
                    WorkingDirectory = project,
                    Configuration = options.Configuration,
                    AdditionalArguments =
                    [
                        $"-p:OutputPath={EnsureTrailingSeparator(dotnetOutput)}",
                        "-p:AppendTargetFrameworkToOutputPath=false"
                    ]
                },
                cancellationToken);

            var stdout = build.StandardOutput;
            var stderr = build.StandardError;

            if (build.WasCancelled)
            {
                deleteWorkDirectory = true;
                return Cancelled(stdout, stderr);
            }

            if (!build.Started)
            {
                return new()
                {
                    Diagnostics = [Diag("MRT3011", "The .NET build process could not be started. Verify that a supported .NET SDK is installed and that 'dotnet' is available on PATH.")],
                    StandardOutput = stdout,
                    StandardError = stderr,
                    GeneratedSourcePath = options.KeepGeneratedFiles ? src : null,
                    GeneratedProjectPath = options.KeepGeneratedFiles ? proj : null
                };
            }

            if (!build.Completed || build.ExitCode != 0)
            {
                var generatedDiagnostics = new GeneratedDiagnosticParser().Parse(stdout + Environment.NewLine + stderr);
                var diagnostics = new GeneratedDiagnosticTranslator().Translate(generatedDiagnostics, emit.GeneratedSource, emit.SourceMap);
                if (diagnostics.IsDefaultOrEmpty)
                    diagnostics = [Diag("MRT3007", "The generated .NET project failed to build.")];

                return new()
                {
                    Diagnostics = diagnostics,
                    ProcessExitCode = build.ExitCode,
                    StandardOutput = stdout,
                    StandardError = stderr,
                    GeneratedSourcePath = options.KeepGeneratedFiles ? src : null,
                    GeneratedProjectPath = options.KeepGeneratedFiles ? proj : null
                };
            }

            CopyBuildOutputToStaging(dotnetOutput, staging);

            var stagedSource = options.KeepGeneratedFiles ? Path.Combine(staging, Path.GetFileName(src)) : null;
            var stagedProject = options.KeepGeneratedFiles ? Path.Combine(staging, Path.GetFileName(proj)) : null;
            if (stagedSource is not null)
                File.Copy(src, stagedSource, overwrite: false);
            if (stagedProject is not null)
                File.Copy(proj, stagedProject, overwrite: false);

            var collector = new BuildArtifactCollector().Collect(
                staging,
                options,
                stagedSource,
                stagedProject);
            if (!collector.Success)
                return new() { Diagnostics = collector.Diagnostics, StandardOutput = stdout, StandardError = stderr, ProcessExitCode = build.ExitCode };

            var validation = new BuildArtifactValidator().Validate(options, runtime with { StagingRoot = staging }, collector.Artifacts);
            if (!validation.Success)
                return new() { Diagnostics = validation.Diagnostics, StandardOutput = stdout, StandardError = stderr, ProcessExitCode = build.ExitCode };

            var stagedEntryPoint = collector.Artifacts.FirstOrDefault(a => a.Kind == BuildArtifactKind.AppHost)?.Path
                ?? collector.Artifacts.FirstOrDefault(a => a.Kind == BuildArtifactKind.ManagedAssembly)?.Path;
            var stagedResult = new BuildResult
            {
                Success = true,
                OutputDirectory = Path.GetFullPath(staging),
                EntryPointPath = stagedEntryPoint,
                GeneratedSourcePath = stagedSource,
                GeneratedProjectPath = stagedProject,
                ProcessExitCode = build.ExitCode,
                StandardOutput = stdout,
                StandardError = stderr,
                Artifacts = collector.Artifacts
            };

            if (options.BuildStateInputs is not null)
            {
                var stateDiagnostics = WriteBuildStateToStaging(options, stagedResult, staging, cancellationToken);
                if (stateDiagnostics.Length > 0)
                    return stagedResult with { Success = false, Diagnostics = stateDiagnostics };
            }

            var publication = PublishStaging(staging, options.OutputDirectory);
            if (publication.Length > 0)
                return new() { Diagnostics = publication, StandardOutput = stdout, StandardError = stderr, ProcessExitCode = build.ExitCode };

            ApplyExecutablePermissions(options.OutputDirectory, options);

            var outputArtifacts = new BuildArtifactCollector().Collect(
                options.OutputDirectory,
                options,
                options.KeepGeneratedFiles ? Path.Combine(options.OutputDirectory, Path.GetFileName(src)) : null,
                options.KeepGeneratedFiles ? Path.Combine(options.OutputDirectory, Path.GetFileName(proj)) : null).Artifacts;
            var entryPoint = outputArtifacts.FirstOrDefault(a => a.Kind == BuildArtifactKind.AppHost)?.Path
                ?? outputArtifacts.FirstOrDefault(a => a.Kind == BuildArtifactKind.ManagedAssembly)?.Path;

            return new()
            {
                Success = true,
                OutputDirectory = Path.GetFullPath(options.OutputDirectory),
                EntryPointPath = entryPoint,
                GeneratedSourcePath = options.KeepGeneratedFiles ? Path.Combine(options.OutputDirectory, Path.GetFileName(src)) : null,
                GeneratedProjectPath = options.KeepGeneratedFiles ? Path.Combine(options.OutputDirectory, Path.GetFileName(proj)) : null,
                ProcessExitCode = build.ExitCode,
                StandardOutput = stdout,
                StandardError = stderr,
                Artifacts = outputArtifacts
            };
        }
        catch (OperationCanceledException)
        {
            deleteWorkDirectory = true;
            return Cancelled();
        }
        finally
        {
            TryDelete(staging);

            if (deleteWorkDirectory)
                TryDelete(work);
        }
    }


    static ImmutableArray<Diagnostic> WriteBuildStateToStaging(BuildOptions options, BuildResult stagedResult, string staging, CancellationToken cancellationToken)
    {
        try
        {
            var inputs = options.BuildStateInputs!;
            var store = new BuildStateStore();
            var document = store.CreateDocument(new BuildStateInputs
            {
                ProjectRoot = inputs.ProjectRoot,
                ManifestPath = inputs.ManifestPath,
                SourceFiles = inputs.SourceFiles,
                CompilerVersion = inputs.CompilerVersion,
                RuntimeVersion = inputs.RuntimeVersion,
                Configuration = options.Configuration,
                TargetFramework = options.TargetFramework,
                AssemblyName = options.AssemblyName
            }, stagedResult, cancellationToken);
            var write = store.Write(document, staging, cancellationToken);
            return write.Diagnostics;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return [Diag("MRT4515", $"Build-state creation failed: {ex.Message}")];
        }
    }

    static void CopyBuildOutputToStaging(string built, string staging)
    {
        foreach (var file in Directory.EnumerateFiles(built).OrderBy(Path.GetFileName, StringComparer.Ordinal))
            File.Copy(file, Path.Combine(staging, Path.GetFileName(file)), overwrite: true);
    }

    static BuildResult Cancelled(string standardOutput = "", string standardError = "") => new()
    {
        Status = BuildStatus.Cancelled,
        Diagnostics = [Diag("MRT3010", "The .NET build process was cancelled.")],
        StandardOutput = standardOutput,
        StandardError = standardError
    };

    static ImmutableArray<Diagnostic> Validate(BuildOptions o){var b=ImmutableArray.CreateBuilder<Diagnostic>(); if(string.IsNullOrWhiteSpace(o.OutputDirectory))b.Add(Diag("MRT3008","The output directory '' could not be created.")); if(string.IsNullOrWhiteSpace(o.AssemblyName)||!Regex.IsMatch(o.AssemblyName,"^[A-Za-z_][A-Za-z0-9_.-]*$"))b.Add(Diag("MRT3016","Invalid assembly name.")); if(o.OutputKind!=OutputKind.ConsoleApplication)b.Add(Diag("MRT3014",$"Unsupported output kind '{o.OutputKind}'.")); if(!Regex.IsMatch(o.TargetFramework,"^net(8|9|10)\\.0$"))b.Add(Diag("MRT3015",$"Target framework '{o.TargetFramework}' is not supported.")); return b.ToImmutable();}
    static string ProjectXml(BuildOptions o,string runtime)=>new XDocument(new XElement("Project",new XAttribute("Sdk","Microsoft.NET.Sdk"),new XElement("PropertyGroup",new XElement("OutputType","Exe"),new XElement("TargetFramework",o.TargetFramework),new XElement("AssemblyName",o.AssemblyName),new XElement("RootNamespace","Martin.Generated"),new XElement("ImplicitUsings","disable"),new XElement("Nullable","enable"),new XElement("Deterministic",o.Deterministic.ToString().ToLowerInvariant()),new XElement("DebugType",o.EmitPortablePdb?"portable":"none"),new XElement("UseAppHost",o.UseAppHost.ToString().ToLowerInvariant()),new XElement("LangVersion","latest")),new XElement("ItemGroup",new XElement("Compile",new XAttribute("Include",Path.Combine("..","generated","Program.g.cs"))),new XElement("Reference",new XAttribute("Include","Martin.Runtime"),new XElement("HintPath",runtime),new XElement("Private","true"))))).ToString();
    static ImmutableArray<Diagnostic> PublishStaging(string staging, string outputDirectory)
    {
        var finalOutput = Path.GetFullPath(outputDirectory);
        var parent = Path.GetDirectoryName(finalOutput);
        if (string.IsNullOrWhiteSpace(parent))
            return [Diag("MRT3210", $"The output directory '{outputDirectory}' could not be published.")];

        var backup = finalOutput + ".backup." + Guid.NewGuid().ToString("N");

        try
        {
            Directory.CreateDirectory(parent);

            if (Directory.Exists(finalOutput))
                Directory.Move(finalOutput, backup);

            try
            {
                Directory.Move(staging, finalOutput);
            }
            catch
            {
                if (Directory.Exists(finalOutput))
                    Directory.Delete(finalOutput, recursive: true);
                if (Directory.Exists(backup))
                    Directory.Move(backup, finalOutput);
                throw;
            }

            // Publication is committed after staging is moved into the final output.
            _ = TryDelete(backup);
            return [];
        }
        catch
        {
            return [Diag("MRT3210", $"The output directory '{outputDirectory}' could not be published atomically.")];
        }
    }

    static void ApplyExecutablePermissions(string outputDirectory, BuildOptions options)
    {
        if (OperatingSystem.IsWindows())
            return;

        foreach (var file in Directory.EnumerateFiles(outputDirectory))
        {
            var name = Path.GetFileName(file);
            if (string.Equals(name, options.AssemblyName, StringComparison.Ordinal) || string.Equals(name, options.AssemblyName + ".exe", StringComparison.Ordinal))
                File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    static string EnsureTrailingSeparator(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    static Diagnostic? TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            return null;
        }
        catch (Exception ex)
        {
            var diagnostic = Diag("MRT3110", $"Temporary build directory '{path}' could not be removed: {ex.Message}");
            System.Diagnostics.Trace.TraceWarning(diagnostic.Message);
            return diagnostic;
        }
    }
    static Diagnostic Diag(string c,string m)=>new(c,DiagnosticSeverity.Error,m,new TextLocation(SourceText.From(""),new TextSpan(0,0)));
}
