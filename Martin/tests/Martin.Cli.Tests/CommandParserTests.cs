using Martin.Build;
using Martin.CommandLine;
using Xunit;

namespace Martin.Cli.Tests;

public sealed class CommandParserTests
{
    [Fact]
    public void BuildParsesFinalizedBehaviorOptions()
    {
        var result = MartinCommandParser.Parse(["build", "app", "--configuration", "release", "--diagnostic-format", "json", "--color", "never", "--verbose", "--no-app-host", "--no-pdb"]);

        Assert.True(result.Success, string.Join("\n", result.Errors));
        var options = Assert.IsType<BuildCommandOptions>(result.Command);
        Assert.Equal("app", options.ProjectPath);
        Assert.Equal(BuildConfiguration.Release, options.Configuration);
        Assert.Equal(DiagnosticFormat.Json, options.DiagnosticFormat);
        Assert.Equal(ColorMode.Never, options.Color);
        Assert.Equal(Verbosity.Detailed, options.Verbosity);
        Assert.False(options.UseAppHost);
        Assert.False(options.EmitPortablePdb);
    }

    [Fact]
    public void RunForwardsArgumentsAfterSeparatorExactly()
    {
        var result = MartinCommandParser.Parse(["run", "--working-directory", "/tmp/app", "--", "--not-a-martin-option", "value"]);

        Assert.True(result.Success, string.Join("\n", result.Errors));
        var options = Assert.IsType<RunCommandOptions>(result.Command);
        Assert.Equal("/tmp/app", options.WorkingDirectory);
        Assert.Equal(["--not-a-martin-option", "value"], options.ProgramArguments);
    }

    [Fact]
    public void CommandHelpDoesNotRequireAProject()
    {
        var result = MartinCommandParser.Parse(["test", "--help"]);

        Assert.True(result.Success, string.Join("\n", result.Errors));
        var options = Assert.IsType<HelpCommandOptions>(result.Command);
        Assert.Equal("test", options.Command);
    }

    [Fact]
    public void InvalidOptionValuesFailDuringParsing()
    {
        var result = MartinCommandParser.Parse(["build", "--diagnostic-format", "xml"]);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("--diagnostic-format", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("build", "--configuration", "release", "--debug", "Option '--debug' conflicts with '--configuration release'.")]
    [InlineData("build", "--configuration", "debug", "--release", "Option '--release' conflicts with '--configuration debug'.")]
    [InlineData("run", "app", "--manifest-path", "Martin.toml", "A project argument cannot be used together with '--manifest-path'.")]
    [InlineData("format", "a.martin", "b.martin", "--stdout", "Option '--stdout' conflicts with multiple format inputs.")]
    [InlineData("format", "--project", ".", "--stdout", "Option '--stdout' conflicts with project-wide in-place formatting.")]
    public void ConflictingOptionsFailDuringParsing(string command, string arg1, string arg2, string arg3, string expected)
    {
        var result = MartinCommandParser.Parse([command, arg1, arg2, arg3]);

        Assert.False(result.Success);
        Assert.Contains(expected, result.Errors);
    }

    [Fact]
    public void ForwardedRunArgumentsAreNotTreatedAsMartinConflicts()
    {
        var result = MartinCommandParser.Parse(["run", "--", "--quiet", "--verbose"]);

        Assert.True(result.Success, string.Join("\n", result.Errors));
        var options = Assert.IsType<RunCommandOptions>(result.Command);
        Assert.Equal(["--quiet", "--verbose"], options.ProgramArguments);
    }

}
