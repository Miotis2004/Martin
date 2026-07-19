using Martin.Build;
using Martin.Compiler;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Syntax;
using Martin.CommandLine;
using System.CommandLine;

namespace Martin.Compiler.Cli;

internal static class Program
{
    static readonly IMartinConsole Console = SystemMartinConsole.Instance;
    static readonly IVersionProvider Versions = new VersionProvider();

    static async Task<int> Main(string[] args)
    {
        using var cancellation = new CommandCancellationSource();
        try
        {
            var parse = MartincCommandParser.Parse(args);
            if (!parse.Success)
            {
                RenderParserErrors(parse.Errors);
                return (int)MartinExitCode.InvalidArguments;
            }

            return parse.Command switch
            {
                HelpCompileCommandOptions _ => Help(),
                VersionCompileCommandOptions _ => Version(),
                CompileCommandOptions options => await Compile(options, cancellation.Token),
                _ => (int)MartinExitCode.InvalidArguments
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("MRT4510: Command was cancelled.");
            return (int)MartinExitCode.Cancelled;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"MRT4807: {ex.Message}");
            return (int)MartinExitCode.InternalError;
        }
    }

    static async Task<int> Compile(CompileCommandOptions options, CancellationToken cancellationToken)
    {
        var normalized = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in options.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var full = Path.GetFullPath(file);
            if (Directory.Exists(full)) { Err($"MRT4503: Input file '{file}' is a directory, not a Martin source file."); return (int)MartinExitCode.InvalidArguments; }
            if (!File.Exists(full)) { Err($"MRT4503: Input file '{file}' does not exist."); return (int)MartinExitCode.InvalidArguments; }
            if (!string.Equals(Path.GetExtension(full), ".martin", StringComparison.OrdinalIgnoreCase)) { Err($"MRT4504: Input file '{file}' is not a Martin source file."); return (int)MartinExitCode.InvalidArguments; }
            if (!normalized.Add(full)) { Err($"MRT4509: Source file '{file}' was supplied more than once."); return (int)MartinExitCode.InvalidArguments; }
        }

        var trees = new List<SyntaxTree>();
        foreach (var file in normalized)
        {
            cancellationToken.ThrowIfCancellationRequested();
            trees.Add(SyntaxTree.Parse(await File.ReadAllTextAsync(file, cancellationToken), file));
        }

        var compilation = Compilation.Create(trees);
        var result = await new MartinBuildService().BuildAsync(compilation, new BuildOptions { AssemblyName = options.AssemblyName, OutputDirectory = options.OutputDirectory, Configuration = options.Configuration, TargetFramework = options.TargetFramework, KeepGeneratedFiles = options.KeepGenerated }, cancellationToken);
        if (!result.Success)
        {
            Render(result.Diagnostics, options.DiagnosticFormat == DiagnosticFormat.Json);
            return (int)(result.Diagnostics.Any(d => d.Code.StartsWith("MRT2", StringComparison.Ordinal)) ? MartinExitCode.CompilationFailed : MartinExitCode.BuildFailed);
        }

        if (ShouldWriteStatus(options)) Console.Out.WriteLine($"Build succeeded: {result.OutputDirectory}");
        return (int)MartinExitCode.Success;
    }

    static bool ShouldWriteStatus(CompileCommandOptions options) =>
        !options.Quiet && options.DiagnosticFormat == DiagnosticFormat.Human;

    static int Help() { Console.Out.WriteLine(MartincCommandParser.HelpText); return (int)MartinExitCode.Success; }
    static int Version() { Console.Out.WriteLine($"martinc: {Versions.MartinCompilerVersion}"); Console.Out.WriteLine($"Language version: {Versions.LanguageVersion}"); Console.Out.WriteLine($"Manifest version: {Versions.ManifestVersion}"); Console.Out.WriteLine($"Target framework: {Versions.TargetFramework}"); return (int)MartinExitCode.Success; }

    static void Render(IEnumerable<Diagnostic> ds, bool json)
    {
        DiagnosticRenderer renderer = json ? new JsonDiagnosticRenderer() : new HumanDiagnosticRenderer();
        renderer.Render(ds.Select(ToCommandDiagnostic), Console.Error);
    }

    static CommandDiagnostic ToCommandDiagnostic(Diagnostic diagnostic) => DiagnosticAdapters.FromCompiler(diagnostic);
    static void RenderParserErrors(IEnumerable<string> errors) { foreach (var error in errors) Console.Error.WriteLine($"{(IsOptionConflict(error) ? "MRT4508" : "MRT4502")}: {error}"); }
    static bool IsOptionConflict(string error) => error.Contains("cannot be used together", StringComparison.Ordinal) || error.Contains("conflicts with", StringComparison.Ordinal);
    static void Err(string m) => Console.Error.WriteLine(m);
}

