namespace Martin.ProjectSystem;

public enum ProjectDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ProjectDiagnostic(
    string Code,
    ProjectDiagnosticSeverity Severity,
    string Message,
    string? Path = null,
    int? Line = null,
    int? Column = null);

internal static class ProjectDiagnostics
{
    public static ProjectDiagnostic Error(string code, string message, string? path = null, int? line = null, int? column = null) =>
        new(code, ProjectDiagnosticSeverity.Error, message, path, line, column);

    public static ProjectDiagnostic Warning(string code, string message, string? path = null, int? line = null, int? column = null) =>
        new(code, ProjectDiagnosticSeverity.Warning, message, path, line, column);
}
