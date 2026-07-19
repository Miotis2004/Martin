using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;

namespace Martin.LanguageServices;

/// <summary>Builds declaration indexes exclusively from compiler locations and semantic models.</summary>
public sealed class DeclarationIndexBuilder
{
    public DeclarationIndex Build(LanguageProjectSnapshot project, ProjectAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(analysis);
        if (project.Id != analysis.ProjectId || project.Version != analysis.Version)
            throw new ArgumentException("The analysis must describe the exact project snapshot.", nameof(analysis));

        var factory = new SymbolIdentityFactory(project.Id);
        var pathMap = project.Documents.ToDictionary(d => Canonical(d.FilePath), d => d, PathComparer);
        var declarations = analysis.SemanticModels
                               .OrderBy(pair => pair.Value.SyntaxTree.Text.FilePath, PathComparer)
                               .SelectMany(pair => pair.Value.GetDeclaredSymbols().Select(symbol => new Entry(pair.Key, pair.Value, symbol)))
                               .DistinctBy(entry => entry.Symbol, ReferenceEqualityComparer.Instance)
                               .ToArray();

        var containing = BuildContainingMap(declarations);
        var ids = new Dictionary<Symbol, SymbolId>(ReferenceEqualityComparer.Instance);
        SymbolId GetId(Symbol symbol)
        {
            if (ids.TryGetValue(symbol, out var id))
                return id;
            var owner = containing.TryGetValue(symbol, out var parent) ? GetId(parent) : (SymbolId?)null;
            var discriminator = symbol is LocalVariableSymbol
                                    ? DeclarationKey(symbol, pathMap)
                                    : null;
            return ids[symbol] = factory.Create(symbol, owner, discriminator);
        }

        var descriptors = new List<SymbolDescriptor>(declarations.Length);
        foreach (var entry in declarations)
        {
            if (!TryLocations(entry.Symbol, pathMap, out var definition, out var additional))
                continue; // Compiler-generated declarations without source are not part of the source index.
            var ownerId = containing.TryGetValue(entry.Symbol, out var parent) ? GetId(parent) : (SymbolId?)null;
            descriptors.Add(new() {
                Id = GetId(entry.Symbol),
                Kind = entry.Symbol.Kind,
                Name = entry.Symbol.Name,
                ContainingSymbolId = ownerId,
                DisplaySignature = factory.DisplaySignature(entry.Symbol),
                Definition = definition,
                AdditionalDeclarations = additional,
                IsBuiltIn = entry.Symbol is FunctionSymbol { IsBuiltIn : true },
                IsReadOnly = entry.Symbol is VariableSymbol { IsReadOnly : true } or PropertySymbol { IsReadOnly : true },
                IsStatic = entry.Symbol is MemberSymbol { IsStatic : true },
                DocumentationMarkdown = Documentation(entry.Model, definition.Span)
            });
        }

        var duplicate = descriptors.GroupBy(d => d.Id).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Duplicate stable symbol identity '{duplicate.Key.Value}' was generated for {string.Join(", ", duplicate.Select(d => d.Name))}.");

        var ordered = descriptors.OrderBy(d => d.Definition.FilePath, PathComparer)
                          .ThenBy(d => d.Definition.Span.Start)
                          .ThenBy(d => d.Id.Value, StringComparer.Ordinal)
                          .ToArray();
        var byId = ordered.ToImmutableDictionary(d => d.Id);
        var byName = ordered.GroupBy(d => d.Name, StringComparer.Ordinal).ToImmutableDictionary(group => group.Key, group => group.Select(d => d.Id).OrderBy(id => id.Value, StringComparer.Ordinal).ToImmutableArray(), StringComparer.Ordinal);
        var byDocument = ordered.GroupBy(d => d.Definition.DocumentId).ToImmutableDictionary(group => group.Key, group => group.Select(d => d.Id).ToImmutableArray());
        return new(project.Id, project.Version, byId, byName, byDocument);
    }