internal static class MartincCommandParser
{
    public const string HelpText = """
Usage: martinc <source-files...> [options]

Options:
  --output <directory>             Build output directory. Defaults to bin.
  --name <assembly-name>           Assembly name. Defaults to Program.
  --configuration <debug|release>  Build configuration. Defaults to debug.
  --target-framework <tfm>         Target framework. Defaults to net8.0.
  --keep-generated                 Keep generated C# files.
  --emit-csharp                    Alias for --keep-generated.
  --diagnostic-format <human|json> Diagnostic output format. Defaults to human.
  --color <auto|always|never>      Accepted for shared CLI compatibility.
  --quiet                          Suppress success output; mutually exclusive with --verbose.
  --verbose                        Use normal verbosity; mutually exclusive with --quiet.
  -h, --help                       Show help.
  -v, --version                    Show version.
""";

    public static CompileParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count > 0 && Is(args[0], "--help", "-h", "help")) return CompileParseResult.Ok(new HelpCompileCommandOptions());
        if (args.Count > 0 && Is(args[0], "--version", "-v", "version")) return CompileParseResult.Ok(new VersionCompileCommandOptions());

        var root = MartincRootCommandFactory.Create();
        var parse = root.Parse(args.ToArray());
        var errors = parse.Errors.Select(error => error.Message).ToList();
        errors.AddRange(CompilerCliValidation.Validate(args));
        if (errors.Count > 0) return CompileParseResult.Fail(errors);

        var files = parse.GetValueForArgument(MartincRootCommandFactory.SourceFilesArgument) ?? [];
        var name = parse.GetValueForOption(MartincRootCommandFactory.NameOption) ?? "Program";
        if (!CompilerCliValidation.IsValidAssemblyName(name)) errors.Add($"Assembly name '{name}' is invalid.");
        if (errors.Count > 0) return CompileParseResult.Fail(errors);

        var configuration = string.Equals(parse.GetValueForOption(MartincRootCommandFactory.ConfigurationOption), "release", StringComparison.OrdinalIgnoreCase) ? BuildConfiguration.Release : BuildConfiguration.Debug;
        var format = string.Equals(parse.GetValueForOption(MartincRootCommandFactory.DiagnosticFormatOption), "json", StringComparison.OrdinalIgnoreCase) ? DiagnosticFormat.Json : DiagnosticFormat.Human;
        var keepGenerated = parse.GetValueForOption(MartincRootCommandFactory.KeepGeneratedOption) || parse.GetValueForOption(MartincRootCommandFactory.EmitCSharpOption);
        var quiet = parse.GetValueForOption(MartincRootCommandFactory.QuietOption);
        return CompileParseResult.Ok(new CompileCommandOptions(files, parse.GetValueForOption(MartincRootCommandFactory.OutputOption) ?? "bin", name, configuration, parse.GetValueForOption(MartincRootCommandFactory.TargetFrameworkOption) ?? "net8.0", keepGenerated, format, quiet));
    }

    static bool Is(string value, params string[] aliases) => aliases.Contains(value, StringComparer.Ordinal);
}

