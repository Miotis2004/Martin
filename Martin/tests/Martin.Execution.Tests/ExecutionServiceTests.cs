using Martin.Build;
using Martin.Execution;
using Xunit;

namespace Martin.Execution.Tests;

public sealed class ExecutionServiceTests : IDisposable
{
    readonly string _temp = Path.Combine(Path.GetTempPath(), "martin-execution-tests-" + Guid.NewGuid().ToString("N"));

    public ExecutionServiceTests()
    {
        Directory.CreateDirectory(_temp);
    }

    [Fact]
    public async Task App_host_execution_captures_process_contract()
    {
        var workingDirectory = Path.Combine(_temp, "work");
        Directory.CreateDirectory(workingDirectory);
        var appHost = CreateExecutable(
            "apphost",
            "printf '%s' \"$1|$2|$(pwd)|$MARTIN_EXEC_TEST|$(cat)\"\nprintf '%s' 'err' >&2\nexit 7\n",
            "set /p standardInput=\r\n<nul set /p dummy=%~1^|%~2^|%CD%^|%MARTIN_EXEC_TEST%^|%standardInput%\r\n<nul set /p dummy=err>&2\r\nexit /b 7\r\n");
        var build = new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp };

        var result = await new MartinExecutionService().RunAsync(build, new ExecutionOptions
        {
            Arguments = ["arg with spaces", "unicode-π"],
            WorkingDirectory = workingDirectory,
            StandardInput = "stdin",
            EnvironmentVariables = new Dictionary<string, string?> { ["MARTIN_EXEC_TEST"] = "env" }
        });

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Equal(7, result.ExitCode);
        Assert.Equal($"arg with spaces|unicode-π|{workingDirectory}|env|stdin", result.StandardOutput);
        Assert.Equal("err", result.StandardError);
    }

    [Fact]
    public async Task Managed_dll_execution_uses_dotnet_and_output_directory_working_directory()
    {
        var tools = Path.Combine(_temp, "tools");
        var output = Path.Combine(_temp, "output");
        Directory.CreateDirectory(tools);
        Directory.CreateDirectory(output);
        var fakeDotnet = CreateExecutable(
            Path.Combine(tools, "dotnet"),
            "printf '%s' \"$1|$2|$(pwd)\"\n",
            "<nul set /p dummy=%~1^|%~2^|%CD%\r\n");
        var dll = Path.Combine(output, "application.dll");
        File.WriteAllText(dll, string.Empty);
        var pathName = OperatingSystem.IsWindows() ? "Path" : "PATH";
        var build = new BuildResult { Success = true, EntryPointPath = dll, OutputDirectory = output };

        var originalPath = Environment.GetEnvironmentVariable(pathName);
        Environment.SetEnvironmentVariable(pathName, tools + Path.PathSeparator + originalPath);
        ExecutionResult result;
        try
        {
            result = await new MartinExecutionService().RunAsync(build, new ExecutionOptions
            {
                Arguments = ["arg"],
                EnvironmentVariables = new Dictionary<string, string?>
                {
                    [pathName] = tools + Path.PathSeparator + originalPath
                }
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable(pathName, originalPath);
        }

        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"{dll}|arg|{output}", result.StandardOutput);
        Assert.True(File.Exists(fakeDotnet));
    }

    [Fact]
    public async Task Cancellation_kills_process_tree_and_reports_cancelled()
    {
        var marker = Path.Combine(_temp, "marker");
        var appHost = CreateExecutable(
            "slow",
            $"trap 'exit 0' TERM\ntouch '{marker}'\nsleep 30\n",
            $"echo.> \"{marker}\"\r\nping 127.0.0.1 -n 30 > nul\r\n");
        var build = new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp };
        using var cts = new CancellationTokenSource();
        var run = new MartinExecutionService().RunAsync(build, new ExecutionOptions(), cts.Token);

        SpinWait.SpinUntil(() => File.Exists(marker), TimeSpan.FromSeconds(5));
        cts.Cancel();
        var result = await run;

        Assert.Equal(ExecutionStatus.Cancelled, result.Status);
        Assert.True(result.WasCancelled);
    }

    [Fact]
    public async Task Invalid_build_result_is_rejected()
    {
        var result = await new MartinExecutionService().RunAsync(new BuildResult(), new ExecutionOptions());

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MRT4801");
    }

    [Fact]
    public async Task Missing_entry_point_is_reported()
    {
        var result = await new MartinExecutionService().RunAsync(
            new BuildResult { Success = true, EntryPointPath = Path.Combine(_temp, "missing") },
            new ExecutionOptions());

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MRT4802");
    }

    [Fact]
    public async Task External_terminal_uses_terminal_launcher()
    {
        var appHost = CreateExecutable("terminal", "exit 0\n", "exit /b 0\r\n");
        var launcher = new CapturingTerminalLauncher();
        var build = new BuildResult { Success = true, EntryPointPath = appHost, OutputDirectory = _temp };

        var result = await new MartinExecutionService(launcher).RunAsync(build, new ExecutionOptions
        {
            Arguments = ["arg"],
            UseExternalTerminal = true
        });

        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.NotNull(launcher.Request);
        Assert.Equal(appHost, launcher.Request!.FileName);
        Assert.Equal(["arg"], launcher.Request.Arguments);
        Assert.Equal(_temp, launcher.Request.WorkingDirectory);
    }

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
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
