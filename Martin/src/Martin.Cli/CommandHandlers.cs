using System.Collections.Immutable;
using Martin.Build;
using Martin.Build.Artifacts;
using Martin.Build.BuildState;
using Martin.CommandLine;
using Martin.Compiler;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Syntax;
using Martin.Execution;
using Martin.LanguageServices;
using Martin.ProjectSystem;

namespace Martin.Cli;

internal sealed class MartinCliApplication
{
    readonly IMartinConsole console;
    readonly CommandDispatcher dispatcher;

    public MartinCliApplication(IMartinConsole console, CommandDispatcher dispatcher)
    {
        this.console = console ?? throw new ArgumentNullException(nameof(console));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public static MartinCliApplication CreateDefault(IMartinConsole console)
    {
        var versions = new VersionProvider();
        var diagnostics = new CommandDiagnosticService(console, new HumanDiagnosticRenderer());
        var projects = new ProjectLoaderService();
        var build = new CliBuildService(versions, new MartinBuildService());
        var execution = new MartinExecutionService();
        var dispatcher = new CommandDispatcher(
            new HelpCommandHandler(console),
            new VersionCommandHandler(console, versions),
            new NewCommandHandler(console, diagnostics, new MartinProjectCreator()),
            new BuildCommandHandler(console, diagnostics, projects, build),
            new RunCommandHandler(console, diagnostics, projects, build, execution, versions),
            new CleanCommandHandler(console, diagnostics, projects, new MartinProjectCleaner()),
            new TestCommandHandler(console, diagnostics, projects),
            new FormatCommandHandler(console, diagnostics, projects, new MartinLanguageService()));
        return new MartinCliApplication(console, dispatcher);
    }

    public async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        try
        {
            var parse = MartinCommandParser.Parse(args);
            if (!parse.Success)
            {
                RenderParserErrors(parse.Errors);
                return (int)MartinExitCode.InvalidArguments;
            }

            return await dispatcher.DispatchAsync(parse.Command!, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            console.Error.WriteLine("MRT4510: Command was cancelled.");
            return (int)MartinExitCode.Cancelled;
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"MRT4807: {ex.Message}");
            return (int)MartinExitCode.InternalError;
        }
    }

    void RenderParserErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            var code = IsOptionConflict(error) ? "MRT4508" : error.StartsWith("Unknown command", StringComparison.Ordinal) ? "MRT4501" : error == "Project name is required." ? "MRT4506" : "MRT4502";
            console.Error.WriteLine($"{code}: {error}");
        }
    }

    static bool IsOptionConflict(string error) =>
        error.Contains("cannot be used together", StringComparison.Ordinal) ||
        error.Contains("conflicts with", StringComparison.Ordinal) ||
        error.Contains("A project argument cannot be used together", StringComparison.Ordinal);
}

internal sealed class CommandDispatcher
{
    readonly HelpCommandHandler help;
    readonly VersionCommandHandler version;
    readonly NewCommandHandler @new;
    readonly BuildCommandHandler build;
    readonly RunCommandHandler run;
    readonly CleanCommandHandler clean;
    readonly TestCommandHandler test;
    readonly FormatCommandHandler format;

    public CommandDispatcher(HelpCommandHandler help, VersionCommandHandler version, NewCommandHandler @new, BuildCommandHandler build, RunCommandHandler run, CleanCommandHandler clean, TestCommandHandler test, FormatCommandHandler format)
    {
        this.help = help; this.version = version; this.@new = @new; this.build = build; this.run = run; this.clean = clean; this.test = test; this.format = format;
    }

    public Task<int> DispatchAsync(CommandOptions command, CancellationToken cancellationToken) => command switch
    {
        HelpCommandOptions options => Task.FromResult(help.Handle(options)),
        VersionCommandOptions options => Task.FromResult(version.Handle(options)),
        NewCommandOptions options => Task.FromResult(@new.Handle(options)),
        BuildCommandOptions options => build.HandleAsync(options, cancellationToken),
        RunCommandOptions options => run.HandleAsync(options, cancellationToken),
        CleanCommandOptions options => Task.FromResult(clean.Handle(options)),
        TestCommandOptions options => Task.FromResult(test.Handle(options)),
        FormatCommandOptions options => Task.FromResult(format.Handle(options)),
        _ => Task.FromResult((int)MartinExitCode.InvalidArguments)
    };
}

