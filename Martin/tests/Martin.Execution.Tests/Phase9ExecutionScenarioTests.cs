using Martin.Build;
using Martin.Execution;
using Xunit;

namespace Martin.Execution.Tests;

public sealed class Phase9ExecutionScenarioTests : IDisposable
{
    readonly string _temp = Path.Combine(Path.GetTempPath(), "martin-execution-scenarios-" + Guid.NewGuid().ToString("N"));

    public Phase9ExecutionScenarioTests()
    {
        Directory.CreateDirectory(_temp);
    }

    [Fact]
    public async Task ManagedAssemblyExecutesThroughDotNet()
    {
        var tools = Path.Combine(_temp, "tools");
        var output = Path.Combine(_temp, "output");
        Directory.CreateDirectory(tools);
        Directory.CreateDirectory(output);
        var fakeDotnet = CreateExecutable(
            Path.Combine(tools, "dotnet"),
            "printf '%s' \"$1|$2\"\n",
            "<nul set /p dummy=%~1^|%~2\r\n");
        var dll = Path.Combine(output, "program.dll");
        File.WriteAllText(dll, string.Empty);
        var pathName = OperatingSystem.IsWindows() ? "Path" : "PATH";

        var result = await RunAsync(
            new BuildResult { Success = true, EntryPointPath = dll, OutputDirectory = output },
            new ExecutionOptions
            {
                Arguments = ["from-martin"],
                EnvironmentVariables = new Dictionary<string, string?>
                {
                    [pathName] = tools + Path.PathSeparator + Environment.GetEnvironmentVariable(pathName)
                }
            });

        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.Equal($"{dll}|from-martin", result.StandardOutput);
        Assert.True(File.Exists(fakeDotnet));
    }

