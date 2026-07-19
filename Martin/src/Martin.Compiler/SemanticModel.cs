using System.Collections.Immutable;
using Martin.Compiler.Binding;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Generics;
using Martin.Compiler.Syntax;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;

namespace Martin.Compiler;

/// <summary>Describes how a bound symbol is used by source code.</summary>
public enum SymbolReferenceKind
{
    Declaration,
    Read,
    Write,
    Call,
    Type,
    MemberAccess,
    Initializer,
    Conformance,
    EnumCasePattern,
    PatternBinding,
    ProtocolRequirement,
    ProtocolWitness
}

/// <summary>Immutable semantic information for a pattern occurrence.</summary>
public sealed record PatternSemanticInfo(
    TypeSymbol InputType,
    ImmutableArray<LocalVariableSymbol> DeclaredVariables,
    EnumCaseSymbol? EnumCase,
    bool IsIrrefutable,
    bool HasErrors);

/// <summary>Describes a generic type or callable as it was used at a source location.</summary>
public sealed record GenericSemanticInfo(
    Symbol OriginalDefinition,
    Symbol ConstructedSymbol,
    ImmutableArray<TypeSymbol> TypeArguments,
    bool TypeArgumentsWereInferred,
    ImmutableArray<ConstraintValidationResult> ConstraintResults,
    SymbolIdentity OriginalDefinitionIdentity,
    TypeIdentity? ConstructedTypeIdentity);

/// <summary>Describes an exact typed-error effect published for a source occurrence.</summary>
public sealed record TypedErrorSemanticInfo(
    ErrorEffect Effect,
    bool IsAcknowledged,
    bool IsCaught,
    bool IsPropagated,
    TypeSymbol? DeclaredErrorType,
    ImmutableArray<Diagnostic> RelatedDiagnostics);

/// <summary>The authoritative typed-error relationship captured during binding.</summary>
internal sealed record TypedErrorUseInfo(
    ErrorEffect Effect,
    bool IsAcknowledged,
    bool IsCaught,
    bool IsPropagated,
    TypeSymbol? DeclaredErrorType,
    TextLocation Location);

/// <summary>The authoritative generic callable construction captured during binding.</summary>
internal sealed record GenericUseInfo(
    Symbol OriginalDefinition,
    Symbol ConstructedSymbol,
    ImmutableArray<TypeSymbol> TypeArguments,
    bool TypeArgumentsWereInferred,
    TextLocation Location);

/// <summary>A source occurrence of a symbol.</summary>
public sealed record SymbolReference(
    Symbol Symbol,
    SyntaxNode Syntax,
    TextLocation Location,
    SymbolReferenceKind Kind);

/// <summary>
/// Provides safe, read-only semantic queries for one syntax tree in a compilation.
/// Query methods deliberately return empty/partial results for malformed source.
/// </summary>
public sealed class SemanticModel
{
    private readonly Compilation _compilation;
    private readonly Dictionary<SyntaxNode, Symbol> _declared;
    private readonly Dictionary<SyntaxNode, Symbol> _symbols;
    private readonly Dictionary<ExpressionSyntax, TypeSymbol> _types;
    private readonly Dictionary<ExpressionSyntax, Conversion> _conversions;
    private readonly Dictionary<SyntaxNode, GenericUseInfo> _genericUses;
    private readonly Dictionary<SyntaxNode, TypedErrorUseInfo> _typedErrorUses;
    private readonly Dictionary<SyntaxNode, SyntaxNode?> _parents = [];
    private ImmutableArray<SyntaxNode> _nodes;

    internal SemanticModel(
        Compilation compilation,
        SyntaxTree syntaxTree,
        Dictionary<SyntaxNode, Symbol> declared,
        Dictionary<SyntaxNode, Symbol> symbols,
        Dictionary<ExpressionSyntax, TypeSymbol> types,
        Dictionary<ExpressionSyntax, Conversion> conversions,
        Dictionary<SyntaxNode, GenericUseInfo> genericUses,
        Dictionary<SyntaxNode, TypedErrorUseInfo> typedErrorUses)
    {
        _compilation = compilation;
        SyntaxTree = syntaxTree;
        _declared = declared;
        _symbols = symbols;
        _types = types;
        _conversions = conversions;
        _genericUses = genericUses;
        _typedErrorUses = typedErrorUses;
        _nodes = Flatten(syntaxTree.Root, null).ToImmutableArray();
    }

