namespace Martin.CommandLine;

public static class DiagnosticOrdering
{
    public static IEnumerable<CommandDiagnostic> Sort(IEnumerable<CommandDiagnostic> diagnostics) =>
        diagnostics
            .OrderBy(diagnostic => SeverityRank(diagnostic.Severity))
            .ThenBy(diagnostic => NormalizePath(diagnostic.Location?.FilePath ?? diagnostic.Path), StringComparer.OrdinalIgnoreCase)
            .ThenBy(diagnostic => diagnostic.Location?.StartLine ?? int.MaxValue)
            .ThenBy(diagnostic => diagnostic.Location?.StartColumn ?? int.MaxValue)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal);

    static int SeverityRank(CommandDiagnosticSeverity severity) => severity switch
    {
        CommandDiagnosticSeverity.Error => 0,
        CommandDiagnosticSeverity.Warning => 1,
        _ => 2
    };

    static string NormalizePath(string? path) => path is null ? string.Empty : path.Replace('\\', '/');
}
