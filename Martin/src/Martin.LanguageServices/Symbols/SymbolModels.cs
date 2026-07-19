using System.Collections.Immutable;
using Martin.Compiler.Symbols;

namespace Martin.LanguageServices;

/// <summary>A language-service description of a compiler symbol.</summary>
public sealed record SymbolDescriptor
{
    public required SymbolId Id { get; init; }
    public required SymbolKind Kind { get; init; }
    public required string Name { get; init; }
    public SymbolId ? ContainingSymbolId { get; init; }
    public required string DisplaySignature { get; init; }
    public required DefinitionLocation Definition { get; init; }
    public ImmutableArray<DefinitionLocation> AdditionalDeclarations { get; init; } = [];
    public bool IsBuiltIn { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsStatic { get; init; }
    public string ? DocumentationMarkdown { get; init; }
}

/// <summary>An immutable declaration lookup for one exact project version.</summary>
public sealed class DeclarationIndex
{
    internal DeclarationIndex(
        ProjectId projectId,
        ProjectVersion projectVersion,
        ImmutableDictionary<SymbolId, SymbolDescriptor> byId,
        ImmutableDictionary<string, ImmutableArray<SymbolId>> byName,
        ImmutableDictionary<DocumentId, ImmutableArray<SymbolId>> byDocument)
    {
        ProjectId = projectId;
        ProjectVersion = projectVersion;
        ById = byId;
        ByName = byName;
        ByDocument = byDocument;
    }

    public ProjectId ProjectId { get; }
    public ProjectVersion ProjectVersion { get; }
    public ImmutableDictionary<SymbolId, SymbolDescriptor> ById { get; }
    public ImmutableDictionary<string, ImmutableArray<SymbolId>> ByName { get; }
    public ImmutableDictionary<DocumentId, ImmutableArray<SymbolId>> ByDocument { get; }
}
