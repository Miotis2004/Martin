using System.Collections.Immutable;

namespace Martin.CommandLine;

public enum CommandDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record CommandTextLocation
{
    public string ? FilePath { get; init; }

    public int StartLine { get; init; }

    public int StartColumn { get; init; }

    public int EndLine { get; init; }

    public int EndColumn { get; init; }
}

public sealed record CommandRelatedLocation
{
    public required CommandTextLocation Location { get; init; }

    public string ? Message { get; init; }
}

public sealed record CommandDiagnostic
{
    public required string Code { get; init; }

    public required CommandDiagnosticSeverity Severity { get; init; }

    public required string Message { get; init; }

    public CommandTextLocation ? Location { get; init; }

    public string ? Path { get; init; }

    public ImmutableArray<CommandRelatedLocation> RelatedLocations { get; init; } = [];
}
