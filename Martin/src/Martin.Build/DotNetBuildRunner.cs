using System.Diagnostics;

namespace Martin.Build;

public sealed class DotNetBuildRunner : IDotNetBuildRunner
{
    readonly string executablePath;

    public DotNetBuildRunner(string executablePath = "dotnet")
    {
        this.executablePath = executablePath;
    }

    public async Task<DotNetBuildResult> RunAsync(DotNetBuildRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process
        {
            StartInfo = CreateStartInfo(request, executablePath)
        };

        try
        {
            if (!process.Start())
                return new DotNetBuildResult { Started = false };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new DotNetBuildResult { Started = false };
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return new DotNetBuildResult
            {
                Started = true,
                Completed = true,
                ExitCode = process.ExitCode,
                StandardOutput = await standardOutput,
                StandardError = await standardError
            };
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            await WaitForExitAfterCancellationAsync(process);
            return new DotNetBuildResult
            {
                Started = true,
                WasCancelled = true,
                StandardOutput = await CompleteOutputReadAsync(standardOutput),
                StandardError = await CompleteOutputReadAsync(standardError)
            };
        }
    }

    static ProcessStartInfo CreateStartInfo(DotNetBuildRequest request, string executablePath)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(request.ProjectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(request.Configuration.ToString());
        startInfo.ArgumentList.Add("--nologo");

        foreach (var argument in request.AdditionalArguments)
            startInfo.ArgumentList.Add(argument);

        return startInfo;
    }

    static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    static async Task WaitForExitAfterCancellationAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
        }
    }

    static async Task<string> CompleteOutputReadAsync(Task<string> output)
    {
        try
        {
            return await output.WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch
        {
            return string.Empty;
        }
    }
}