internal sealed class CommandDiagnosticService
{
    readonly IMartinConsole console;
    readonly HumanDiagnosticRenderer renderer;
    public CommandDiagnosticService(IMartinConsole console, HumanDiagnosticRenderer renderer) { this.console = console; this.renderer = renderer; }
    public void Render(IEnumerable<ProjectDiagnostic> diagnostics, DiagnosticFormat format = DiagnosticFormat.Human, ColorMode color = ColorMode.Auto) => Render(diagnostics.Select(DiagnosticAdapters.FromProject), format, color);
    public void Render(IEnumerable<Diagnostic> diagnostics, DiagnosticFormat format = DiagnosticFormat.Human, ColorMode color = ColorMode.Auto) => Render(diagnostics.Select(DiagnosticAdapters.FromCompiler), format, color);
    public void Render(IEnumerable<CommandDiagnostic> diagnostics, DiagnosticFormat format = DiagnosticFormat.Human, ColorMode color = ColorMode.Auto)
    {
        DiagnosticRenderer selected = format == DiagnosticFormat.Json ? new JsonDiagnosticRenderer() : UseColor(format, color) ? new HumanDiagnosticRenderer(useColor: true) : renderer;
        selected.Render(diagnostics, console.Error);
    }

    bool UseColor(DiagnosticFormat format, ColorMode color) =>
        format == DiagnosticFormat.Human &&
        Environment.GetEnvironmentVariable("NO_COLOR") is null &&
        (color == ColorMode.Always || color == ColorMode.Auto && console.SupportsColor && !console.IsErrorRedirected);
}

internal interface IProjectLoaderService { ProjectLoadResult Load(string? projectPath, string? manifestPath, CancellationToken cancellationToken = default); }
internal sealed class ProjectLoaderService : IProjectLoaderService
{
    public ProjectLoadResult Load(string? projectPath, string? manifestPath, CancellationToken cancellationToken = default) => MartinProjectLoader.Load(new() { ProjectPath = projectPath, ManifestPath = manifestPath }, cancellationToken);
}

internal interface ICliBuildService
{
    Task<BuildResult> BuildProjectAsync(MartinProject project, string outputDirectory, BuildConfiguration configuration, bool keepGeneratedFiles, bool useAppHost, bool emitPortablePdb, CancellationToken cancellationToken);
}

internal sealed class CliBuildService : ICliBuildService
{
    readonly IVersionProvider versions;
    readonly IMartinBuildService buildService;
    public CliBuildService(IVersionProvider versions, IMartinBuildService buildService) { this.versions = versions; this.buildService = buildService; }
    public async Task<BuildResult> BuildProjectAsync(MartinProject project, string outputDirectory, BuildConfiguration configuration, bool keepGeneratedFiles, bool useAppHost, bool emitPortablePdb, CancellationToken cancellationToken)
    {
        var trees = new List<SyntaxTree>();
        foreach (var file in project.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            trees.Add(SyntaxTree.Parse(await File.ReadAllTextAsync(file, cancellationToken), file));
        }

        return await buildService.BuildAsync(Compilation.Create(trees), new BuildOptions
        {
            AssemblyName = project.Manifest.Package.Name,
            OutputDirectory = outputDirectory,
            Configuration = configuration,
            TargetFramework = project.Manifest.Target.Framework,
            KeepGeneratedFiles = keepGeneratedFiles,
            UseAppHost = useAppHost,
            EmitPortablePdb = emitPortablePdb,
            BuildStateInputs = new ProjectBuildStateInputs
            {
                ProjectRoot = project.RootDirectory,
                ManifestPath = project.ManifestPath,
                SourceFiles = project.SourceFiles.ToImmutableArray(),
                CompilerVersion = versions.MartinCompilerVersion,
                RuntimeVersion = versions.MartinRuntimeVersion
            }
        }, cancellationToken);
    }
}

