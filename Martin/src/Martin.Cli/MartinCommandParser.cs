using Martin.Build;
using Martin.CommandLine;
using System.CommandLine;

namespace Martin.Cli;

internal static class MartinCommandParser
{
    public const string HelpText = """
Usage: martin <command> [options]

Commands:
  new <name>             Create a Martin project.
  build [project]        Build a Martin project.
  run [project] [-- ...] Build and run a Martin project, forwarding arguments after -- exactly.
  clean [project]        Remove project build outputs.
  format [files...]      Format Martin source files.
  test [project]         Report that Martin tests are unavailable in this language version.
  version                Print toolchain version information.

Global options:
  -h, --help             Show help without requiring a project.
  -v, --version          Show version without requiring a project.
  --manifest-path <path> Use an explicit Martin.toml manifest where accepted.
  --diagnostic-format <human|json>  Select diagnostic output where accepted.
  --color <auto|always|never>       Select color mode for human diagnostics.
  --quiet, --verbose     Select command verbosity where accepted; mutually exclusive.

Mutually exclusive options include --quiet with --verbose, --release with --debug,
project arguments with --manifest-path, and format --stdout with project-wide or
multiple-file formatting.

Run `martin <command> --help` for command-specific options.
""";

    public static MartinParseResult Parse(IReadOnlyList<string> args)
    {
        var root = MartinRootCommandFactory.Create();
        var raw = args.ToArray();

        if (raw.Length == 0 || Is(raw[0], "help", "--help", "-h")) return MartinParseResult.Ok(new HelpCommandOptions());
        if (Is(raw[0], "version", "--version", "-v")) return MartinParseResult.Ok(new VersionCommandOptions());

        var command = raw[0];
        var tail = raw.Skip(1).ToArray();
        if (!IsKnownCommand(command)) return MartinParseResult.Fail([$"Unknown command '{command}'."]);
        if (tail.Length == 1 && Is(tail[0], "--help", "-h")) return MartinParseResult.Ok(new HelpCommandOptions(command));
        if (Is(command, "new", "init") && !PositionalTokens(tail).Any()) return MartinParseResult.Fail(["Project name is required."]);

        var separator = Array.IndexOf(raw, "--");
        var parseArgs = separator >= 0 ? raw.Take(separator).ToArray() : raw;
        var forwarded = separator >= 0 ? raw.Skip(separator + 1).ToArray() : Array.Empty<string>();
        var errors = ValidateUnknownOptions(parseArgs).Concat(ValidateMissingOptionValues(parseArgs)).Concat(ValidateChoiceValues(parseArgs)).ToList();
        var parse = root.Parse(parseArgs);
        errors.AddRange(parse.Errors.Select(error => error.Message));
        errors.AddRange(ValidateCommon(parseArgs));
        if (errors.Count > 0) return MartinParseResult.Fail(errors);

        return command switch
        {
            "new" or "init" => MartinParseResult.Ok(new NewCommandOptions(Value(parse, MartinRootCommandFactory.NewNameArgument)!, Value(parse, MartinRootCommandFactory.NewPathOption), Bool(parse, MartinRootCommandFactory.NewForceOption), Bool(parse, MartinRootCommandFactory.NewNoGitOption), Value(parse, MartinRootCommandFactory.NewFrameworkOption))),
            "build" or "b" => MartinParseResult.Ok(new BuildCommandOptions(Value(parse, MartinRootCommandFactory.BuildProjectArgument), Value(parse, MartinRootCommandFactory.BuildManifestOption), Value(parse, MartinRootCommandFactory.BuildOutputOption), BuildConfiguration(parse, MartinRootCommandFactory.BuildConfigurationOption, Bool(parse, MartinRootCommandFactory.BuildReleaseOption), Bool(parse, MartinRootCommandFactory.BuildDebugOption)), Verbosity(parse), Bool(parse, MartinRootCommandFactory.BuildKeepGeneratedOption), DiagnosticFormat(parse, MartinRootCommandFactory.BuildDiagnosticFormatOption), Color(parse, MartinRootCommandFactory.BuildColorOption), !Bool(parse, MartinRootCommandFactory.BuildNoAppHostOption), !Bool(parse, MartinRootCommandFactory.BuildNoPdbOption))),
            "run" or "r" => MartinParseResult.Ok(new RunCommandOptions(Value(parse, MartinRootCommandFactory.RunProjectArgument), Value(parse, MartinRootCommandFactory.RunManifestOption), BuildConfiguration(parse, MartinRootCommandFactory.RunConfigurationOption, Bool(parse, MartinRootCommandFactory.RunReleaseOption), Bool(parse, MartinRootCommandFactory.RunDebugOption)), Bool(parse, MartinRootCommandFactory.RunNoBuildOption), forwarded, DiagnosticFormat(parse, MartinRootCommandFactory.RunDiagnosticFormatOption), Color(parse, MartinRootCommandFactory.RunColorOption), Verbosity(parse), Value(parse, MartinRootCommandFactory.RunWorkingDirectoryOption))),
            "clean" => MartinParseResult.Ok(new CleanCommandOptions(Value(parse, MartinRootCommandFactory.CleanProjectArgument), Value(parse, MartinRootCommandFactory.CleanManifestOption), Bool(parse, MartinRootCommandFactory.CleanDryRunOption), Bool(parse, MartinRootCommandFactory.CleanVerboseOption), Value(parse, MartinRootCommandFactory.CleanConfigurationOption))),
            "format" or "fmt" => MartinParseResult.Ok(new FormatCommandOptions(Value(parse, MartinRootCommandFactory.FormatProjectOption), Value(parse, MartinRootCommandFactory.FormatManifestOption), Bool(parse, MartinRootCommandFactory.FormatCheckOption), Bool(parse, MartinRootCommandFactory.FormatStdoutOption), Values(parse, MartinRootCommandFactory.FormatFilesArgument))),
            "test" => MartinParseResult.Ok(new TestCommandOptions(Value(parse, MartinRootCommandFactory.TestProjectArgument), Value(parse, MartinRootCommandFactory.TestManifestOption))),
            _ => MartinParseResult.Fail([$"Unknown command '{command}'."])
        };
    }