    static Dictionary<Symbol, Symbol> BuildContainingMap(IEnumerable<Entry> declarations)
    {
        var map = new Dictionary<Symbol, Symbol>(ReferenceEqualityComparer.Instance);
        foreach (var entry in declarations)
        {
            if (entry.Symbol is MemberSymbol member && member.ContainingType is Symbol owner)
                map[member] = owner;
            if (entry.Symbol is TypeParameterSymbol typeParameter)
                map[typeParameter] = typeParameter.ContainingSymbol;
            switch (entry.Symbol)
            {
            case FunctionSymbol function:
                foreach (var parameter in function.Parameters)
                    map[parameter] = function;
                foreach (var parameter in function.TypeParameters)
                    map[parameter] = function;
                break;
            case MethodSymbol method:
                foreach (var parameter in method.Parameters)
                    map[parameter] = method;
                break;
            case InitializerSymbol initializer:
                foreach (var parameter in initializer.Parameters)
                    map[parameter] = initializer;
                break;
            case EnumCaseSymbol enumCase:
                foreach (var parameter in enumCase.AssociatedValues)
                    map[parameter] = enumCase;
                break;
            case LocalVariableSymbol local when local.DeclarationLocation is {} location:
                var enclosing = entry.Model.GetEnclosingSymbol(location.Span.Start);
                if (enclosing is not null)
                    map[local] = enclosing;
                break;
            }
        }
        return map;
    }

    static bool TryLocations(Symbol symbol, Dictionary<string, LanguageDocumentSnapshot> documents,
                             out DefinitionLocation definition, out ImmutableArray<DefinitionLocation> additional)
    {
        var locations = symbol.Locations.Select(location =>
                                                {
                                                    var path = location.FilePath;
                                                    return path is not null && documents.TryGetValue(Canonical(path), out var document)
                                                               ? new DefinitionLocation(document.Id, document.FilePath, location.Span)
                                                               : null;
                                                })
                            .OfType<DefinitionLocation>()
                            .Distinct()
                            .OrderBy(l => l.FilePath, PathComparer)
                            .ThenBy(l => l.Span.Start)
                            .ToArray();
        if (locations.Length == 0)
        {
            definition = null!;
            additional = [];
            return false;
        }
        definition = locations[0];
        additional = locations.Skip(1).ToImmutableArray();
        return true;
    }

    static string DeclarationKey(Symbol symbol, Dictionary<string, LanguageDocumentSnapshot> documents)
    {
        if (symbol.DeclarationLocation is not {} location || location.FilePath is null ||
            !documents.TryGetValue(Canonical(location.FilePath), out var document))
            return string.Empty;
        return $"{document.Id.Value:N}:{location.Span.Start}:{location.Span.Length}";
    }

    static string? Documentation(SemanticModel model, Martin.Compiler.Text.TextSpan declarationSpan)
    {
        var tokens = Flatten(model.SyntaxTree.Root).OfType<SyntaxToken>().ToArray();
        var tokenIndex = Array.FindIndex(tokens, t => t.Span == declarationSpan);
        if (tokenIndex < 0)
            return null;
        var trivia = tokens[tokenIndex].LeadingTrivia.AsEnumerable();
        // Documentation is normally attached to the declaration keyword, while the
        // compiler location identifies its name token.
        if (!trivia.Any(t => t.Kind == SyntaxKind.DocumentationCommentTrivia) && tokenIndex > 0)
            trivia = tokens[tokenIndex - 1].LeadingTrivia;
        var comments = trivia.Where(t => t.Kind == SyntaxKind.DocumentationCommentTrivia)
                           .Select(t => t.Text.StartsWith("///", StringComparison.Ordinal) ? t.Text[3..].TrimStart() : t.Text.Trim())
                           .ToArray();
        return comments.Length == 0 ? null : string.Join("\n", comments).Trim();
    }

    static IEnumerable<SyntaxNode> Flatten(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.GetChildren())
            foreach (var descendant in Flatten(child))
                yield return descendant;
    }

    static string Canonical(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    sealed record Entry(DocumentId DocumentId, SemanticModel Model, Symbol Symbol);
}
