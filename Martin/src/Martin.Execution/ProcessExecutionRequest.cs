namespace Martin.Execution;

public sealed record ProcessExecutionRequest
{
    public required string FileName { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public required string WorkingDirectory { get; init; }
    public string ? StandardInput { get; init; }
    public bool RedirectStandardOutput { get; init; } = true;
    public bool RedirectStandardError { get; init; } = true;
    public IReadOnlyDictionary < string, string ? > EnvironmentVariables { get; init; } = new Dictionary < string, string ? > ();
}