    static IEnumerable<string> ValidateMissingOptionValues(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!OptionTakesValue(token)) continue;
            if (i == args.Length - 1 || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                yield return $"Option '{token}' requires a value.";
        }
    }

    static IEnumerable<string> ValidateUnknownOptions(string[] args)
    {
        if (args.Length == 0)
            yield break;

        var knownOptions = KnownOptions(args[0]).ToHashSet(StringComparer.Ordinal);
        var skipNext = false;
        for (var i = 1; i < args.Length; i++)
        {
            var token = args[i];
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            if (!token.StartsWith("-", StringComparison.Ordinal))
                continue;

            var optionName = token.Split('=', 2)[0];
            if (!knownOptions.Contains(optionName))
            {
                yield return $"Unknown option '{optionName}'.";
                continue;
            }

            if (OptionTakesValue(optionName) && !token.Contains('=', StringComparison.Ordinal))
                skipNext = true;
        }
    }

    static IEnumerable<string> KnownOptions(string command) => command switch
    {
        "new" or "init" => ["--path", "-p", "--framework", "--force", "-f", "--no-git"],
        "build" or "b" => ["--manifest-path", "-m", "--output", "-o", "--configuration", "-c", "--release", "--debug", "--keep-generated", "--no-app-host", "--no-pdb", "--diagnostic-format", "--color", "--quiet", "-q", "--verbose"],
        "run" or "r" => ["--manifest-path", "-m", "--configuration", "-c", "--release", "--debug", "--no-build", "--working-directory", "--diagnostic-format", "--color", "--quiet", "-q", "--verbose"],
        "clean" => ["--manifest-path", "-m", "--configuration", "-c", "--dry-run", "--verbose"],
        "format" or "fmt" => ["--manifest-path", "-m", "--project", "-p", "--check", "--stdout"],
        "test" => ["--manifest-path", "-m"],
        _ => []
    };

    static IEnumerable<string> ValidateChoiceValues(string[] args)
    {
        foreach (var (aliases, values) in ChoiceOptions(args))
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (!aliases.Contains(args[i], StringComparer.Ordinal)) continue;
                var value = args[i + 1];
                if (!value.StartsWith("-", StringComparison.Ordinal) && !values.Contains(value, StringComparer.OrdinalIgnoreCase))
                    yield return $"Option '{args[i]}' value '{value}' is not recognized. Must be one of: {string.Join(", ", values)}.";
            }
        }
    }

    static IEnumerable<(string[] Aliases, string[] Values)> ChoiceOptions(string[] args)
    {
        yield return (["--configuration", "-c"], Is(args.FirstOrDefault() ?? string.Empty, "clean") ? ["debug", "release", "all"] : ["debug", "release"]);
        yield return (["--diagnostic-format"], ["human", "json"]);
        yield return (["--color"], ["auto", "always", "never"]);
    }

    static IEnumerable<string> ValidateCommon(string[] args)
    {
        if (args.Contains("--external-terminal", StringComparer.Ordinal)) yield return "Unknown option '--external-terminal'.";
        if (HasAll(args, "--quiet", "--verbose")) yield return "Options '--quiet' and '--verbose' cannot be used together.";
        if (HasAll(args, "--release", "--debug")) yield return "Options '--release' and '--debug' cannot be used together.";

        var configuration = OptionValue(args, "--configuration", "-c");
        if (string.Equals(configuration, "release", StringComparison.OrdinalIgnoreCase) && args.Contains("--debug", StringComparer.Ordinal)) yield return "Option '--debug' conflicts with '--configuration release'.";
        if (string.Equals(configuration, "debug", StringComparison.OrdinalIgnoreCase) && args.Contains("--release", StringComparer.Ordinal)) yield return "Option '--release' conflicts with '--configuration debug'.";

        if (HasDuplicate(args, "--manifest-path", "-m")) yield return "Option '--manifest-path' can only be specified once.";
        if (HasDuplicate(args, "--configuration", "-c")) yield return "Option '--configuration' can only be specified once.";
        if (HasDuplicate(args, "--output", "-o")) yield return "Option '--output' can only be specified once.";
        if (HasProjectAndManifest(args)) yield return "A project argument cannot be used together with '--manifest-path'.";
        foreach (var error in ValidateFormatConflicts(args)) yield return error;
    }

    static IEnumerable<string> ValidateFormatConflicts(string[] args)
    {
        if (args.Length == 0 || !Is(args[0], "format", "fmt") || !args.Contains("--stdout", StringComparer.Ordinal)) yield break;

        var inputs = PositionalTokens(args.Skip(1)).ToArray();
        if (inputs.Length > 1) yield return "Option '--stdout' conflicts with multiple format inputs.";
        if (inputs.Length == 0 || args.Contains("--project", StringComparer.Ordinal) || args.Contains("-p", StringComparer.Ordinal) || args.Contains("--manifest-path", StringComparer.Ordinal) || args.Contains("-m", StringComparer.Ordinal)) yield return "Option '--stdout' conflicts with project-wide in-place formatting.";
    }

    static IEnumerable<string> PositionalTokens(IEnumerable<string> tokens)
    {
        var skipNext = false;
        foreach (var token in tokens)
        {
            if (skipNext) { skipNext = false; continue; }
            if (OptionTakesValue(token)) { skipNext = true; continue; }
            if (!token.StartsWith("-", StringComparison.Ordinal)) yield return token;
        }
    }

    static bool HasProjectAndManifest(string[] args) => args.Length > 2 && Is(args[0], "build", "b", "run", "r", "clean", "test") && HasAny(args, "--manifest-path", "-m") && PositionalTokens(args.Skip(1)).Any();
    static bool HasDuplicate(string[] args, params string[] aliases) => args.Count(arg => aliases.Contains(arg, StringComparer.Ordinal)) > 1;
    static bool HasAll(string[] args, params string[] aliases) => aliases.All(alias => args.Contains(alias, StringComparer.Ordinal));
    static bool HasAny(string[] args, params string[] aliases) => aliases.Any(alias => args.Contains(alias, StringComparer.Ordinal));
    static string? OptionValue(string[] args, params string[] aliases)
    {
        for (var i = 0; i < args.Length - 1; i++) if (aliases.Contains(args[i], StringComparer.Ordinal)) return args[i + 1];
        return null;
    }
    static bool OptionTakesValue(string token) => token is "--manifest-path" or "-m" or "--configuration" or "-c" or "--output" or "-o" or "--diagnostic-format" or "--color" or "--working-directory" or "--path" or "-p" or "--framework" or "--project";
    static string? Value(System.CommandLine.Parsing.ParseResult parse, Option<string?> option) => parse.GetValueForOption(option);
    static string? Value(System.CommandLine.Parsing.ParseResult parse, Argument<string?> argument) => parse.GetValueForArgument(argument);
    static IReadOnlyList<string> Values(System.CommandLine.Parsing.ParseResult parse, Argument<string[]> argument) => parse.GetValueForArgument(argument) ?? [];
    static bool Bool(System.CommandLine.Parsing.ParseResult parse, Option<bool> option) => parse.GetValueForOption(option);
    static BuildConfiguration BuildConfiguration(System.CommandLine.Parsing.ParseResult parse, Option<string?> option, bool release, bool debug) => release ? Martin.Build.BuildConfiguration.Release : debug ? Martin.Build.BuildConfiguration.Debug : string.Equals(Value(parse, option), "release", StringComparison.OrdinalIgnoreCase) ? Martin.Build.BuildConfiguration.Release : Martin.Build.BuildConfiguration.Debug;
    static DiagnosticFormat DiagnosticFormat(System.CommandLine.Parsing.ParseResult parse, Option<string?> option) => string.Equals(Value(parse, option), "json", StringComparison.OrdinalIgnoreCase) ? Martin.CommandLine.DiagnosticFormat.Json : Martin.CommandLine.DiagnosticFormat.Human;
    static ColorMode Color(System.CommandLine.Parsing.ParseResult parse, Option<string?> option) => string.Equals(Value(parse, option), "always", StringComparison.OrdinalIgnoreCase) ? ColorMode.Always : string.Equals(Value(parse, option), "never", StringComparison.OrdinalIgnoreCase) ? ColorMode.Never : ColorMode.Auto;
    static Verbosity Verbosity(System.CommandLine.Parsing.ParseResult parse) => Bool(parse, MartinRootCommandFactory.QuietOption) ? Martin.CommandLine.Verbosity.Quiet : Bool(parse, MartinRootCommandFactory.VerboseOption) ? Martin.CommandLine.Verbosity.Detailed : Martin.CommandLine.Verbosity.Normal;
    static bool IsKnownCommand(string value) => Is(value, "new", "init", "build", "b", "run", "r", "clean", "format", "fmt", "test");
    static bool Is(string value, params string[] aliases) => aliases.Contains(value, StringComparer.Ordinal);
}

