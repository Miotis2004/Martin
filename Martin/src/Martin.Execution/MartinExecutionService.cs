using System.Diagnostics;
using System.Text;
using Martin.Build;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Text;

namespace Martin.Execution;

public sealed class MartinExecutionService : IMartinExecutionService
{
    private readonly IExternalTerminalLauncher? _externalTerminalLauncher;

    public MartinExecutionService(
        IExternalTerminalLauncher? externalTerminalLauncher = null)
    {
        _externalTerminalLauncher = externalTerminalLauncher;
    }

    public Task<ExecutionResult> RunAsync(
        BuildResult build,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(
            build,
            new ExecutionOptions {
                Arguments = arguments
            },
            cancellationToken);
    }

    public async Task<ExecutionResult> RunAsync(
        BuildResult build,
        ExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(options);

        if (!build.Success ||
            string.IsNullOrWhiteSpace(build.EntryPointPath))
        {
            return new ExecutionResult {
                Status = ExecutionStatus.Failed,
                Diagnostics =
                    [
                        CreateDiagnostic(
                            "MRT4801",
                            "The build result does not contain an executable entry point.")
                    ]
            };
        }

        if (!File.Exists(build.EntryPointPath))
        {
            return new ExecutionResult {
                Status = ExecutionStatus.Failed,
                Diagnostics =
                    [
                        CreateDiagnostic(
                            "MRT4802",
                            $"The executable entry point '{build.EntryPointPath}' does not exist.")
                    ]
            };
        }

        var request = CreateRequest(build, options);

        if (options.UseExternalTerminal)
        {
            if (_externalTerminalLauncher is null)
            {
                return new ExecutionResult {
                    Status = ExecutionStatus.Failed,
                    Diagnostics =
                        [
                            CreateDiagnostic(
                                "MRT4806",
                                "External-terminal execution requires an external-terminal launcher.")
                        ]
                };
            }

            return await _externalTerminalLauncher.LaunchAsync(
                request,
                cancellationToken);
        }

        return await RunProcessAsync(request, cancellationToken);
    }

    private static ProcessExecutionRequest CreateRequest(
        BuildResult build,
        ExecutionOptions options)
    {
        var entryPointPath = build.EntryPointPath!;

        var isManagedAssembly = string.Equals(
            Path.GetExtension(entryPointPath),
            ".dll",
            StringComparison.OrdinalIgnoreCase);

        var arguments = new List<string>();

        if (isManagedAssembly)
            arguments.Add(entryPointPath);

        arguments.AddRange(options.Arguments);

        return new ProcessExecutionRequest {
            FileName = isManagedAssembly
                           ? "dotnet"
                           : entryPointPath,

            Arguments = arguments,

            WorkingDirectory =
                options.WorkingDirectory ??
                build.OutputDirectory ??
                Environment.CurrentDirectory,

            StandardInput = options.StandardInput,

            EnvironmentVariables = options.EnvironmentVariables
        };
    }

    private static async Task<ExecutionResult> RunProcessAsync(
        ProcessExecutionRequest request,
        CancellationToken cancellationToken)
    {
        using var process = new Process {
            StartInfo = CreateStartInfo(request)
        };

        Task<string>? standardOutputTask = null;
        Task<string>? standardErrorTask = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!process.Start())
            {
                return new ExecutionResult {
                    Status = ExecutionStatus.Failed,
                    Diagnostics =
                        [
                            CreateDiagnostic(
                                "MRT4811",
                                $"The process '{request.FileName}' could not be started.")
                        ]
                };
            }

            /*
             * Start reading redirected output immediately. These reads do not
             * use the caller's cancellation token because they need to finish
             * after the process is terminated during cancellation.
             */
            if (request.RedirectStandardOutput)
                standardOutputTask = process.StandardOutput.ReadToEndAsync();

            if (request.RedirectStandardError)
                standardErrorTask = process.StandardError.ReadToEndAsync();

