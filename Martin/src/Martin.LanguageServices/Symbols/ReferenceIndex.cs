using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Symbols;

namespace Martin.LanguageServices;

/// <summary>An immutable, project-version-specific index of bound source occurrences.</summary>
public sealed class ReferenceIndex
{
    internal ReferenceIndex(ProjectId projectId, ProjectVersion version,
        ImmutableDictionary<SymbolId, ImmutableArray<ReferenceLocation>> bySymbol,
        ImmutableDictionary<DocumentId, ImmutableArray<ReferenceLocation>> byDocument,
        bool isComplete)
    {
        ProjectId = projectId;
        ProjectVersion = version;
        BySymbol = bySymbol;
        ByDocument = byDocument;
        IsComplete = isComplete;
    }

    public ProjectId ProjectId { get; }
    public ProjectVersion ProjectVersion { get; }
    public ImmutableDictionary<SymbolId, ImmutableArray<ReferenceLocation>> BySymbol { get; }
    public ImmutableDictionary<DocumentId, ImmutableArray<ReferenceLocation>> ByDocument { get; }
    /// <summary>False when one or more requested semantic models were unavailable.</summary>
    public bool IsComplete { get; }

    public ImmutableArray<ReferenceLocation> Find(SymbolId symbolId, bool includeDeclaration = true) =>
        BySymbol.TryGetValue(symbolId, out var locations)
            ? includeDeclaration ? locations : locations.Where(location => !location.IsDeclaration).ToImmutableArray()
            : [];
}

/// <summary>Builds a reference index from compiler binding data, never from source-text matching.</summary>
public sealed class ReferenceIndexBuilder
{
    public ReferenceIndex Build(LanguageProjectSnapshot project, ProjectAnalysis analysis,
        CancellationToken cancellationToken = default) => Build(project, analysis, null, cancellationToken);

