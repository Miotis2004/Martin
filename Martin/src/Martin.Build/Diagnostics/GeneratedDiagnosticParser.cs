using System.Text.RegularExpressions;
using Martin.Compiler.Diagnostics;

namespace Martin.Build.Diagnostics;

public sealed record GeneratedDiagnostic(
    string FilePath,
    int Line,
    int Column,
    DiagnosticSeverity Severity,
    string Code,
    string Message,
    string OriginalText);

public sealed class GeneratedDiagnosticParser
{
    static readonly Regex DiagnosticPattern = new(
        @"^(?<path>.+)\((?<line>\d+),(?<column>\d+)\):\s*(?<severity>warning|error)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<message>.*?)(?:\s*\[.*\])?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<GeneratedDiagnostic> Parse(string text)
    {
        var diagnostics = new List<GeneratedDiagnostic>();
        foreach (var line in SplitLines(text))
        {
            var match = DiagnosticPattern.Match(line);
            if (!match.Success)
                continue;

            diagnostics.Add(new GeneratedDiagnostic(
                match.Groups["path"].Value,
                int.Parse(match.Groups["line"].Value),
                int.Parse(match.Groups["column"].Value),
                match.Groups["severity"].Value == "warning" ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error,
                match.Groups["code"].Value,
                match.Groups["message"].Value.Trim(),
                line));
        }

        return diagnostics;
    }

    static IEnumerable<string> SplitLines(string text)
    {
        using var reader = new StringReader(text ?? string.Empty);
        string? line;
        while ((line = reader.ReadLine()) is not null)
            yield return line;
    }
}
