using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace Martin.Cli.Tests;

public sealed class CliProcessTests
{
    [Fact] public void CliAssemblyLoads() => Assert.NotNull(Assembly.Load("Martin.Cli"));
    [Fact] public async Task HelpCommandWritesUsageToStdout() { var r = await RunAsync("--help"); Assert.Equal(0, r.ExitCode); Assert.Contains("Usage: martin", r.StandardOutput); Assert.Equal(string.Empty, r.StandardError); }
    [Fact] public async Task VersionCommandWritesVersionToStdout() { var r = await RunAsync("--version"); Assert.Equal(0, r.ExitCode); Assert.Contains("Martin CLI", r.StandardOutput); Assert.Contains("Language version:", r.StandardOutput); }
    [Fact] public async Task UnknownCommandFailsOnStderr() { var r = await RunAsync("definitely-unknown"); Assert.NotEqual(0, r.ExitCode); Assert.Contains("MRT4501", r.StandardError); }
    [Fact] public async Task NewCommandRequiresProjectName() { var r = await RunAsync("new"); Assert.NotEqual(0, r.ExitCode); Assert.Contains("MRT4506", r.StandardError); }
    [Fact] public async Task FormatMissingFileFailsOnStderr() { var r = await RunAsync("format", Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".martin")); Assert.NotEqual(0, r.ExitCode); Assert.Contains("MRT4601", r.StandardError); }

    [Fact] public async Task UnknownOptionFailsBeforeProjectDiscovery() { var r = await RunAsync("build", "--definitely-unknown"); Assert.Equal(2, r.ExitCode); Assert.Contains("MRT4502", r.StandardError); Assert.Contains("Unknown option", r.StandardError); }
    [Fact] public async Task MissingOptionValueFailsBeforeProjectDiscovery() { var r = await RunAsync("build", "--output"); Assert.Equal(2, r.ExitCode); Assert.Contains("requires a value", r.StandardError); }
    [Fact] public async Task ConflictingConfigurationOptionsFailBeforeProjectDiscovery() { var r = await RunAsync("run", "--release", "--debug"); Assert.Equal(2, r.ExitCode); Assert.Contains("cannot be used together", r.StandardError); }
    [Fact] public async Task RunAllowsProgramArgumentsAfterDoubleDash() { var r = await RunAsync("run", "--no-build", "--", "--child-option", "value"); Assert.DoesNotContain("Unknown option '--child-option'", r.StandardError); }
    [Fact] public async Task NewCommandWritesSuccessToStdout() { var root = Temp(); try { var r = await RunAsync("new", "Hello", "--path", root, "--no-git"); Assert.Equal(0, r.ExitCode); Assert.Contains("Created Martin project", r.StandardOutput); Assert.Equal(string.Empty, r.StandardError); } finally { Directory.Delete(root, true); } }

    static string Temp() { var p = Path.Combine(Path.GetTempPath(), "MartinCliTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(p); return p; }
    static async Task<Result> RunAsync(params string[] args) { var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = RepoRoot() }; psi.ArgumentList.Add("run"); psi.ArgumentList.Add("--project"); psi.ArgumentList.Add(Path.Combine("Martin", "src", "Martin.Cli", "Martin.Cli.csproj")); psi.ArgumentList.Add("--"); foreach (var a in args) psi.ArgumentList.Add(a); using var p = Process.Start(psi)!; var so = p.StandardOutput.ReadToEndAsync(); var se = p.StandardError.ReadToEndAsync(); using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(150)); await p.WaitForExitAsync(cts.Token); return new(p.ExitCode, await so, await se); }
    static string RepoRoot() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d != null && !File.Exists(Path.Combine(d.FullName, "README.md"))) d = d.Parent; return d?.FullName ?? throw new InvalidOperationException("Repository root not found."); }
    sealed record Result(int ExitCode, string StandardOutput, string StandardError);
}