internal sealed class HelpCommandHandler(IMartinConsole console)
{
    public int Handle(HelpCommandOptions options)
    {
        console.Out.WriteLine(options.Command is null ? MartinCommandParser.HelpText : CommandHelp(options.Command));
        return (int)MartinExitCode.Success;
    }

    static string CommandHelp(string command) => command switch
    {
        "new" or "init" => "Usage: martin new <name> [--path <directory>] [--force] [--no-git] [--framework <tfm>]",
        "build" or "b" => "Usage: martin build [project] [--manifest-path <path>] [--configuration <debug|release>] [--release|--debug] [--output <directory>] [--keep-generated] [--no-app-host] [--no-pdb] [--diagnostic-format <human|json>] [--color <auto|always|never>] [--quiet|--verbose]",
        "run" or "r" => "Usage: martin run [project] [--manifest-path <path>] [--configuration <debug|release>] [--release|--debug] [--no-build] [--working-directory <path>] [--diagnostic-format <human|json>] [--color <auto|always|never>] [--quiet|--verbose] [-- <program-arguments...>]",
        "clean" => "Usage: martin clean [project] [--manifest-path <path>] [--configuration <debug|release|all>] [--dry-run] [--verbose]",
        "format" or "fmt" => "Usage: martin format [files...] [--manifest-path <path>] [--project <path>] [--check] [--stdout]",
        "test" => "Usage: martin test [project] [--manifest-path <path>]\nMartin test execution is unavailable in this language version.",
        "version" => "Usage: martin version",
        _ => MartinCommandParser.HelpText
    };
}
internal sealed class VersionCommandHandler(IMartinConsole console, IVersionProvider versions) { public int Handle(VersionCommandOptions _) { console.Out.WriteLine($"Martin CLI: {versions.MartinCliVersion}"); console.Out.WriteLine($"Martin compiler: {versions.MartinCompilerVersion}"); console.Out.WriteLine($"Martin runtime: {versions.MartinRuntimeVersion}"); console.Out.WriteLine($"Language version: {versions.LanguageVersion}"); console.Out.WriteLine($"Manifest version: {versions.ManifestVersion}"); console.Out.WriteLine($"Target framework: {versions.TargetFramework}"); return (int)MartinExitCode.Success; } }

internal sealed class NewCommandHandler(IMartinConsole console, CommandDiagnosticService diagnostics, MartinProjectCreator creator)
{
    public int Handle(NewCommandOptions options)
    {
        var result = creator.Create(new MartinProjectCreationOptions { ProjectName = options.ProjectName, BasePath = options.Path, Force = options.Force, CreateGitIgnore = !options.NoGit, TargetFramework = options.Framework ?? "net8.0" });
        if (!result.Success) { diagnostics.Render(result.Diagnostics); return (int)MartinExitCode.InvalidArguments; }
        console.Out.WriteLine($"Created Martin project at {result.ProjectDirectory}");
        return (int)MartinExitCode.Success;
    }
}

internal sealed class BuildCommandHandler(IMartinConsole console, CommandDiagnosticService diagnostics, IProjectLoaderService projects, ICliBuildService build)
{
    public async Task<int> HandleAsync(BuildCommandOptions options, CancellationToken cancellationToken)
    {
        var loaded = projects.Load(options.ProjectPath, options.ManifestPath, cancellationToken);
        if (!loaded.Success) { diagnostics.Render(loaded.Diagnostics, options.DiagnosticFormat, options.Color); return (int)(loaded.Diagnostics.Any(d => d.Code == "MRT4001") ? MartinExitCode.ProjectNotFound : MartinExitCode.ManifestInvalid); }
        var p = loaded.Project!; var config = options.Configuration; var outDir = options.Output ?? Path.Combine(p.RootDirectory, p.Manifest.Build.Output, config.ToString(), p.Manifest.Target.Framework);
        if (ShouldWriteStatus(options)) console.Out.WriteLine($"Building {p.Manifest.Package.Name} {p.Manifest.Package.Version}");
        var res = await build.BuildProjectAsync(p, outDir, config, options.KeepGenerated, options.UseAppHost, options.EmitPortablePdb, cancellationToken);
        if (!res.Success) { diagnostics.Render(res.Diagnostics, options.DiagnosticFormat, options.Color); return (int)ExitCodeMapper.ForBuildFailure(res.Diagnostics); }
        if (ShouldWriteStatus(options)) console.Out.WriteLine($"Build succeeded\n  Output: {res.OutputDirectory}");
        return (int)MartinExitCode.Success;
    }

