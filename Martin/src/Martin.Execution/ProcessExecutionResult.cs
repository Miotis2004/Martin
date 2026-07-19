namespace Martin.Execution;

public sealed record ProcessExecutionResult
{
    public int ? ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
}
