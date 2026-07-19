using System.Collections.Immutable;

namespace Martin.LanguageServices;

public sealed record FormattingOptions
{
    public int IndentSize { get; init; } = 4;
    public bool UseTabs { get; init; }
    public bool InsertFinalNewLine { get; init; } = true;
    public bool PreserveLineEndings { get; init; } = true;
}

public sealed record FormattingRequest : LanguageRequest
{
    public FormattingOptions Options { get; init; } = new();
}

/// <summary>The safe formatter's result. Refused results never contain edits.</summary>
public sealed record FormattingResult
{
    public ImmutableArray<TextEdit> Edits { get; init; } = [];
    public bool WasRefused { get; init; }
    public string ? DiagnosticCode { get; init; }
    public string ? Message { get; init; }
}
