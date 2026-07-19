using Martin.Compiler.Diagnostics;
using Martin.ProjectSystem;

namespace Martin.CommandLine;

public static class DiagnosticAdapters
{
    public static CommandDiagnostic FromCompiler(Diagnostic diagnostic)
    {
        CommandTextLocation? location = null;
        if (diagnostic.Location.Text is not null)
        {
            var span = diagnostic.Location.LineSpan;
            location = new CommandTextLocation
            {
                FilePath = diagnostic.Location.FilePath,
                StartLine = span.Start.Line + 1,
                StartColumn = span.Start.Character + 1,
                EndLine = span.End.Line + 1,
                EndColumn = span.End.Character + 1
            };
        }

        return new CommandDiagnostic
        {
            Code = diagnostic.Code,
            Severity = diagnostic.Severity switch { DiagnosticSeverity.Info => CommandDiagnosticSeverity.Info, DiagnosticSeverity.Warning => CommandDiagnosticSeverity.Warning, _ => CommandDiagnosticSeverity.Error },
            Message = diagnostic.Message,
            Location = location
        };
    }

    public static CommandDiagnostic FromProject(ProjectDiagnostic diagnostic) => new()
    {
        Code = diagnostic.Code,
        Severity = diagnostic.Severity switch { ProjectDiagnosticSeverity.Info => CommandDiagnosticSeverity.Info, ProjectDiagnosticSeverity.Warning => CommandDiagnosticSeverity.Warning, _ => CommandDiagnosticSeverity.Error },
        Message = diagnostic.Message,
        Path = diagnostic.Path,
        Location = diagnostic.Path is { Length: > 0 } && diagnostic.Line is { } line
            ? new CommandTextLocation
            {
                FilePath = diagnostic.Path,
                StartLine = line,
                StartColumn = Math.Max(diagnostic.Column ?? 1, 1),
                EndLine = line,
                EndColumn = Math.Max((diagnostic.Column ?? 1) + 1, 2)
            }
            : null
    };

    public static CommandDiagnostic FromParserError(string code, string message) => new()
    {
        Code = code,
        Severity = CommandDiagnosticSeverity.Error,
        Message = message
    };
}
