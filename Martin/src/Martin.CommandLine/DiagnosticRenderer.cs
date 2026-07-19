namespace Martin.CommandLine;

public interface DiagnosticRenderer
{
    void Render(IEnumerable<CommandDiagnostic> diagnostics, TextWriter writer);
}
