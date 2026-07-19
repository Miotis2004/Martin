using System.Text;

namespace Martin.LanguageServices;

/// <summary>Renders documentation as Markdown while neutralizing source-authored HTML.</summary>
public sealed class MarkdownDocumentationRenderer
{
    public string Render(DocumentationComment documentation)
    {
        ArgumentNullException.ThrowIfNull(documentation);
        var output = new StringBuilder();
        Append(output, documentation.SummaryMarkdown);
        if (documentation.Parameters.Count > 0)
        {
            Heading(output, "Parameters");
            foreach (var (name, text) in documentation.Parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                output.Append("- `").Append(Escape(name)).Append("`: ").AppendLine(Escape(text));
        }
        if (documentation.ReturnsMarkdown is not null)
        {
            Heading(output, "Returns");
            Append(output, documentation.ReturnsMarkdown);
        }
        if (documentation.ThrowsMarkdown is not null)
        {
            Heading(output, "Throws");
            Append(output, documentation.ThrowsMarkdown);
        }
        return output.ToString().Trim();
    }

    static void Heading(StringBuilder output, string heading)
    {
        if (output.Length > 0)
            output.AppendLine().AppendLine();
        output.Append("**").Append(heading).AppendLine("**");
    }

    static void Append(StringBuilder output, string? text)
    {
        if (text is not null)
            output.Append(Escape(text));
    }

    // Monaco accepts Markdown; escaping HTML delimiters prevents comments from injecting arbitrary tags.
    public static string Escape(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal)
                                                     .Replace("<", "&lt;", StringComparison.Ordinal)
                                                     .Replace(">", "&gt;", StringComparison.Ordinal);
}
