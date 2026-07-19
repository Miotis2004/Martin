using System.Collections.Immutable;
using Martin.CodeGeneration;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Text;

namespace Martin.Build.Diagnostics;

public sealed class GeneratedDiagnosticTranslator
{
    public ImmutableArray<Diagnostic> Translate(
        IEnumerable<GeneratedDiagnostic> diagnostics,
        string generatedSource,
        ImmutableArray<GeneratedSourceMapEntry> sourceMap)
    {
        var sourceText = SourceText.From(generatedSource);
        var translated = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var diagnostic in diagnostics)
        {
            var offset = TryGetOffset(sourceText, diagnostic.Line, diagnostic.Column);
            var entry = offset is int position ? FindBestEntry(sourceMap, position) : null;
            if (entry is { } mapped)
            {
                translated.Add(new Diagnostic(
                    diagnostic.Code,
                    diagnostic.Severity,
                    $"Generated C# {diagnostic.Code}: {diagnostic.Message}",
                    mapped.MartinLocation));
            }
            else
            {
                var location = offset is int positionInGenerated
                    ? new TextLocation(sourceText, new TextSpan(Math.Clamp(positionInGenerated, 0, sourceText.Length), 0))
                    : EmptyLocation();
                translated.Add(new Diagnostic(
                    "MRT3020",
                    diagnostic.Severity,
                    $"Generated C# {diagnostic.Code} at {Path.GetFileName(diagnostic.FilePath)}({diagnostic.Line},{diagnostic.Column}) could not be mapped to a Martin source location: {diagnostic.Message}",
                    location));
            }
        }

        return translated.ToImmutable();
    }

    static int? TryGetOffset(SourceText text, int line, int column)
    {
        if (line < 1 || line > text.Lines.Count || column < 1)
            return null;

        var textLine = text.Lines[line - 1];
        return Math.Clamp(textLine.Start + column - 1, textLine.Start, textLine.End);
    }

    static GeneratedSourceMapEntry? FindBestEntry(ImmutableArray<GeneratedSourceMapEntry> sourceMap, int position)
    {
        return sourceMap
            .Where(entry => entry.GeneratedSpan.Start <= position && position <= entry.GeneratedSpan.End)
            .OrderBy(entry => entry.GeneratedSpan.Length)
            .ThenBy(entry => entry.GeneratedSpan.Start)
            .FirstOrDefault();
    }

    static TextLocation EmptyLocation() => new(SourceText.From(string.Empty), new TextSpan(0, 0));
}
