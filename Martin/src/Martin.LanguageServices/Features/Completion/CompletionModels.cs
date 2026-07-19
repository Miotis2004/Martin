using System.Collections.Immutable;

namespace Martin.LanguageServices;

public enum CompletionTriggerKind
{
    Invoked,
    TriggerCharacter,
    IncompleteCompletion
}
public enum CompletionItemKind
{
    Keyword,
    Type,
    Function,
    Method,
    Property,
    Variable,
    Parameter,
    EnumCase,
    Initializer,
    TypeParameter,
    Snippet
}
public enum InsertTextFormat
{
    PlainText,
    Snippet
}

public sealed record CompletionRequest : PositionLanguageRequest
{
    public CompletionTriggerKind TriggerKind { get; init; }
    public char ? TriggerCharacter { get; init; }
    public int MaximumResults { get; init; } = 200;
}

/// <summary>A semantic completion candidate and the exact edit that accepts it.</summary>
public sealed record CompletionItem
{
    public required string Label { get; init; }
    public required CompletionItemKind Kind { get; init; }
    public required TextEdit TextEdit { get; init; }
    public string ? Detail { get; init; }
    public string ? DocumentationMarkdown { get; init; }
    public string ? FilterText { get; init; }
    public string ? SortText { get; init; }
    public string ? InsertText { get; init; }
    public InsertTextFormat InsertTextFormat { get; init; }
    public ImmutableArray<char> CommitCharacters { get; init; } = [];
    public SymbolId ? SymbolId { get; init; }
    public bool IsRecommended { get; init; }

    // Phase 6 compatibility aliases.
    public string DisplayText => Label;
    public CompletionItemKind ItemKind => Kind;
    public string KindText => Kind.ToString().ToLowerInvariant();

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public CompletionItem(string displayText, string insertText, string kind, string? detail = null)
    {
        Label = displayText;
        InsertText = insertText;
        Kind = Enum.TryParse<CompletionItemKind>(kind, true, out var parsed) ? parsed : CompletionItemKind.Variable;
        TextEdit = new(new(0, 0), insertText);
        Detail = detail;
    }
}