    public SyntaxTree SyntaxTree { get; }
    public ImmutableArray<Diagnostic> Diagnostics => _compilation.Diagnostics;

    public Symbol? GetDeclaredSymbol(SyntaxNode node) =>
        _declared.TryGetValue(node, out var symbol) && IsInThisTree(node) ? symbol : null;

    public Symbol? GetSymbolInfo(SyntaxNode node)
    {
        if (_symbols.TryGetValue(node, out var symbol) && IsInThisTree(node))
            return symbol;

        // Binding information is generally attached to an expression. Accepting a
        // token here makes identifier-at-caret queries useful without leaking this
        // implementation detail to language-service clients.
        for (var current = node; _parents.TryGetValue(current, out var parent) && parent is not null; current = parent)
            if (_symbols.TryGetValue(parent, out symbol))
                return symbol;
        return null;
    }

    public TypeSymbol? GetTypeInfo(ExpressionSyntax expression) =>
        _types.TryGetValue(expression, out var type) && IsInThisTree(expression) ? type : null;

    public Conversion GetConversion(ExpressionSyntax expression) =>
        _conversions.TryGetValue(expression, out var conversion) && IsInThisTree(expression)
            ? conversion
            : Conversion.None;

    /// <summary>Gets definition, substitution, inference, and constraint data for a generic use.</summary>
    public GenericSemanticInfo? GetGenericInfo(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!IsInThisTree(node)) return null;
        SyntaxNode? semanticNode = node;
        Symbol? symbol = null;
        GenericUseInfo? genericUse = null;
        while (semanticNode is not null)
        {
            if (_genericUses.TryGetValue(semanticNode, out genericUse))
            {
                symbol = genericUse.ConstructedSymbol;
                break;
            }
            // Callable symbols alone do not carry an exact substitution. Keep walking
            // to the enclosing call, where the binder stored its authoritative use.
            if (_symbols.TryGetValue(semanticNode, out symbol) &&
                symbol is ConstructedTypeSymbol or InitializerSymbol { ContainingType: ConstructedTypeSymbol }
                    or EnumCaseSymbol { ContainingType: ConstructedTypeSymbol })
                break;
            semanticNode = _parents.GetValueOrDefault(semanticNode);
        }
        if (symbol is null) return null;

        Symbol original;
        ImmutableArray<TypeSymbol> arguments;
        bool argumentsWereInferred;
        TypeIdentity? typeIdentity = null;
        if (genericUse is not null)
        {
            original = genericUse.OriginalDefinition;
            arguments = genericUse.TypeArguments;
            argumentsWereInferred = genericUse.TypeArgumentsWereInferred;
        }
        else switch (symbol)
        {
            case ConstructedTypeSymbol type:
                original = type.GenericDefinition; arguments = type.TypeArguments; typeIdentity = type.Identity;
                argumentsWereInferred = false; break;
            case InitializerSymbol { ContainingType: ConstructedTypeSymbol type }:
                original = type.GenericDefinition; symbol = type; arguments = type.TypeArguments; typeIdentity = type.Identity;
                argumentsWereInferred = false; break;
            case EnumCaseSymbol { ContainingType: ConstructedTypeSymbol type }:
                original = type.GenericDefinition; symbol = type; arguments = type.TypeArguments; typeIdentity = type.Identity;
                argumentsWereInferred = false; break;
            default: return null;
        }

