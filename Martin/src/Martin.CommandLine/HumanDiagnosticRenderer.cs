namespace Martin.CommandLine;

public sealed class HumanDiagnosticRenderer(bool useColor = false) : DiagnosticRenderer
{
    readonly SourceExcerptRenderer _sourceExcerptRenderer = new();

    public void Render(IEnumerable<CommandDiagnostic> diagnostics, TextWriter writer)
    {
        foreach (var diagnostic in DiagnosticOrdering.Sort(diagnostics))
        {
            var location = FormatLocation(diagnostic);
            writer.WriteLine(location.Length == 0
                ? $"{FormatSeverity(diagnostic.Severity)} {diagnostic.Code}: {diagnostic.Message}"
                : $"{location}: {FormatSeverity(diagnostic.Severity)} {diagnostic.Code}: {diagnostic.Message}");

            _sourceExcerptRenderer.Render(diagnostic, writer);

            foreach (var relatedLocation in diagnostic.RelatedLocations)
            {
                var related = FormatLocation(relatedLocation.Location);
                if (related.Length == 0)
                    continue;

                writer.WriteLine(relatedLocation.Message is { Length: > 0 } message
                    ? $"  related: {related}: {message}"
                    : $"  related: {related}");
            }
        }
    }

    static string FormatLocation(CommandDiagnostic diagnostic)
    {
        if (diagnostic.Location is { } location)
            return FormatLocation(location);

        return diagnostic.Path ?? string.Empty;
    }

    static string FormatLocation(CommandTextLocation location)
    {
        if (location.FilePath is { Length: > 0 } filePath && location.StartLine > 0)
            return $"{filePath}({location.StartLine},{Math.Max(location.StartColumn, 1)})";

        return location.FilePath ?? string.Empty;
    }

    string FormatSeverity(CommandDiagnosticSeverity severity)
    {
        var text = severity.ToString().ToLowerInvariant();
        if (!useColor) return text;
        var code = severity switch
        {
            CommandDiagnosticSeverity.Error => "31",
            CommandDiagnosticSeverity.Warning => "33",
            _ => "36"
        };
        return $"\u001b[{code}m{text}\u001b[0m";
    }
}
