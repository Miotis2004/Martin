using System.Collections.Immutable;
using Martin.Compiler.Diagnostics;

namespace Martin.Build;

public sealed record BuildResult
{
    public BuildStatus Status { get; init; } = BuildStatus.Failed;
    public bool Success
    {
        get => Status == BuildStatus.Succeeded;
        init => Status = value ? BuildStatus.Succeeded : BuildStatus.Failed;
    }
    public bool WasCancelled => Status == BuildStatus.Cancelled;
    public ImmutableArray<Diagnostic> Diagnostics { get; init; } = [];
    public string ? OutputDirectory { get; init; }
    public string ? EntryPointPath { get; init; }
    public string ? GeneratedSourcePath { get; init; }
    public string ? GeneratedProjectPath { get; init; }
    public int    ? ProcessExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public ImmutableArray<BuildArtifact> Artifacts { get; init; } = [];
}
