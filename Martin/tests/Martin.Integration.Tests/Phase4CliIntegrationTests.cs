using Xunit;
using System.Diagnostics;

namespace Martin.Integration.Tests;

public sealed class Phase4CliIntegrationTests
{
    [Fact]
    public async Task Martin_cli_can_create_build_run_clean_and_report_test_stub()
    {
        using var temp = new TempDir();
        var cliProject = FindCliProject();

        var created = await Dotnet(cliProject, temp.Path, "new", "HelloMartin", "--no-git");
        Assert.Equal(0, created.ExitCode);

        var projectDir = Path.Combine(temp.Path, "HelloMartin");
        var built = await Dotnet(cliProject, projectDir, "build", "--quiet");
        Assert.Equal(0, built.ExitCode);
        Assert.True(Directory.Exists(Path.Combine(projectDir, "bin", "Debug", "net8.0")));

        var ran = await Dotnet(cliProject, projectDir, "run", "--quiet");
        Assert.Equal(0, ran.ExitCode);
        Assert.Contains("Hello from Martin!", ran.Stdout);

        var test = await Dotnet(cliProject, projectDir, "test");
        Assert.Equal(7, test.ExitCode);
        Assert.Contains("MRT4901", test.Stderr);

        await EnsureProcessStoppedAsync(Path.GetFileName(projectDir));

        var cleaned = await CleanWithRetryAsync(cliProject, projectDir);
        Assert.Equal(0, cleaned.ExitCode);
        Assert.False(Directory.Exists(Path.Combine(projectDir, "bin")));
    }

    static async Task<(int ExitCode, string Stdout, string Stderr)> CleanWithRetryAsync(string cliProject, string projectDir)
    {
        (int ExitCode, string Stdout, string Stderr) cleaned = (0, string.Empty, string.Empty);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            cleaned = await Dotnet(cliProject, projectDir, "clean");
            if (cleaned.ExitCode == 0)
                break;

            if (attempt < 5)
                await Task.Delay(200);
        }

        return cleaned;
    }

    static async Task EnsureProcessStoppedAsync(string processName, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
                return;

            foreach (var process in processes)
            {
                using (process)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                            await process.WaitForExitAsync();
                        }
                    }
                    catch
                    {
                        // Best effort: process access can fail transiently, so retry until timeout.
                    }
                }
            }

            await Task.Delay(200);
        }
    }

    static string FindCliProject()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Martin.Cli", "Martin.Cli.csproj");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate src/Martin.Cli/Martin.Cli.csproj from the test output directory.");
    }

    static async Task<(int ExitCode, string Stdout, string Stderr)> Dotnet(string project, string cwd, params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet") { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("run"); psi.ArgumentList.Add("--project"); psi.ArgumentList.Add(project); psi.ArgumentList.Add("--"); foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return (p.ExitCode, stdout, stderr);
    }

    sealed class TempDir : IDisposable { public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MartinCliTests", Guid.NewGuid().ToString("N")); public TempDir() => Directory.CreateDirectory(Path); public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); } }
}