    static bool ShouldWriteStatus(BuildCommandOptions options) =>
        options.Verbosity != Verbosity.Quiet &&
        options.DiagnosticFormat == DiagnosticFormat.Human;
}

internal sealed class RunCommandHandler(IMartinConsole console, CommandDiagnosticService diagnostics, IProjectLoaderService projects, ICliBuildService build, IMartinExecutionService execution, IVersionProvider versions)
{
    public async Task<int> HandleAsync(RunCommandOptions options, CancellationToken cancellationToken)
    {
        var loaded = projects.Load(options.ProjectPath, options.ManifestPath, cancellationToken);
        if (!loaded.Success) { diagnostics.Render(loaded.Diagnostics, options.DiagnosticFormat, options.Color); return (int)MartinExitCode.ProjectNotFound; }
        var p = loaded.Project!; var config = options.Configuration; var outDir = Path.Combine(p.RootDirectory, p.Manifest.Build.Output, config.ToString(), p.Manifest.Target.Framework);
        BuildResult br;
        if (options.NoBuild)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var runtimeDiscovery = new RuntimeDiscovery().Discover(new RuntimeDiscoveryOptions { KnownRuntimeDirectory = Path.Combine(AppContext.BaseDirectory, "runtimes") });
            if (!runtimeDiscovery.Success || runtimeDiscovery.Runtime is null)
            {
                diagnostics.Render(runtimeDiscovery.Diagnostics, options.DiagnosticFormat, options.Color);
                return (int)MartinExitCode.BuildFailed;
            }

            var freshness = new BuildFreshnessChecker().Check(outDir, new BuildFreshnessCheckInputs { ProjectRoot = p.RootDirectory, ManifestPath = p.ManifestPath, SourceFiles = p.SourceFiles, CompilerVersion = versions.MartinCompilerVersion, RuntimeVersion = versions.MartinRuntimeVersion, RuntimeSha256 = runtimeDiscovery.Runtime.Sha256, Configuration = config, TargetFramework = p.Manifest.Target.Framework, AssemblyName = p.Manifest.Package.Name }, cancellationToken);
            if (!freshness.IsFresh)
            {
                diagnostics.Render([new CommandDiagnostic
                {
                    Code = "MRT4805",
                    Severity = CommandDiagnosticSeverity.Error,
                    Message = $"Build output is stale because {freshness.Reason}. Run 'martin build' or omit '--no-build'."
                }], options.DiagnosticFormat, options.Color);
                return (int)MartinExitCode.ExecutionFailed;
            }
            br = new() { Success = true, OutputDirectory = outDir, EntryPointPath = freshness.EntryPointPath };
        }
        else br = await build.BuildProjectAsync(p, outDir, config, false, true, true, cancellationToken);
        if (!br.Success) { diagnostics.Render(br.Diagnostics, options.DiagnosticFormat, options.Color); return (int)MartinExitCode.BuildFailed; }
        var er = await execution.RunAsync(br, new ExecutionOptions { Arguments = options.ProgramArguments, WorkingDirectory = options.WorkingDirectory }, cancellationToken);
        console.Out.Write(er.StandardOutput); console.Error.Write(er.StandardError);
        return er.ExitCode ?? (int)(er.Started ? MartinExitCode.Success : MartinExitCode.ExecutionFailed);
    }
}

