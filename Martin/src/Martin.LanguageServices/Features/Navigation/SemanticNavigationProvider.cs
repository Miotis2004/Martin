using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;

namespace Martin.LanguageServices;

/// <summary>Project-wide definition and reference queries backed by bound compiler occurrences.</summary>
internal static class SemanticNavigationProvider
{
    internal sealed record Context(DeclarationIndex Declarations, ReferenceIndex References,
                                   ImmutableDictionary<SymbolId, ImmutableArray<SymbolId>> ProtocolRelationships,
                                   ImmutableDictionary<DocumentId, SemanticModel> SemanticModels);

    public static Context Build(ProjectAnalysis analysis, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var related = new Dictionary<SymbolId, HashSet<SymbolId>>();
        SymbolId ? Find(Symbol symbol)
        {
            var location = symbol.DeclarationLocation;
            if (location is null || !analysis.Declarations.ByName.TryGetValue(symbol.Name, out var ids))
                return null;
            foreach (var id in ids)
                if (analysis.Declarations.ById.TryGetValue(id, out var descriptor) && descriptor.Definition.Span == location.Value.Span)
                    return id;
            return null;
        }
        void Relate(Symbol left, Symbol right)
        {
            var leftId = Find(left);
            var rightId = Find(right);
            if (leftId is null || rightId is null)
                return;
            if (!related.TryGetValue(leftId.Value, out var leftSet))
                related[leftId.Value] = leftSet = [];
            if (!related.TryGetValue(rightId.Value, out var rightSet))
                related[rightId.Value] = rightSet = [];
            leftSet.Add(rightId.Value);
            rightSet.Add(leftId.Value);
        }
        foreach (var conformance in analysis.Phase13.Conformances)
            foreach (var (requirement, witness) in conformance.Witnesses)
                Relate(requirement, witness);
        return new(analysis.Declarations, analysis.References,
                   related.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.OrderBy(id => id.Value, StringComparer.Ordinal).ToImmutableArray()),
                   analysis.SemanticModels);
    }

    public static SymbolId? Resolve(Context context, DocumentId documentId, int position)
    {
        SymbolId? indexed = null;
        if (context.References.ByDocument.TryGetValue(documentId, out var occurrences))
        {
            // A caret at the end of an identifier is customary in editors. Prefer an
            // occurrence containing the caret, then accept its exclusive end.
            indexed = occurrences
                .Where(location => position >= location.Span.Start && position < location.Span.End)
                .OrderBy(location => location.Span.Length).Select(location => (SymbolId?)location.SymbolId).FirstOrDefault()
                ?? occurrences.Where(location => position == location.Span.End && location.Span.Length > 0)
                    .OrderBy(location => location.Span.Length).Select(location => (SymbolId?)location.SymbolId).FirstOrDefault();
        }
        return indexed ?? ResolveFromSemanticModel(context, documentId, position);
    }

    private static SymbolId? ResolveFromSemanticModel(Context context, DocumentId documentId, int position)
    {
        if (!context.SemanticModels.TryGetValue(documentId, out var model))
            return null;
        var token = model.FindToken(position) ?? (position > 0 ? model.FindToken(position - 1) : null);
        var node = token as SyntaxNode ?? model.FindNode(position);
        var symbol = node is null ? null : model.GetSymbolInfo(node);
        if (symbol is ConstructedTypeSymbol constructed)
            symbol = constructed.GenericDefinition;
        if (symbol is null || !context.Declarations.ByName.TryGetValue(symbol.Name, out var candidates))
            return null;
        var declaration = symbol.DeclarationLocation;
        if (declaration is null)
            return candidates.Length == 1 ? candidates[0] : null;
        foreach (var candidate in candidates)
            if (context.Declarations.ById.TryGetValue(candidate, out var descriptor) && descriptor.Definition.Span == declaration.Value.Span)
                return candidate;
        return null;
    }

    public static ImmutableArray<DefinitionLocation> Definitions(Context context, SymbolId? symbolId)
    {
        if (symbolId is null || !context.Declarations.ById.TryGetValue(symbolId.Value, out var descriptor))
            return [];
        if (context.ProtocolRelationships.TryGetValue(symbolId.Value, out var relationships))
            return relationships.SelectMany(id => context.Declarations.ById.TryGetValue(id, out var related)
                                                      ? new[] { related.Definition }.Concat(related.AdditionalDeclarations)
                                                      : [])
                .Distinct()
                .OrderBy(location => location.FilePath, PathComparer)
                .ThenBy(location => location.Span.Start)
                .ToImmutableArray();
        return [descriptor.Definition, ..descriptor.AdditionalDeclarations];
    }

    public static ImmutableArray<ReferenceLocation> References(Context context, SymbolId? symbolId,
                                                               DocumentId currentDocument, bool includeDeclaration) => symbolId is null ? [] : context.References.Find(symbolId.Value, includeDeclaration).OrderBy(location => location.DocumentId == currentDocument ? 0 : 1).ThenBy(location => location.FilePath, PathComparer).ThenBy(location => location.Span.Start).ThenBy(location => location.Kind).ToImmutableArray();

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
                                                      ? StringComparer.OrdinalIgnoreCase
                                                      : StringComparer.Ordinal;
}
