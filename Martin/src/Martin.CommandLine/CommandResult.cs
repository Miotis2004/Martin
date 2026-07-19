using System.Collections.Immutable;

namespace Martin.CommandLine;

public sealed record CommandResult
{
    public MartinExitCode ExitCode { get; init; } = MartinExitCode.Success;

    public ImmutableArray<CommandDiagnostic> Diagnostics { get; init; } = [];

    public bool Success => ExitCode == MartinExitCode.Success && !Diagnostics.Any(d => d.Severity == CommandDiagnosticSeverity.Error);
}