internal sealed class FormatCommandHandler(IMartinConsole console, CommandDiagnosticService diagnostics, IProjectLoaderService projects, MartinLanguageService formatter)
{
    public int Handle(FormatCommandOptions options)
    {
        var files = options.Files;
        if (files.Count == 0) { var loaded = projects.Load(options.ProjectPath, options.ManifestPath); if (!loaded.Success) { diagnostics.Render(loaded.Diagnostics); return (int)MartinExitCode.ProjectNotFound; } files = loaded.Project!.SourceFiles.Order(StringComparer.Ordinal).ToArray(); }
        if (options.Stdout && files.Count != 1) { console.Error.WriteLine("MRT4602: --stdout requires exactly one source file."); return (int)MartinExitCode.InvalidArguments; }
        var fullPaths = files.Select(Path.GetFullPath).ToArray();
        foreach (var full in fullPaths)
        {
            if (!File.Exists(full)) { console.Error.WriteLine($"MRT4601: Source file '{full}' does not exist."); return (int)MartinExitCode.InvalidArguments; }
        }

        var changed = false;
        foreach (var full in fullPaths)
        {
            var text = File.ReadAllText(full); var result = formatter.GetFormattingEdits(text);
            if (result.WasRefused) { console.Error.WriteLine($"{result.DiagnosticCode}: {result.Message} ({full})"); return (int)MartinExitCode.CompilationFailed; }
            var formatted = TextEditApplicator.Apply(text, result.Edits);
            if (options.Stdout) { console.Out.Write(formatted); continue; }
            if (text != formatted) { changed = true; if (options.Check) console.Out.WriteLine($"Would format {full}"); else { var temp = full + ".tmp." + Guid.NewGuid().ToString("N"); File.WriteAllText(temp, formatted); File.Move(temp, full, overwrite: true); console.Out.WriteLine($"Formatted {full}"); } }
        }
        return options.Check && changed ? (int)MartinExitCode.CompilationFailed : (int)MartinExitCode.Success;
    }
}

internal sealed class CleanCommandHandler(IMartinConsole console, CommandDiagnosticService diagnostics, IProjectLoaderService projects, MartinProjectCleaner cleaner)
{
    public int Handle(CleanCommandOptions options)
    {
        var loaded = projects.Load(options.ProjectPath, options.ManifestPath);
        if (!loaded.Success) { diagnostics.Render(loaded.Diagnostics); return (int)MartinExitCode.ProjectNotFound; }
        var p = loaded.Project!;
        var result = cleaner.Clean(new ProjectCleanOptions { ProjectRoot = p.RootDirectory, TargetPaths = CleanTargets(p, options.Configuration), DryRun = options.DryRun });
        if (options.DryRun) foreach (var directory in result.Plan.Directories) console.Out.WriteLine($"Would remove {directory}");
        else if (options.Verbose) foreach (var directory in result.Plan.Directories) console.Out.WriteLine($"Removed {directory}");
        if (!result.Success) { diagnostics.Render(result.Diagnostics); return (int)(result.Diagnostics.Any(diagnostic => diagnostic.Code == "MRT4513") ? MartinExitCode.ManifestInvalid : MartinExitCode.BuildFailed); }
        return (int)MartinExitCode.Success;
    }

    static IEnumerable<string> CleanTargets(MartinProject project, string? configuration)
    {
        var output = project.Manifest.Build.Output;
        var intermediate = project.Manifest.Build.Intermediate;
        var framework = project.Manifest.Target.Framework;
        return string.Equals(configuration, "debug", StringComparison.OrdinalIgnoreCase)
            ? [Path.Combine(output, "Debug", framework), Path.Combine(intermediate, "Debug", framework)]
            : string.Equals(configuration, "release", StringComparison.OrdinalIgnoreCase)
                ? [Path.Combine(output, "Release", framework), Path.Combine(intermediate, "Release", framework)]
                : [output, intermediate];
    }
}

internal sealed class TestCommandHandler(IMartinConsole console, CommandDiagnosticService diagnostics, IProjectLoaderService projects)
{
    public int Handle(TestCommandOptions options)
    {
        var loaded = projects.Load(options.ProjectPath, options.ManifestPath);
        if (!loaded.Success) { diagnostics.Render(loaded.Diagnostics); return (int)MartinExitCode.ProjectNotFound; }
        console.Error.WriteLine("MRT4901: Martin test execution is not implemented in this language version.");
        return (int)MartinExitCode.TestsUnavailableOrFailed;
    }
}

internal static class ExitCodeMapper
{
    public static MartinExitCode ForBuildFailure(IEnumerable<Diagnostic> diagnostics) => diagnostics.Any(d => d.Code.StartsWith("MRT2", StringComparison.Ordinal)) ? MartinExitCode.CompilationFailed : MartinExitCode.BuildFailed;
}
