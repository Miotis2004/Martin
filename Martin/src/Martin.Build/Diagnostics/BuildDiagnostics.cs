using Martin.Compiler.Diagnostics;
using Martin.Compiler.Text;

namespace Martin.Build.Diagnostics;

public static class BuildDiagnostics
{
    private static readonly TextLocation EmptyLocation = new TextLocation(SourceText.From(""), new TextSpan(0, 0));

    public static Diagnostic BuildCancelled() => new Diagnostic(
        "MRT3010",
        DiagnosticSeverity.Error,
        "Build was cancelled.",
        EmptyLocation);
}