internal static class MartinRootCommandFactory
{
    public static readonly Option<bool> QuietOption = new(["--quiet", "-q"], "Suppress non-essential output.");
    public static readonly Option<bool> VerboseOption = new("--verbose", "Use detailed output.");
    public static readonly Argument<string?> NewNameArgument = new("name");
    public static readonly Option<string?> NewPathOption = new(["--path", "-p"], "Project directory.");
    public static readonly Option<string?> NewFrameworkOption = new("--framework", "Target framework.");
    public static readonly Option<bool> NewForceOption = new(["--force", "-f"], "Overwrite existing files.");
    public static readonly Option<bool> NewNoGitOption = new("--no-git", "Do not create .gitignore.");
    public static readonly Argument<string?> BuildProjectArgument = new("project", () => null);
    public static readonly Option<string?> BuildManifestOption = new(["--manifest-path", "-m"], "Manifest path.");
    public static readonly Option<string?> BuildOutputOption = new(["--output", "-o"], "Output directory.");
    public static readonly Option<string?> BuildConfigurationOption = ChoiceOption(["--configuration", "-c"], "Build configuration.", "debug", "release");
    public static readonly Option<bool> BuildReleaseOption = new("--release", "Use Release configuration.");
    public static readonly Option<bool> BuildDebugOption = new("--debug", "Use Debug configuration.");
    public static readonly Option<bool> BuildKeepGeneratedOption = new("--keep-generated", "Keep generated files.");
    public static readonly Option<bool> BuildNoAppHostOption = new("--no-app-host", "Disable native app-host generation.");
    public static readonly Option<bool> BuildNoPdbOption = new("--no-pdb", "Disable portable PDB generation.");
    public static readonly Option<string?> BuildDiagnosticFormatOption = ChoiceOption("--diagnostic-format", "Diagnostic format.", "human", "json");
    public static readonly Option<string?> BuildColorOption = ChoiceOption("--color", "Color mode.", "auto", "always", "never");
    public static readonly Argument<string?> RunProjectArgument = new("project", () => null);
    public static readonly Option<string?> RunManifestOption = new(["--manifest-path", "-m"], "Manifest path.");
    public static readonly Option<string?> RunConfigurationOption = ChoiceOption(["--configuration", "-c"], "Build configuration.", "debug", "release");
    public static readonly Option<bool> RunReleaseOption = new("--release", "Use Release configuration.");
    public static readonly Option<bool> RunDebugOption = new("--debug", "Use Debug configuration.");
    public static readonly Option<bool> RunNoBuildOption = new("--no-build", "Skip build.");
    public static readonly Option<string?> RunWorkingDirectoryOption = new("--working-directory", "Working directory.");
    public static readonly Option<string?> RunDiagnosticFormatOption = ChoiceOption("--diagnostic-format", "Diagnostic format.", "human", "json");
    public static readonly Option<string?> RunColorOption = ChoiceOption("--color", "Color mode.", "auto", "always", "never");
    public static readonly Argument<string?> CleanProjectArgument = new("project", () => null);
    public static readonly Option<string?> CleanManifestOption = new(["--manifest-path", "-m"], "Manifest path.");
    public static readonly Option<string?> CleanConfigurationOption = ChoiceOption(["--configuration", "-c"], "Clean configuration.", "debug", "release", "all");
    public static readonly Option<bool> CleanDryRunOption = new("--dry-run", "Show clean plan.");
    public static readonly Option<bool> CleanVerboseOption = new("--verbose", "Show removed paths.");
    public static readonly Argument<string[]> FormatFilesArgument = new("files", () => []);
    public static readonly Option<string?> FormatManifestOption = new(["--manifest-path", "-m"], "Manifest path.");
    public static readonly Option<string?> FormatProjectOption = new(["--project", "-p"], "Project path.");
    public static readonly Option<bool> FormatCheckOption = new("--check", "Check formatting.");
    public static readonly Option<bool> FormatStdoutOption = new("--stdout", "Write formatted file to stdout.");
    public static readonly Argument<string?> TestProjectArgument = new("project", () => null);
    public static readonly Option<string?> TestManifestOption = new(["--manifest-path", "-m"], "Manifest path.");

