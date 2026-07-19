using System.Collections.Immutable;
using Martin.Compiler.Text;

namespace Martin.LanguageServices;

public sealed record HoverInfo(TextSpan Span, string Markdown);
public sealed record HoverRequest : PositionLanguageRequest;
public sealed record DefinitionRequest : PositionLanguageRequest;
public sealed record ReferencesRequest : PositionLanguageRequest
{
    public bool IncludeDeclaration { get; init; } = true;
}
public sealed record DefinitionLocation(DocumentId DocumentId, string FilePath, TextSpan Span);
public enum ReferenceKind
{
    Declaration,
    Read,
    Write,
    Call,
    Type,
    MemberAccess,
    Initializer,
    ProtocolConformance,
    EnumCasePattern,
    PatternBinding,
    ProtocolRequirement,
    ProtocolWitness
}

public sealed record ReferenceLocation(DocumentId DocumentId, string FilePath, TextSpan Span)
{
    public SymbolId SymbolId { get; init; }
    public ReferenceKind Kind { get; init; } = ReferenceKind.Read;
    public bool IsDeclaration => Kind == ReferenceKind.Declaration;
}
public sealed record SignatureHelp
{
    public ImmutableArray<SignatureInformation> Signatures { get; init; } = [];
    public int ActiveSignature { get; init; }
    public int ActiveParameter { get; init; }

    // Kept for clients of the original Phase 6 contract.
    public string Name => Signatures.IsDefaultOrEmpty ? string.Empty : Signatures[ActiveSignature].Name;
    public ImmutableArray<string> Parameters => Signatures.IsDefaultOrEmpty ? [] :
        Signatures[ActiveSignature].Parameters.Select(p => p.Label).ToImmutableArray();
    public string ReturnType => Signatures.IsDefaultOrEmpty ? string.Empty : Signatures[ActiveSignature].ReturnType;
}

public sealed record SignatureInformation
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public string ReturnType { get; init; } = "Void";
    public string? DocumentationMarkdown { get; init; }
    public ImmutableArray<ParameterInformation> Parameters { get; init; } = [];
    public SymbolId? SymbolId { get; init; }
}

public sealed record ParameterInformation(string Label, string Type, string? DocumentationMarkdown = null);

public sealed record SignatureHelpRequest : PositionLanguageRequest
{
    public char? TriggerCharacter { get; init; }
    public bool IsRetrigger { get; init; }
}
