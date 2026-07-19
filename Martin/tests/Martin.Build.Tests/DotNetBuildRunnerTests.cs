using System.Runtime.InteropServices;
using Xunit;

namespace Martin.Build.Tests;

public sealed class DotNetBuildRunnerTests
{
    [Fact]
    public async Task Runner_uses_safe_argument_list_and_captures_output()
    {
        var workingDirectory = CreateTempDirectory();
        var captureFile = Path.Combine(workingDirectory, "args.txt");
        var executable = CreateEchoExecutable(workingDirectory, 7);
        var projectPath = Path.Combine(workingDirectory, "project with spaces.csproj");

        var result = await new DotNetBuildRunner(executable).RunAsync(new DotNetBuildRequest
        {
            ProjectPath = projectPath,
            WorkingDirectory = workingDirectory,
            Configuration = BuildConfiguration.Release,
            AdditionalArguments = ["/p:DefineConstants=A B", "/warnaserror"]
        });

        Assert.True(result.Started);
        Assert.True(result.Completed);
        Assert.False(result.WasCancelled);
        Assert.Equal(7, result.ExitCode);
        Assert.Contains("stdout from runner", result.StandardOutput);
        Assert.Contains("stderr from runner", result.StandardError);

        var arguments = await File.ReadAllLinesAsync(captureFile);
        Assert.Equal([
            "build",
            projectPath,
            "--configuration",
            "Release",
            "--nologo",
            "/p:DefineConstants=A B",
            "/warnaserror"
        ], arguments);
    }

    [Fact]
    public async Task Runner_reports_startup_failure()
    {
        var result = await new DotNetBuildRunner(Path.Combine(CreateTempDirectory(), "missing-dotnet")).RunAsync(new DotNetBuildRequest
        {
            ProjectPath = "missing.csproj",
            WorkingDirectory = Path.GetTempPath(),
            Configuration = BuildConfiguration.Debug
        });

        Assert.False(result.Started);
        Assert.False(result.Completed);
        Assert.Null(result.ExitCode);
    }

    [Fact]
    public async Task Runner_honors_cancellation_before_startup()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => new DotNetBuildRunner().RunAsync(new DotNetBuildRequest
        {
            ProjectPath = "project.csproj",
            WorkingDirectory = Path.GetTempPath(),
            Configuration = BuildConfiguration.Debug
        }, cancellation.Token));
    }

    [Fact]
    public async Task Runner_reports_cancellation_during_execution()
    {
        var workingDirectory = CreateTempDirectory();
        var executable = CreateSlowExecutable(workingDirectory);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        var result = await new DotNetBuildRunner(executable).RunAsync(new DotNetBuildRequest
        {
            ProjectPath = "project.csproj",
            WorkingDirectory = workingDirectory,
            Configuration = BuildConfiguration.Debug
        }, cancellation.Token);

        Assert.True(result.Started);
        Assert.False(result.Completed);
        Assert.True(result.WasCancelled);
    }


    [Fact]
    public async Task Runner_kills_child_process_tree_on_cancellation()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var workingDirectory = CreateTempDirectory();
        var executable = CreateChildProcessExecutable(workingDirectory);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var result = await new DotNetBuildRunner(executable).RunAsync(new DotNetBuildRequest
        {
            ProjectPath = "project.csproj",
            WorkingDirectory = workingDirectory,
            Configuration = BuildConfiguration.Debug
        }, cancellation.Token);

        Assert.True(result.WasCancelled);

        var childPidFile = Path.Combine(workingDirectory, "child.pid");
        Assert.True(File.Exists(childPidFile));
        var childPid = (await File.ReadAllTextAsync(childPidFile)).Trim();
        await Task.Delay(250);
        Assert.False(Directory.Exists(Path.Combine("/proc", childPid)));
    }

    static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MartinBuildRunnerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    static string CreateEchoExecutable(string directory, int exitCode)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var path = Path.Combine(directory, "fake-dotnet.cmd");
            File.WriteAllText(path, "@echo off\r\n" +
                "for %%A in (%*) do echo %%~A>> args.txt\r\n" +
                "echo stdout from runner\r\n" +
                "echo stderr from runner 1>&2\r\n" +
                $"exit /b {exitCode}\r\n");
            return path;
        }

        var script = Path.Combine(directory, "fake-dotnet.sh");
        File.WriteAllText(script, "#!/usr/bin/env sh\n" +
            ": > args.txt\n" +
            "for arg in \"$@\"; do printf '%s\\n' \"$arg\" >> args.txt; done\n" +
            "printf '%s\\n' 'stdout from runner'\n" +
            "printf '%s\\n' 'stderr from runner' >&2\n" +
            $"exit {exitCode}\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return script;
    }

    static string CreateSlowExecutable(string directory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var path = Path.Combine(directory, "slow-dotnet.cmd");
            File.WriteAllText(path, "@echo off\r\nping 127.0.0.1 -n 5 > nul\r\n");
            return path;
        }

        var script = Path.Combine(directory, "slow-dotnet.sh");
        File.WriteAllText(script, "#!/usr/bin/env sh\nsleep 5\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return script;
    }

    static string CreateChildProcessExecutable(string directory)
    {
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "The child-process script test requires a Unix-like platform.");

        var script = Path.Combine(directory, "child-dotnet.sh");

        File.WriteAllText(
            script,
            "#!/usr/bin/env sh\n" +
            "sleep 30 &\n" +
            "printf '%s\\n' \"$!\" > child.pid\n" +
            "wait\n");

        File.SetUnixFileMode(
            script,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute);

        return script;
    }

}