        var parameters = original switch
        {
            NamedTypeSymbol type => type.TypeParameters,
            FunctionSymbol function => function.TypeParameters,
            MethodSymbol method => method.TypeParameters,
            _ => ImmutableArray<TypeParameterSymbol>.Empty
        };
        var validator = new GenericConstraintValidator(_compilation.GetConformances());
        var results = parameters
            .Zip(arguments, (parameter, argument) => validator.Validate(parameter, argument))
            .ToImmutableArray();
        return new(original, symbol, arguments, argumentsWereInferred,
            results, SymbolIdentity.Create(original), typeIdentity);
    }


    /// <summary>Gets typed-error effect, acknowledgement, catch/propagation, identity, and diagnostics for a syntax node.</summary>
    public TypedErrorSemanticInfo? GetTypedErrorInfo(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!IsInThisTree(node)) return null;
        for (SyntaxNode? current = node; current is not null; current = _parents.GetValueOrDefault(current))
            if (_typedErrorUses.TryGetValue(current, out var info))
            {
                return new(info.Effect, info.IsAcknowledged, info.IsCaught, info.IsPropagated,
                    info.DeclaredErrorType, RelatedDiagnostics(info.Location));
            }
        return null;
    }

    public TypedErrorSemanticInfo? GetTryInfo(ExpressionSyntax tryExpression) =>
        tryExpression.Kind == SyntaxKind.TryExpression ? GetTypedErrorInfo(tryExpression) : null;

    public TypedErrorSemanticInfo? GetThrowInfo(StatementSyntax throwStatement) =>
        throwStatement.Kind == SyntaxKind.ThrowStatement ? GetTypedErrorInfo(throwStatement) : null;

    public TypedErrorSemanticInfo? GetCatchInfo(SyntaxNode catchClause) =>
        catchClause.Kind == SyntaxKind.CatchClause ? GetTypedErrorInfo(catchClause) : null;

    private ImmutableArray<Diagnostic> RelatedDiagnostics(TextLocation location) => Diagnostics
        .Where(diagnostic => ReferenceEquals(diagnostic.Location.Text, location.Text) && Intersects(diagnostic.Location.Span, location.Span))
        .ToImmutableArray();

    private static bool Intersects(TextSpan left, TextSpan right) => left.Start < right.End && right.Start < left.End || left.Length == 0 && Contains(right, left.Start, true);

    private static TypeSymbol? CanonicalType(TypeSymbol? type) => type is ConstructedTypeSymbol constructed ? constructed.GenericDefinition : type;

    /// <summary>Gets exhaustiveness and usefulness information for a switch statement.</summary>
    public SwitchAnalysisResult? GetSwitchAnalysis(StatementSyntax switchStatement)
    {
        ArgumentNullException.ThrowIfNull(switchStatement);
        if (switchStatement.Kind != SyntaxKind.SwitchStatement || !IsInThisTree(switchStatement)) return null;
        return _compilation.FindSwitchAnalysis(new TextLocation(SyntaxTree.Text, switchStatement.Span));
    }

    /// <summary>Gets the bound meaning of a pattern in this syntax tree.</summary>
    public PatternSemanticInfo? GetPatternInfo(PatternSyntax pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (!IsInThisTree(pattern)) return null;
        var bound = _compilation.FindPattern(new TextLocation(SyntaxTree.Text, pattern.Span));
        return bound is null ? null : new(bound.InputType, bound.DeclaredVariables,
            bound is BoundEnumCasePattern enumCase ? enumCase.Case : null,
            bound.IsIrrefutable, bound.HasErrors);
    }

    /// <summary>Finds the token containing <paramref name="position"/>.</summary>
    public SyntaxToken? FindToken(int position, bool includeTrivia = false)
    {
        if (position < 0 || position > SyntaxTree.Text.Length)
            return null;

        return _nodes.OfType<SyntaxToken>().FirstOrDefault(token =>
            Contains(token.Span, position, token.Kind == SyntaxKind.EndOfFileToken) ||
            includeTrivia && Contains(token.FullSpan, position, token.Kind == SyntaxKind.EndOfFileToken));
    }

    /// <summary>Finds the innermost (or outermost) node containing a position.</summary>
    public SyntaxNode? FindNode(int position, bool innermost = true) =>
        FindNode(new TextSpan(position, 0), innermost);

    /// <summary>Finds the innermost (or outermost) node containing a span.</summary>
    public SyntaxNode? FindNode(TextSpan span, bool innermost = true)
    {
        if (span.Start < 0 || span.Length < 0 || span.End > SyntaxTree.Text.Length)
            return null;

        var matches = _nodes.Where(node => Contains(node.FullSpan, span)).ToArray();
        return innermost ? matches.LastOrDefault() : matches.FirstOrDefault();
    }

    public ImmutableArray<Symbol> GetDeclaredSymbols() => _declared
        .Where(pair => IsInThisTree(pair.Key))
        .OrderBy(pair => pair.Key.Span.Start)
        .Select(pair => pair.Value)
        .Distinct()
        .ToImmutableArray();

    public ImmutableArray<SymbolReference> GetSymbolReferences()
    {
        var result = ImmutableArray.CreateBuilder<SymbolReference>();
        foreach (var (syntax, symbol) in _declared.Where(pair => IsInThisTree(pair.Key)))
            result.Add(new(symbol, syntax, DeclarationLocation(symbol, syntax), SymbolReferenceKind.Declaration));

        foreach (var (syntax, symbol) in _symbols.Where(pair => IsInThisTree(pair.Key)))
        {
            // A call and its target can both carry the same binding. Publish only
            // the smallest occurrence so reference indexes remain duplicate-free.
            if (_parents.TryGetValue(syntax, out var parent) && parent is not null &&
                _symbols.TryGetValue(parent, out var parentSymbol) && ReferenceEquals(symbol, parentSymbol))
                continue;
            result.Add(new(symbol, syntax, new TextLocation(SyntaxTree.Text, ReferenceSpan(syntax)), ReferenceKind(syntax, symbol)));
        }

        foreach (var (syntax, info) in _typedErrorUses.Where(pair => IsInThisTree(pair.Key)))
        {
            var errorType = CanonicalType(info.DeclaredErrorType ?? info.Effect.ErrorType);
            if (errorType is null || ReferenceEquals(errorType, TypeSymbol.Error)) continue;
            if (_symbols.TryGetValue(syntax, out var existing) && ReferenceEquals(CanonicalSymbol(existing), errorType)) continue;
            result.Add(new(errorType, syntax, new TextLocation(SyntaxTree.Text, ReferenceSpan(syntax)), SymbolReferenceKind.Type));
        }

        return result.OrderBy(reference => reference.Location.Span.Start)
            .ThenBy(reference => reference.Kind)
            .ToImmutableArray();
    }

    /// <summary>Returns symbols visible at a source position, with inner declarations shadowing outer ones.</summary>
    public ImmutableArray<Symbol> LookupSymbols(int position)
    {
        if (position < 0 || position > SyntaxTree.Text.Length)
            return [];

        var enclosing = FindNode(position);
        var candidates = _compilation.GetAllSymbols().Where(IsAlwaysVisible).ToList();
        candidates.AddRange(_declared.Where(pair => IsInThisTree(pair.Key) && IsVisibleDeclaration(pair.Key, position, enclosing))
            .Select(pair => pair.Value));

        var containingType = GetContainingType(enclosing);
        if (containingType is not null)
        {
            candidates.AddRange(containingType.TypeParameters);
            candidates.AddRange(containingType.Members);
        }

        return candidates
            .GroupBy(symbol => symbol.Name, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(symbol => symbol.Name, StringComparer.Ordinal)
            .ThenBy(symbol => symbol.Kind)
            .ToImmutableArray();
    }

    public Symbol? GetEnclosingSymbol(int position)
    {
        var node = FindNode(position);
        while (node is not null)
        {
            if (_declared.TryGetValue(node, out var symbol) && symbol is FunctionSymbol or MethodSymbol or InitializerSymbol or NamedTypeSymbol)
                return symbol;
            node = _parents.GetValueOrDefault(node);
        }
        return null;
    }

    public ImmutableArray<MemberSymbol> LookupMembers(TypeSymbol type, int position)
    {
        if (position < 0 || position > SyntaxTree.Text.Length)
            return [];
        return type switch
        {
            NamedTypeSymbol named => named.Members,
            ConstructedTypeSymbol constructed => constructed.Members,
            OptionalTypeSymbol optional => LookupMembers(optional.ElementType, position),
            _ => []
        };
    }

    /// <summary>Returns the contextual target type for common expression positions.</summary>
    public TypeSymbol? GetExpectedType(int position)
    {
        var node = FindNode(position);
        var expression = AncestorOrSelf<ExpressionSyntax>(node);
        if (expression is not null && _conversions.TryGetValue(expression, out var conversion) &&
            !conversion.IsIdentity && _types.TryGetValue(expression, out var actual))
        {
            // Walk outward: the parent's already-bound result is the conversion target.
            var parentExpression = AncestorOrSelf<ExpressionSyntax>(_parents.GetValueOrDefault(expression));
            if (parentExpression is not null && _types.TryGetValue(parentExpression, out var parentType) && parentType != actual)
                return parentType;
        }

        var enclosing = GetEnclosingSymbol(position);
        for (var current = node; current is not null; current = _parents.GetValueOrDefault(current))
        {
            if (current.Kind == SyntaxKind.ReturnStatement)
                return enclosing switch { FunctionSymbol f => f.ReturnType, MethodSymbol m => m.ReturnType, _ => null };
            if ((current.Kind is SyntaxKind.IfStatement or SyntaxKind.WhileStatement) &&
                current.GetChildren().OfType<ExpressionSyntax>().FirstOrDefault() is { } condition &&
                condition.FullSpan.Start <= position && position <= condition.FullSpan.End)
                return TypeSymbol.Bool;
            if (current.Kind == SyntaxKind.VariableDeclarationStatement && _declared.TryGetValue(current, out var declared) && declared is VariableSymbol variable)
                return variable.Type;
        }
        return null;
    }

    /// <summary>Returns bound or plausible callable symbols for complete and incomplete invocations.</summary>
    public ImmutableArray<Symbol> GetInvocationCandidates(SyntaxNode invocationOrIncompleteInvocation)
    {
        if (!IsInThisTree(invocationOrIncompleteInvocation))
            return [];
        if (GetSymbolInfo(invocationOrIncompleteInvocation) is { } bound && IsCallable(bound))
            return [bound];

        var callee = invocationOrIncompleteInvocation.Kind == SyntaxKind.CallExpression
            ? invocationOrIncompleteInvocation.GetChildren().OfType<ExpressionSyntax>().FirstOrDefault()
            : invocationOrIncompleteInvocation as ExpressionSyntax;
        if (callee is null)
            return [];

        if (callee is GenericExpressionSyntax { Kind: SyntaxKind.GenericName } genericName)
            callee = genericName.GetChildren().OfType<ExpressionSyntax>().FirstOrDefault() ?? callee;

        ExpressionSyntax? receiver = null;
        SyntaxToken? token;
        if (callee is GenericExpressionSyntax { Kind: SyntaxKind.MemberAccessExpression } memberAccess)
        {
            receiver = memberAccess.GetChildren().OfType<ExpressionSyntax>().FirstOrDefault();
            token = memberAccess.GetChildren().OfType<SyntaxToken>()
                .FirstOrDefault(candidate => candidate.Kind == SyntaxKind.IdentifierToken);
        }
        else
        {
            token = Descendants(callee).Prepend(callee).OfType<SyntaxToken>()
                .FirstOrDefault(candidate => candidate.Kind == SyntaxKind.IdentifierToken);
        }
        if (token is null)
            return [];

        if (receiver is not null && GetTypeInfo(receiver) is { } receiverType)
            return LookupMembers(receiverType, token.Position).Where(member => member.Name == token.Text && IsCallable(member)).Cast<Symbol>().ToImmutableArray();

        return LookupSymbols(token.Position).Where(symbol => symbol.Name == token.Text && IsCallable(symbol)).ToImmutableArray();
    }

    private IEnumerable<SyntaxNode> Flatten(SyntaxNode node, SyntaxNode? parent)
    {
        _parents[node] = parent;
        yield return node;
        foreach (var child in node.GetChildren())
            foreach (var descendant in Flatten(child, node))
                yield return descendant;
    }

    private bool IsInThisTree(SyntaxNode node) => _parents.ContainsKey(node);
    private static bool Contains(TextSpan span, int position, bool includeEnd = false) =>
        span.Start <= position && (position < span.End || includeEnd && position == span.End || span.Length == 0 && position == span.Start);
    private static bool Contains(TextSpan outer, TextSpan inner) => outer.Start <= inner.Start && inner.End <= outer.End;
    private static IEnumerable<SyntaxNode> Descendants(SyntaxNode node) => node.GetChildren().SelectMany(child => new[] { child }.Concat(Descendants(child)));
    private T? AncestorOrSelf<T>(SyntaxNode? node) where T : SyntaxNode
    {
        while (node is not null)
        {
            if (node is T result) return result;
            node = _parents.GetValueOrDefault(node);
        }
        return null;
    }

    private bool IsAlwaysVisible(Symbol symbol) => symbol is TypeSymbol or FunctionSymbol { IsBuiltIn: true } ||
        (symbol is FunctionSymbol or NamedTypeSymbol) && symbol.Locations.Any(location => location.Text == SyntaxTree.Text || location.FilePath != SyntaxTree.Text.FilePath);

    private bool IsVisibleDeclaration(SyntaxNode declaration, int position, SyntaxNode? enclosing)
    {
        if (declaration.Span.Start > position) return false;
        if (declaration is MemberSyntax) return true;
        // A pattern local belongs to its case, not to the switch's containing
        // block.  Syntax nesting alone would otherwise make it appear in later
        // sibling cases during completion and other position-based queries.
        if (declaration is PatternSyntax)
        {
            var declarationCase = AncestorOfKind(declaration, SyntaxKind.SwitchCase);
            var positionCase = AncestorOfKind(enclosing, SyntaxKind.SwitchCase);
            return declarationCase is not null && ReferenceEquals(declarationCase, positionCase);
        }
        var declarationOwner = EnclosingCallableSyntax(declaration);
        var positionOwner = EnclosingCallableSyntax(enclosing);
        if (declarationOwner is not null && !ReferenceEquals(declarationOwner, positionOwner)) return false;
        var declarationBlock = AncestorOfKind(declaration, SyntaxKind.BlockStatement);
        var positionBlock = AncestorOfKind(enclosing, SyntaxKind.BlockStatement);
        if (declarationBlock is null) return true;
        for (var block = positionBlock; block is not null; block = AncestorOfKind(_parents.GetValueOrDefault(block), SyntaxKind.BlockStatement))
            if (ReferenceEquals(block, declarationBlock)) return true;
        return false;
    }

    private SyntaxNode? EnclosingCallableSyntax(SyntaxNode? node)
    {
        while (node is not null)
        {
            if (node.Kind is SyntaxKind.FunctionDeclaration or SyntaxKind.MethodDeclaration or SyntaxKind.InitializerDeclaration)
                return node;
            node = _parents.GetValueOrDefault(node);
        }
        return null;
    }

    private SyntaxNode? AncestorOfKind(SyntaxNode? node, SyntaxKind kind)
    {
        while (node is not null) { if (node.Kind == kind) return node; node = _parents.GetValueOrDefault(node); }
        return null;
    }

    private NamedTypeSymbol? GetContainingType(SyntaxNode? node)
    {
        while (node is not null)
        {
            if (_declared.TryGetValue(node, out var symbol) && symbol is NamedTypeSymbol type) return type;
            node = _parents.GetValueOrDefault(node);
        }
        return null;
    }

    private static Symbol CanonicalSymbol(Symbol symbol) => symbol is ConstructedTypeSymbol constructed ? constructed.GenericDefinition : symbol;

    private TextLocation DeclarationLocation(Symbol symbol, SyntaxNode syntax) =>
        symbol.DeclarationLocation is { } location ? location : new TextLocation(SyntaxTree.Text, syntax.Span);

    private static TextSpan ReferenceSpan(SyntaxNode syntax) =>
        Descendants(syntax).OfType<SyntaxToken>().FirstOrDefault(token => token.Kind == SyntaxKind.IdentifierToken)?.Span ?? syntax.Span;

    private SymbolReferenceKind ReferenceKind(SyntaxNode syntax, Symbol symbol)
    {
        var parent = _parents.GetValueOrDefault(syntax);
        if (AncestorOfKind(syntax, SyntaxKind.ProtocolConformanceClause) is not null)
            return SymbolReferenceKind.Conformance;
        if (AncestorOfKind(syntax, SyntaxKind.ValueBindingPattern) is not null)
            return SymbolReferenceKind.PatternBinding;
        if (symbol is EnumCaseSymbol && AncestorOfKind(syntax, SyntaxKind.EnumCasePattern) is not null)
            return SymbolReferenceKind.EnumCasePattern;
        if (symbol is ProtocolRequirementSymbol)
            return SymbolReferenceKind.ProtocolRequirement;
        if (symbol is TypeSymbol) return SymbolReferenceKind.Type;
        if (parent?.Kind == SyntaxKind.AssignmentExpression || parent?.Kind == SyntaxKind.PropertyAssignmentExpression)
            return SymbolReferenceKind.Write;
        if (syntax.Kind == SyntaxKind.CallExpression || parent?.Kind == SyntaxKind.CallExpression)
            return symbol is InitializerSymbol ? SymbolReferenceKind.Initializer : SymbolReferenceKind.Call;
        if (symbol is MemberSymbol) return SymbolReferenceKind.MemberAccess;
        return SymbolReferenceKind.Read;
    }

    private static bool IsCallable(Symbol symbol) => symbol is FunctionSymbol or MethodSymbol or InitializerSymbol or EnumCaseSymbol or ProtocolMethodRequirementSymbol;
}
