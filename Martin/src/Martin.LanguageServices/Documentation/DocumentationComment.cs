using System.Collections.Immutable;

namespace Martin.LanguageServices;

/// <summary>Structured, source-authored documentation associated with a declaration.</summary>
public sealed record DocumentationComment
{
    public string ? SummaryMarkdown { get; init; }
    public ImmutableDictionary<string, string> Parameters { get; init; } =
        ImmutableDictionary<string, string>.Empty;
    public string ? ReturnsMarkdown { get; init; }
    public string ? ThrowsMarkdown { get; init; }
}
