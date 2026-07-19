namespace Martin.Build;

public sealed record DotNetBuildResult
{
    public bool Started { get; init; }
    public bool Completed { get; init; }
    public bool WasCancelled { get; init; }
    public int? ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
}
