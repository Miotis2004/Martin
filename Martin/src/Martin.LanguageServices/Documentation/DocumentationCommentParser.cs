using System.Collections.Immutable;
using System.Text;

namespace Martin.LanguageServices;

/// <summary>Parses the small, deliberately transport-independent Martin documentation format.</summary>
public sealed class DocumentationCommentParser
{
    public DocumentationComment Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var summary = new StringBuilder();
        var parameters = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        string? returns = null, throws = null;

        foreach (var sourceLine in lines)
        {
            var line = StripMarker(sourceLine).Trim();
            if (TrySection(line, "- Parameter ", out var parameter))
            {
                var colon = parameter.IndexOf(':');
                if (colon > 0)
                    parameters[parameter[..colon].Trim()] = parameter[(colon + 1)..].Trim();
            }
            else if (TrySection(line, "- Returns:", out var returnText))
                returns = returnText.Trim();
            else if (TrySection(line, "- Throws:", out var throwsText))
                throws = throwsText.Trim();
            else
            {
                if (summary.Length > 0)
                    summary.AppendLine();
                summary.Append(line);
            }
        }

        return new DocumentationComment {
            SummaryMarkdown = EmptyToNull(summary.ToString().Trim()),
            Parameters = parameters.ToImmutable(),
            ReturnsMarkdown = EmptyToNull(returns),
            ThrowsMarkdown = EmptyToNull(throws)
        };
    }

    static string StripMarker(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("///", StringComparison.Ordinal) ? trimmed[3..].TrimStart() : trimmed;
    }

    static bool TrySection(string line, string prefix, out string value)
    {
        if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = line[prefix.Length..];
            return true;
        }
        value = string.Empty;
        return false;
    }

    static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