            if (request.StandardInput is not null)
            {
                await process.StandardInput.WriteAsync(
                    request.StandardInput.AsMemory(),
                    cancellationToken);

                await process.StandardInput.FlushAsync(cancellationToken);
                process.StandardInput.Close();
            }

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return await CreateCancelledResultAsync(
                    process,
                    standardOutputTask,
                    standardErrorTask);
            }

            /*
             * The process may exit at approximately the same time cancellation
             * is requested. Cancellation should take priority in that race.
             */
            if (cancellationToken.IsCancellationRequested)
            {
                return await CreateCancelledResultAsync(
                    process,
                    standardOutputTask,
                    standardErrorTask);
            }

            var standardOutput =
                await CompleteOutputReadAsync(standardOutputTask);

            var standardError =
                await CompleteOutputReadAsync(standardErrorTask);

            return new ExecutionResult {
                Status = process.ExitCode == 0
                             ? ExecutionStatus.Completed
                             : ExecutionStatus.Failed,

                ExitCode = process.ExitCode,
                StandardOutput = standardOutput,
                StandardError = standardError
            };
        }
        catch (OperationCanceledException)
        {
            return await CreateCancelledResultAsync(
                process,
                standardOutputTask,
                standardErrorTask);
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or
                System.ComponentModel.Win32Exception or
                    IOException or
                        UnauthorizedAccessException or
                            NotSupportedException)
        {
            KillProcessTree(process);
            await WaitForExitAfterCancellationAsync(process);

            return new ExecutionResult {
                Status = ExecutionStatus.Failed,
                StandardOutput =
                    await CompleteOutputReadAsync(standardOutputTask),

                StandardError =
                    await CompleteOutputReadAsync(standardErrorTask),

                Diagnostics =
                    [
                        CreateDiagnostic(
                            "MRT4811",
                            ex.Message)
                    ]
            };
        }
    }

    private static ProcessStartInfo CreateStartInfo(
        ProcessExecutionRequest request)
    {
        var fileName = ResolveExecutablePath(
            request.FileName,
            request.EnvironmentVariables);

        var isWindowsCommandScript =
            OperatingSystem.IsWindows() &&
            (string.Equals(
                 Path.GetExtension(fileName),
                 ".cmd",
                 StringComparison.OrdinalIgnoreCase) ||

             string.Equals(
                 Path.GetExtension(fileName),
                 ".bat",
                 StringComparison.OrdinalIgnoreCase));

        var processFileName = isWindowsCommandScript
                                  ? GetCommandProcessorPath()
                                  : fileName;

        var startInfo = new ProcessStartInfo(processFileName) {
            RedirectStandardInput =
                request.StandardInput is not null,

            RedirectStandardOutput =
                request.RedirectStandardOutput,

            RedirectStandardError =
                request.RedirectStandardError,

            UseShellExecute = false,
            WorkingDirectory = request.WorkingDirectory,
            CreateNoWindow = true
        };

        foreach (var environmentVariable in request.EnvironmentVariables)
        {
            var name = environmentVariable.Key;
            var value = environmentVariable.Value;

            if (value is null)
                startInfo.Environment.Remove(name);
            else
                startInfo.Environment[name] = value;
        }

        if (isWindowsCommandScript)
        {
            startInfo.StandardOutputEncoding = Encoding.Unicode;
            startInfo.StandardErrorEncoding = Encoding.Unicode;

            ConfigureWindowsCommandScript(
                startInfo,
                fileName,
                request.Arguments);
        }
        else
        {
            foreach (var argument in request.Arguments)
                startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void ConfigureWindowsCommandScript(
        ProcessStartInfo startInfo,
        string scriptPath,
        IReadOnlyList<string> arguments)
    {
        /*
         * cmd.exe requires an extra outer pair of quotes when the command
         * itself begins with a quoted executable or script path.
         *
         * The resulting command has this form:
         *
         * cmd.exe /d /s /c ""C:\path\script.cmd" "argument one" "argument two""
         */
        var commandParts = new List<string> {
            QuoteWindowsCommandArgument(scriptPath)
        };

        commandParts.AddRange(
            arguments.Select(QuoteWindowsCommandArgument));

        var command = string.Join(" ", commandParts);

        startInfo.Arguments = $"/d /u /s /c \"{command}\"";
    }

    private static string ResolveExecutablePath(
        string fileName,
        IReadOnlyDictionary<string, string?> environmentVariables)
    {
        if (Path.IsPathFullyQualified(fileName) ||
            !string.IsNullOrEmpty(Path.GetDirectoryName(fileName)))
        {
            return fileName;
        }

        var path = GetEnvironmentValue(
            environmentVariables,
            OperatingSystem.IsWindows() ? "Path" : "PATH");

        if (string.IsNullOrWhiteSpace(path))
        {
            path = Environment.GetEnvironmentVariable(
                OperatingSystem.IsWindows() ? "Path" : "PATH");
        }

        if (string.IsNullOrWhiteSpace(path))
            return fileName;

        foreach (var directory in path.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries |
                         StringSplitOptions.TrimEntries))
        {
            foreach (var candidateName in
                         GetExecutableCandidateNames(fileName))
            {
                var candidate = Path.Combine(
                    directory,
                    candidateName);

                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
        }

        return fileName;
    }

    private static IEnumerable<string> GetExecutableCandidateNames(
        string fileName)
    {
        yield return fileName;

        if (!OperatingSystem.IsWindows() ||
            !string.IsNullOrEmpty(Path.GetExtension(fileName)))
        {
            yield break;
        }

        // Always check command scripts explicitly. This keeps execution
        // deterministic even when PATHEXT is missing or customized.
        yield return fileName + ".exe";
        yield return fileName + ".cmd";
        yield return fileName + ".bat";
        yield return fileName + ".com";
    }

    private static string? GetEnvironmentValue(
        IReadOnlyDictionary<string, string?> environmentVariables,
        string name)
    {
        if (environmentVariables.TryGetValue(name, out var value))
            return value;

        if (!OperatingSystem.IsWindows())
            return null;

        // Windows environment-variable names are case-insensitive, but
        // IReadOnlyDictionary may use a case-sensitive comparer.
        foreach (var pair in environmentVariables)
        {
            if (string.Equals(
                    pair.Key,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static string GetCommandProcessorPath()
    {
        return Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
    }

    private static string QuoteWindowsCommandArgument(
        string argument)
    {
        /*
         * Double quotes inside a quoted cmd.exe argument are represented
         * by two consecutive quote characters.
         */
        return "\"" +
               argument.Replace("\"", "\"\"") +
               "\"";
    }

    private static async Task<ExecutionResult>
    CreateCancelledResultAsync(
        Process process,
        Task<string>? standardOutputTask,
        Task<string>? standardErrorTask)
    {
        KillProcessTree(process);
        await WaitForExitAfterCancellationAsync(process);

        return new ExecutionResult {
            Status = ExecutionStatus.Cancelled,

            StandardOutput =
                await CompleteOutputReadAsync(standardOutputTask),

            StandardError =
                await CompleteOutputReadAsync(standardErrorTask),

            Diagnostics =
                [
                    CreateDiagnostic(
                        "MRT4810",
                        "Program execution was cancelled.")
                ]
        };
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            /*
             * The process may have exited between HasExited and Kill,
             * or the operating system may deny process-tree termination.
             * Cancellation cleanup is best effort.
             */
        }
    }

    private static async Task WaitForExitAfterCancellationAsync(
        Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                await process
                    .WaitForExitAsync(CancellationToken.None)
                    .WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch
        {
            // Process cleanup is best effort.
        }
    }

    private static async Task<string> CompleteOutputReadAsync(
        Task<string>? outputTask)
    {
        if (outputTask is null)
            return string.Empty;

        try
        {
            return await outputTask.WaitAsync(
                TimeSpan.FromSeconds(2));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static Diagnostic CreateDiagnostic(
        string code,
        string message)
    {
        return new Diagnostic(
            code,
            DiagnosticSeverity.Error,
            message,
            new TextLocation(
                SourceText.From(string.Empty),
                new TextSpan(0, 0)));
    }
}