    /// <param name="documents">Optional subset for a partial/background index.</param>
    public ReferenceIndex Build(LanguageProjectSnapshot project, ProjectAnalysis analysis,
        IEnumerable<DocumentId>? documents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(analysis);
        if (project.Id != analysis.ProjectId || project.Version != analysis.Version)
            throw new ArgumentException("The analysis must describe the exact project snapshot.", nameof(analysis));

        var requested = documents?.ToImmutableHashSet() ?? project.Documents.Select(d => d.Id).ToImmutableHashSet();
        var documentMap = project.Documents.ToDictionary(d => d.Id);
        var pathMap = project.Documents.ToDictionary(d => Canonical(d.FilePath), d => d.Id, PathComparer);
        var declarations = analysis.SemanticModels.Values.SelectMany(model => model.GetDeclaredSymbols()
            .Select(symbol => (Model: model, Symbol: symbol))).DistinctBy(x => x.Symbol, ReferenceEqualityComparer.Instance).ToArray();
        var owners = BuildOwners(declarations);
        var factory = new SymbolIdentityFactory(project.Id);
        var ids = new Dictionary<Symbol, SymbolId>(ReferenceEqualityComparer.Instance);
        SymbolId Id(Symbol symbol)
        {
            symbol = OriginalDefinition(symbol);
            if (ids.TryGetValue(symbol, out var existing)) return existing;
            var owner = owners.TryGetValue(symbol, out var containing) ? Id(containing) : (SymbolId?)null;
            var discriminator = symbol is LocalVariableSymbol && symbol.DeclarationLocation is { } location
                ? $"{(pathMap.TryGetValue(Canonical(location.FilePath), out var documentId) ? documentId.Value.ToString("N") : string.Empty)}:{location.Span.Start}:{location.Span.Length}" : null;
            return ids[symbol] = factory.Create(symbol, owner, discriminator);
        }

        var results = new List<ReferenceLocation>();
        foreach (var documentId in requested.OrderBy(id => id.Value))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!documentMap.TryGetValue(documentId, out var document) ||
                !analysis.SemanticModels.TryGetValue(documentId, out var model)) continue;
            foreach (var reference in model.GetSymbolReferences())
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(new(documentId, document.FilePath, reference.Location.Span)
                {
                    SymbolId = Id(reference.Symbol),
                    Kind = Map(reference.Kind)
                });
            }
        }

        var ordered = results.Distinct().OrderBy(r => r.FilePath, PathComparer)
            .ThenBy(r => r.Span.Start).ThenBy(r => r.Span.Length).ThenBy(r => r.Kind).ToArray();
        var bySymbol = ordered.GroupBy(r => r.SymbolId).ToImmutableDictionary(g => g.Key, g => g.ToImmutableArray());
        var byDocument = ordered.GroupBy(r => r.DocumentId).ToImmutableDictionary(g => g.Key, g => g.ToImmutableArray());
        var complete = requested.SetEquals(project.Documents.Select(d => d.Id)) && requested.All(analysis.SemanticModels.ContainsKey);
        return new(project.Id, project.Version, bySymbol, byDocument, complete);
    }

    static Symbol OriginalDefinition(Symbol symbol) => symbol switch
    {
        ConstructedTypeSymbol type => type.GenericDefinition,
        FunctionSymbol function => function.OriginalDefinition,
        MethodSymbol method => method.OriginalDefinition,
        PropertySymbol property => property.OriginalDefinition,
        InitializerSymbol initializer => initializer.OriginalDefinition,
        EnumCaseSymbol enumCase => enumCase.OriginalDefinition,
        _ => symbol
    };

    static Dictionary<Symbol, Symbol> BuildOwners(IEnumerable<(SemanticModel Model, Symbol Symbol)> declarations)
    {
        var map = new Dictionary<Symbol, Symbol>(ReferenceEqualityComparer.Instance);
        foreach (var (model, symbol) in declarations)
        {
            if (symbol is MemberSymbol member && member.ContainingType is Symbol type) map[symbol] = type;
            if (symbol is TypeParameterSymbol typeParameter) map[symbol] = typeParameter.ContainingSymbol;
            IEnumerable<ParameterSymbol> parameters = symbol switch
            {
                FunctionSymbol f => f.Parameters,
                MethodSymbol m => m.Parameters,
                InitializerSymbol i => i.Parameters,
                EnumCaseSymbol c => c.AssociatedValues,
                _ => []
            };
            foreach (var parameter in parameters) map[parameter] = symbol;
            if (symbol is FunctionSymbol function)
                foreach (var parameter in function.TypeParameters) map[parameter] = symbol;
            if (symbol is LocalVariableSymbol local && local.DeclarationLocation is { } location &&
                model.GetEnclosingSymbol(location.Span.Start) is { } enclosing) map[local] = enclosing;
        }
        return map;
    }

    static ReferenceKind Map(SymbolReferenceKind kind) => kind switch
    {
        SymbolReferenceKind.Declaration => ReferenceKind.Declaration,
        SymbolReferenceKind.Read => ReferenceKind.Read,
        SymbolReferenceKind.Write => ReferenceKind.Write,
        SymbolReferenceKind.Call => ReferenceKind.Call,
        SymbolReferenceKind.Type => ReferenceKind.Type,
        SymbolReferenceKind.MemberAccess => ReferenceKind.MemberAccess,
        SymbolReferenceKind.Initializer => ReferenceKind.Initializer,
        SymbolReferenceKind.Conformance => ReferenceKind.ProtocolConformance,
        SymbolReferenceKind.EnumCasePattern => ReferenceKind.EnumCasePattern,
        SymbolReferenceKind.PatternBinding => ReferenceKind.PatternBinding,
        SymbolReferenceKind.ProtocolRequirement => ReferenceKind.ProtocolRequirement,
        SymbolReferenceKind.ProtocolWitness => ReferenceKind.ProtocolWitness,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    static string Canonical(string? path) => path is null ? string.Empty : Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
