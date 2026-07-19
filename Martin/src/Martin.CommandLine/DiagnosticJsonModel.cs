namespace Martin.CommandLine;

public sealed record DiagnosticJsonEnvelope(IReadOnlyList<DiagnosticJsonModel> Diagnostics);

public sealed record DiagnosticJsonModel(
    string Code,
    string Severity,
    string Message,
    DiagnosticLocationJsonModel? Location,
    string? Path,
    IReadOnlyList<DiagnosticRelatedLocationJsonModel> RelatedLocations)
{
    public static DiagnosticJsonModel FromDiagnostic(CommandDiagnostic diagnostic) =>
        new(
            AnsiEscapeStripper.Strip(diagnostic.Code),
            diagnostic.Severity.ToString().ToLowerInvariant(),
            AnsiEscapeStripper.Strip(diagnostic.Message),
            diagnostic.Location is null ? null : DiagnosticLocationJsonModel.FromLocation(diagnostic.Location),
            diagnostic.Path is null ? null : AnsiEscapeStripper.Strip(diagnostic.Path),
            diagnostic.RelatedLocations.Select(DiagnosticRelatedLocationJsonModel.FromRelatedLocation).ToArray());
}

public sealed record DiagnosticLocationJsonModel(
    string? FilePath,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn)
{
    public static DiagnosticLocationJsonModel FromLocation(CommandTextLocation location) =>
        new(location.FilePath is null ? null : AnsiEscapeStripper.Strip(location.FilePath), location.StartLine, location.StartColumn, location.EndLine, location.EndColumn);
}

public sealed record DiagnosticRelatedLocationJsonModel(DiagnosticLocationJsonModel Location, string? Message)
{
    public static DiagnosticRelatedLocationJsonModel FromRelatedLocation(CommandRelatedLocation relatedLocation) =>
        new(DiagnosticLocationJsonModel.FromLocation(relatedLocation.Location), relatedLocation.Message is null ? null : AnsiEscapeStripper.Strip(relatedLocation.Message));
}

static class AnsiEscapeStripper
{
    public static string Strip(string value)
    {
        var start = value.IndexOf("\u001b[", StringComparison.Ordinal);
        if (start < 0)
            return value;

        var builder = new System.Text.StringBuilder(value.Length);
        builder.Append(value, 0, start);
        for (var i = start; i < value.Length; i++)
        {
            if (value[i] == '\u001b' && i + 1 < value.Length && value[i + 1] == '[')
            {
                i += 2;
                while (i < value.Length && (value[i] < '@' || value[i] > '~'))
                    i++;
                continue;
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }
}