    static readonly RootCommand Root = BuildRoot();

    public static RootCommand Create() => Root;

    static RootCommand BuildRoot()
    {
        var root = new RootCommand("Martin project-oriented CLI");
        var @new = Command("new", [NewNameArgument], [NewPathOption, NewFrameworkOption, NewForceOption, NewNoGitOption]);
        @new.AddAlias("init");
        root.AddCommand(@new);
        var build = Command("build", [BuildProjectArgument], [BuildManifestOption, BuildOutputOption, BuildConfigurationOption, BuildReleaseOption, BuildDebugOption, BuildKeepGeneratedOption, BuildNoAppHostOption, BuildNoPdbOption, BuildDiagnosticFormatOption, BuildColorOption, QuietOption, VerboseOption]);
        build.AddAlias("b");
        root.AddCommand(build);
        var run = Command("run", [RunProjectArgument], [RunManifestOption, RunConfigurationOption, RunReleaseOption, RunDebugOption, RunNoBuildOption, RunWorkingDirectoryOption, RunDiagnosticFormatOption, RunColorOption, QuietOption, VerboseOption]);
        run.AddAlias("r");
        root.AddCommand(run);
        root.AddCommand(Command("clean", [CleanProjectArgument], [CleanManifestOption, CleanConfigurationOption, CleanDryRunOption, CleanVerboseOption]));
        var format = Command("format", [FormatFilesArgument], [FormatManifestOption, FormatProjectOption, FormatCheckOption, FormatStdoutOption]);
        format.AddAlias("fmt");
        root.AddCommand(format);
        root.AddCommand(Command("test", [TestProjectArgument], [TestManifestOption]));
        root.AddCommand(new Command("version", "Print toolchain version information."));
        return root;
    }