internal static class MartincRootCommandFactory
{
    public static readonly System.CommandLine.Argument<string[]> SourceFilesArgument = new("source-files");
    public static readonly System.CommandLine.Option<string?> OutputOption = new("--output", () => "bin", "Build output directory.");
    public static readonly System.CommandLine.Option<string?> NameOption = new("--name", () => "Program", "Assembly name.");
    public static readonly System.CommandLine.Option<string?> ConfigurationOption = ChoiceOption("--configuration", "Build configuration.", "debug", "release");
    public static readonly System.CommandLine.Option<string?> TargetFrameworkOption = new("--target-framework", () => "net8.0", "Target framework.");
    public static readonly System.CommandLine.Option<bool> KeepGeneratedOption = new("--keep-generated", "Keep generated files.");
    public static readonly System.CommandLine.Option<bool> EmitCSharpOption = new("--emit-csharp", "Alias for --keep-generated.");
    public static readonly System.CommandLine.Option<string?> DiagnosticFormatOption = ChoiceOption("--diagnostic-format", "Diagnostic output format.", "human", "json");
    public static readonly System.CommandLine.Option<string?> ColorOption = ChoiceOption("--color", "Color mode.", "auto", "always", "never");
    public static readonly System.CommandLine.Option<bool> QuietOption = new("--quiet", "Suppress success output.");
    public static readonly System.CommandLine.Option<bool> VerboseOption = new("--verbose", "Use normal verbosity.");

    static readonly System.CommandLine.RootCommand Root = BuildRoot();

    public static System.CommandLine.RootCommand Create() => Root;

    static System.CommandLine.RootCommand BuildRoot()
    {
        var root = new System.CommandLine.RootCommand("Martin file-oriented compiler CLI");
        root.AddArgument(SourceFilesArgument);
        root.AddOption(OutputOption);
        root.AddOption(NameOption);
        root.AddOption(ConfigurationOption);
        root.AddOption(TargetFrameworkOption);
        root.AddOption(KeepGeneratedOption);
        root.AddOption(EmitCSharpOption);
        root.AddOption(DiagnosticFormatOption);
        root.AddOption(ColorOption);
        root.AddOption(QuietOption);
        root.AddOption(VerboseOption);
        return root;
    }

    static System.CommandLine.Option<string?> ChoiceOption(string alias, string description, params string[] values)
    {
        var option = new System.CommandLine.Option<string?>(alias, description);
        option.FromAmong(values);
        return option;
    }
}

internal static class CompilerCliValidation
{
    public static IEnumerable<string> Validate(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args.All(arg => arg.StartsWith('-'))) yield return "At least one source file is required.";
        if (args.Contains("--quiet", StringComparer.Ordinal) && args.Contains("--verbose", StringComparer.Ordinal)) yield return "Options '--quiet' and '--verbose' cannot be used together.";
        foreach (var option in new[] { "--output", "--name", "--configuration", "--target-framework", "--diagnostic-format", "--color" })
        {
            if (args.Count(arg => arg == option) > 1) yield return $"Option '{option}' can only be specified once.";
        }
    }

    public static bool IsValidAssemblyName(string value) => !string.IsNullOrWhiteSpace(value) && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}

internal abstract record CompileCommand;
internal sealed record CompileParseResult(bool Success, CompileCommand? Command, IReadOnlyList<string> Errors)
{
    public static CompileParseResult Ok(CompileCommand command) => new(true, command, Array.Empty<string>());
    public static CompileParseResult Fail(IReadOnlyList<string> errors) => new(false, null, errors);
}
internal sealed record HelpCompileCommandOptions : CompileCommand;
internal sealed record VersionCompileCommandOptions : CompileCommand;
internal sealed record CompileCommandOptions(IReadOnlyList<string> SourceFiles, string OutputDirectory, string AssemblyName, BuildConfiguration Configuration, string TargetFramework, bool KeepGenerated, DiagnosticFormat DiagnosticFormat, bool Quiet) : CompileCommand;
