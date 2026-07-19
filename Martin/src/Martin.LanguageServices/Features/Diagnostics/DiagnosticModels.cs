using System.Collections.Immutable;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Text;

namespace Martin.LanguageServices;

public enum LanguageDiagnosticSource
{
    LiveSyntax,
    LiveSemantic,
    Build
}

public sealed record LanguageDiagnostic(string Code, DiagnosticSeverity Severity, string Message, string FilePath, TextSpan Span, LinePositionSpan LineSpan, string Source)
{
    /// <summary>Identifies the lifetime and replacement bucket for a diagnostic.</summary>
    public LanguageDiagnosticSource DiagnosticSource { get; init; } = LanguageDiagnosticSource.LiveSemantic;
}

public sealed record DocumentDiagnosticSet
{
    public required DocumentId DocumentId { get; init; }
    public required DocumentVersion DocumentVersion { get; init; }
    public ImmutableArray<LanguageDiagnostic> Diagnostics { get; init; } = [];
}

/// <summary>An immutable, exact-version replacement set for one diagnostic source.</summary>
public sealed record DiagnosticSnapshot
{
    public required ProjectId ProjectId { get; init; }
    public required ProjectVersion ProjectVersion { get; init; }
    public required LanguageDiagnosticSource Source { get; init; }
    public ImmutableDictionary<DocumentId, DocumentDiagnosticSet> Documents { get; init; }
        = ImmutableDictionary<DocumentId, DocumentDiagnosticSet>.Empty;
}

public sealed class DiagnosticSnapshotEventArgs(DiagnosticSnapshot snapshot) : EventArgs
{
    public DiagnosticSnapshot Snapshot { get; } = snapshot;
}
