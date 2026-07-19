using System.Collections.Immutable;

namespace Martin.ProjectSystem;

public sealed class MartinProject
{
    public required string RootDirectory { get; init; }
    public required string ManifestPath { get; init; }
    public required MartinManifest Manifest { get; init; }
    public ImmutableArray<string> SourceFiles { get; init; }
    public ImmutableArray<string> TestFiles { get; init; }
}

public sealed record ProjectLoadResult
{
    public MartinProject? Project { get; init; }
    public ImmutableArray<ProjectDiagnostic> Diagnostics { get; init; } = [];
    public bool Success => Project is not null && !Diagnostics.Any(d => d.Severity == ProjectDiagnosticSeverity.Error);
}

public sealed record ProjectLoadOptions
{
    public string? ProjectPath { get; init; }
    public string? ManifestPath { get; init; }
    public string? WorkingDirectory { get; init; }
}
