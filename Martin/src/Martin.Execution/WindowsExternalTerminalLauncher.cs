using System.Diagnostics;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Text;

namespace Martin.Execution;

public sealed class WindowsExternalTerminalLauncher : IExternalTerminalLauncher
{
    private const string WindowsTerminalExecutable = "wt.exe";

    public Task<ExecutionResult> LaunchAsync(
        ProcessExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(Failure(
                "MRT4820",
                "External-terminal execution is currently supported only on Windows."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var startInfo = CreateTerminalStartInfo(request);
            using var process = new Process { StartInfo = startInfo };

            if (!process.Start())
            {
                return Task.FromResult(Failure(
                    "MRT4821",
                    "The external terminal could not be started."));
            }

            return Task.FromResult(new ExecutionResult {
                Status = ExecutionStatus.Completed,
                Diagnostics =
                    [
                        Diagnostic(
                            "MRT4822",
                            DiagnosticSeverity.Info,
                            "The program was launched in a detached external terminal. Stop controls integrated execution only.")
                    ]
            });
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(new ExecutionResult {
                Status = ExecutionStatus.Cancelled,
                Diagnostics =
                    [
                        Diagnostic(
                            "MRT4823",
                            DiagnosticSeverity.Warning,
                            "External-terminal launch was cancelled before the detached process started.")
                    ]
            });
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or
                System.ComponentModel.Win32Exception or
                    IOException or
                        UnauthorizedAccessException or
                            NotSupportedException)
        {
            return Task.FromResult(Failure(
                "MRT4821",
                $"The external terminal could not be started: {ex.Message}"));
        }
    }

    private static ProcessStartInfo CreateTerminalStartInfo(
        ProcessExecutionRequest request)
    {
        var windowsTerminalPath = FindOnPath(WindowsTerminalExecutable);

        return windowsTerminalPath is not null
                   ? CreateWindowsTerminalStartInfo(windowsTerminalPath, request)
                   : CreateCommandPromptStartInfo(request);
    }

    private static ProcessStartInfo CreateWindowsTerminalStartInfo(
        string windowsTerminalPath,
        ProcessExecutionRequest request)
    {
        var startInfo = CreateDetachedStartInfo(windowsTerminalPath, request);

        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add(request.WorkingDirectory);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(request.FileName);

        foreach (var argument in request.Arguments)
            startInfo.ArgumentList.Add(argument);

        return startInfo;
    }

    private static ProcessStartInfo CreateCommandPromptStartInfo(
        ProcessExecutionRequest request)
    {
        var startInfo = CreateDetachedStartInfo(
            Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            request);

        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/k");
        startInfo.ArgumentList.Add(BuildCommandPromptCommand(request));

        return startInfo;
    }

    private static ProcessStartInfo CreateDetachedStartInfo(
        string fileName,
        ProcessExecutionRequest request)
    {
        var startInfo = new ProcessStartInfo(fileName) {
            UseShellExecute = false,
            WorkingDirectory = request.WorkingDirectory,
            CreateNoWindow = false
        };

        foreach (var environmentVariable in request.EnvironmentVariables)
        {
            if (environmentVariable.Value is null)
                startInfo.Environment.Remove(environmentVariable.Key);
            else
                startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
        }

        return startInfo;
    }

    private static string BuildCommandPromptCommand(
        ProcessExecutionRequest request)
    {
        var parts = new List<string> { QuoteForCommandPrompt(request.FileName) };
        parts.AddRange(request.Arguments.Select(QuoteForCommandPrompt));
        return string.Join(" ", parts);
    }

    private static string QuoteForCommandPrompt(string argument)
    {
        return "\"" + argument.Replace("\"", "\"\"") + "\"";
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("Path");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var directory in path.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static ExecutionResult Failure(string code, string message)
    {
        return new ExecutionResult {
            Status = ExecutionStatus.Failed,
            Diagnostics = [Diagnostic(code, DiagnosticSeverity.Error, message)]
        };
    }

    private static Diagnostic Diagnostic(
        string code,
        DiagnosticSeverity severity,
        string message)
    {
        return new Diagnostic(
            code,
            severity,
            message,
            new TextLocation(SourceText.From(string.Empty), new TextSpan(0, 0)));
    }
}