    static Command Command(string name, IEnumerable<Argument> arguments, IEnumerable<Option> options)
    {
        var command = new Command(name);
        foreach (var argument in arguments) command.AddArgument(argument);
        foreach (var option in options) command.AddOption(option);
        return command;
    }

    static Option<string?> ChoiceOption(string alias, string description, params string[] values) => ChoiceOption([alias], description, values);
    static Option<string?> ChoiceOption(string[] aliases, string description, params string[] values)
    {
        var option = new Option<string?>(aliases, description);
        option.FromAmong(values);
        return option;
    }
}

internal abstract record CommandOptions;
internal sealed record MartinParseResult(bool Success, CommandOptions? Command, IReadOnlyList<string> Errors)
{
    public static MartinParseResult Ok(CommandOptions command) => new(true, command, Array.Empty<string>());
    public static MartinParseResult Fail(IReadOnlyList<string> errors) => new(false, null, errors);
}
internal sealed record HelpCommandOptions(string? Command = null) : CommandOptions;
internal sealed record VersionCommandOptions : CommandOptions;
internal sealed record NewCommandOptions(string ProjectName, string? Path, bool Force, bool NoGit, string? Framework = null) : CommandOptions;
internal sealed record BuildCommandOptions(string? ProjectPath, string? ManifestPath, string? Output, BuildConfiguration Configuration, Verbosity Verbosity, bool KeepGenerated, DiagnosticFormat DiagnosticFormat = DiagnosticFormat.Human, ColorMode Color = ColorMode.Auto, bool UseAppHost = true, bool EmitPortablePdb = true) : CommandOptions { public bool Quiet => Verbosity == Verbosity.Quiet; }
internal sealed record RunCommandOptions(string? ProjectPath, string? ManifestPath, BuildConfiguration Configuration, bool NoBuild, IReadOnlyList<string> ProgramArguments, DiagnosticFormat DiagnosticFormat = DiagnosticFormat.Human, ColorMode Color = ColorMode.Auto, Verbosity Verbosity = Verbosity.Normal, string? WorkingDirectory = null) : CommandOptions;
internal sealed record CleanCommandOptions(string? ProjectPath, string? ManifestPath, bool DryRun, bool Verbose = false, string? Configuration = null) : CommandOptions;
internal sealed record TestCommandOptions(string? ProjectPath, string? ManifestPath) : CommandOptions;
internal sealed record FormatCommandOptions(string? ProjectPath, string? ManifestPath, bool Check, bool Stdout, IReadOnlyList<string> Files) : CommandOptions;