    [Fact]
    public async Task AppHostExecutesDirectly()
    {
        var appHost = CreateExecutable("apphost", "printf '%s' apphost\n", "<nul set /p dummy=apphost\r\n");

        var result = await RunAsync(new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp });

        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.Equal("apphost", result.StandardOutput);
    }

    [Fact]
    public async Task ArgumentsAreForwardedExactly()
    {
        var appHost = CreateExecutable("arguments", "printf '%s' \"$1|$2|$#\"\n", "<nul set /p dummy=%~1^|%~2^|%*\r\n");

        var result = await RunAsync(
            new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp },
            new ExecutionOptions { Arguments = ["--flag", "value"] });

        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.StartsWith("--flag|value|", result.StandardOutput);
    }

    [Fact]
    public async Task ArgumentsWithSpacesArePreserved()
    {
        var appHost = CreateExecutable("spaced-arguments", "printf '%s' \"$1\"\n", "<nul set /p dummy=%~1\r\n");

        var result = await RunAsync(
            new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp },
            new ExecutionOptions { Arguments = ["argument with spaces"] });

        Assert.Equal("argument with spaces", result.StandardOutput);
    }

    [Fact]
    public async Task UnicodeArgumentsArePreserved()
    {
        var appHost = CreateExecutable("unicode-arguments", "printf '%s' \"$1\"\n", "<nul set /p dummy=%~1\r\n");

        var result = await RunAsync(
            new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp },
            new ExecutionOptions { Arguments = ["unicode-π-雪"] });

        Assert.Equal("unicode-π-雪", result.StandardOutput);
    }

    [Fact]
    public async Task WorkingDirectoryIsApplied()
    {
        var work = Path.Combine(_temp, "work");
        Directory.CreateDirectory(work);
        var appHost = CreateExecutable("working-directory", "printf '%s' \"$(pwd)\"\n", "<nul set /p dummy=%CD%\r\n");

        var result = await RunAsync(
            new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp },
            new ExecutionOptions { WorkingDirectory = work });

        Assert.Equal(Path.GetFullPath(work), Path.GetFullPath(result.StandardOutput));
    }

    [Fact]
    public async Task StandardInputIsForwarded()
    {
        var appHost = CreateExecutable("stdin", "cat", "more");

        var result = await RunAsync(
            new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp },
            new ExecutionOptions { StandardInput = "standard input" });

        Assert.Equal("standard input", result.StandardOutput.TrimEnd('\r', '\n'));
    }

    [Fact]
    public async Task StandardOutputIsCaptured()
    {
        var appHost = CreateExecutable("stdout", "printf '%s' output\n", "<nul set /p dummy=output\r\n");

        var result = await RunAsync(new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp });

        Assert.Equal("output", result.StandardOutput);
    }

    [Fact]
    public async Task StandardErrorIsCaptured()
    {
        var appHost = CreateExecutable("stderr", "printf '%s' error >&2\n", "<nul set /p dummy=error 1>&2\r\n");

        var result = await RunAsync(new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp });

        Assert.Equal("error", result.StandardError);
    }

    [Fact]
    public async Task ExitCodeIsPreserved()
    {
        var appHost = CreateExecutable("exit-code", "exit 42\n", "exit /b 42\r\n");

        var result = await RunAsync(new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp });

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Equal(42, result.ExitCode);
    }

    [Fact]
    public async Task EnvironmentVariablesAreApplied()
    {
        var appHost = CreateExecutable("environment", "printf '%s' \"$MARTIN_EXEC_ENV\"\n", "<nul set /p dummy=%MARTIN_EXEC_ENV%\r\n");

        var result = await RunAsync(
            new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp },
            new ExecutionOptions { EnvironmentVariables = new Dictionary<string, string?> { ["MARTIN_EXEC_ENV"] = "applied" } });

        Assert.Equal("applied", result.StandardOutput);
    }

    [Fact]
    public async Task ExecutionCancellationKillsProcessTree()
    {
        var marker = Path.Combine(_temp, "ticks.txt");
        var ready = Path.Combine(_temp, "ready.txt");
        var appHost = CreateExecutable(
            "process-tree",
            $"(while true; do echo tick >> '{marker}'; sleep 1; done) & echo ready > '{ready}'; wait\n",
            $"start /b cmd /d /s /c \"for /l %%i in (1,0,2) do @echo tick>>\"\"{marker}\"\" & ping 127.0.0.1 -n 2 > nul\"\r\necho ready>\"{ready}\"\r\nping 127.0.0.1 -n 30 > nul\r\n");
        using var cts = new CancellationTokenSource();
        var run = RunAsync(new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp }, new ExecutionOptions(), cts.Token);

        Assert.True(SpinWait.SpinUntil(() => File.Exists(ready) && File.Exists(marker), TimeSpan.FromSeconds(10)));
        cts.Cancel();
        var result = await run;
        var tickCountAfterCancel = CountLines(marker);
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.Equal(ExecutionStatus.Cancelled, result.Status);
        Assert.Equal(tickCountAfterCancel, CountLines(marker));
    }

    [Fact]
    public async Task CancelledExecutionReportsCancelled()
    {
        var ready = Path.Combine(_temp, "cancel-ready.txt");
        var appHost = CreateExecutable("cancelled", $"echo ready > '{ready}'; sleep 30\n", $"echo ready>\"{ready}\"\r\nping 127.0.0.1 -n 30 > nul\r\n");
        using var cts = new CancellationTokenSource();
        var run = RunAsync(new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp }, new ExecutionOptions(), cts.Token);

        Assert.True(SpinWait.SpinUntil(() => File.Exists(ready), TimeSpan.FromSeconds(10)));
        cts.Cancel();
        var result = await run;

        Assert.Equal(ExecutionStatus.Cancelled, result.Status);
        Assert.True(result.WasCancelled);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MRT4810");
    }

    [Fact]
    public async Task InvalidBuildResultIsRejected()
    {
        var result = await RunAsync(new BuildResult());

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MRT4801");
    }

    [Fact]
    public async Task MissingEntryPointIsReported()
    {
        var result = await RunAsync(new BuildResult { Success = true, EntryPointPath = Path.Combine(_temp, "missing") });

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MRT4802");
    }

    [Fact]
    public async Task ExternalTerminalUsesTerminalLauncher()
    {
        var appHost = CreateExecutable("terminal", "exit 0\n", "exit /b 0\r\n");
        var launcher = new CapturingTerminalLauncher();

        var result = await new MartinExecutionService(launcher).RunAsync(
            new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp },
            new ExecutionOptions { Arguments = ["external"], UseExternalTerminal = true });

        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.NotNull(launcher.Request);
        Assert.Equal(appHost, launcher.Request!.FileName);
        Assert.Equal(["external"], launcher.Request.Arguments);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temp, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    static Task<ExecutionResult> RunAsync(BuildResult build, ExecutionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return new MartinExecutionService().RunAsync(build, options ?? new ExecutionOptions(), cancellationToken);
    }

    string CreateExecutable(string name, string unixBody, string windowsBody)
    {
        var path = Path.IsPathRooted(name) ? name : Path.Combine(_temp, name);

        if (OperatingSystem.IsWindows())
        {
            path = Path.ChangeExtension(path, ".cmd");
            File.WriteAllText(path, "@echo off\r\n" + windowsBody);
            return path;
        }

        var shell = File.Exists("/bin/sh") ? "/bin/sh" : "/usr/bin/env sh";
        File.WriteAllText(path, $"#!{shell}\n" + unixBody);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    static int CountLines(string path)
    {
        return File.Exists(path)
                   ? File.ReadAllLines(path).Length
                   : 0;
    }

    sealed class CapturingTerminalLauncher : IExternalTerminalLauncher
    {
        public ProcessExecutionRequest? Request { get; private set; }

        public Task<ExecutionResult> LaunchAsync(ProcessExecutionRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new ExecutionResult { Status = ExecutionStatus.Completed, ExitCode = 0 });
        }
    }
}
