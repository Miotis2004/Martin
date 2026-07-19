using System.Collections.Immutable;
using Martin.Compiler.Diagnostics;

namespace Martin.Execution;

public sealed record ExecutionResult
{
    public ExecutionStatus Status { get; init; } = ExecutionStatus.NotStarted;
    public bool Started
    {
        get => Status is ExecutionStatus.Running or ExecutionStatus.Completed or ExecutionStatus.Failed or ExecutionStatus.Cancelled;
        init => Status = value ? ExecutionStatus.Running : ExecutionStatus.NotStarted;
    }
    public bool Completed
    {
        get => Status == ExecutionStatus.Completed;
        init => Status = value ? ExecutionStatus.Completed : Status;
    }
    public bool WasCancelled => Status == ExecutionStatus.Cancelled;
    public int? ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public ImmutableArray<Diagnostic> Diagnostics { get; init; } = [];
}
