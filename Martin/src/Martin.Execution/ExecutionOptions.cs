namespace Martin.Execution;

public sealed record ExecutionOptions
{
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public string? WorkingDirectory { get; init; }
    public string? StandardInput { get; init; }
    public bool UseExternalTerminal { get; init; }
    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } = new Dictionary<string, string?>();
}
