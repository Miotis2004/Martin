using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Martin.Compiler.Text;

namespace Martin.LanguageServices;

internal static class SemanticClassificationProvider
{
    public static ImmutableArray<ClassifiedSpan> Classify(SyntaxTree tree, SemanticModel model)
    {
        var occurrences = model.GetSymbolReferences()
                              .GroupBy(reference => reference.Location.Span)
                              .ToDictionary(group => group.Key, group => group.First());
        var diagnostics = model.Diagnostics.Where(d => d.Location.Text == tree.Text).ToImmutableArray();
        var result = new List<ClassifiedSpan>();

        foreach (var token in Descendants(tree.Root).OfType<SyntaxToken>().Where(t => !t.IsMissing))
        {
            foreach (var trivia in token.LeadingTrivia.Concat(token.TrailingTrivia).Where(IsComment))
                result.Add(new(trivia.Span,
                               trivia.Kind == SyntaxKind.DocumentationCommentTrivia ? ClassificationKind.DocumentationComment : ClassificationKind.Comment,
                               trivia.Kind == SyntaxKind.DocumentationCommentTrivia ? ClassificationModifiers.Documentation : ClassificationModifiers.None));

            if (token.Kind == SyntaxKind.EndOfFileToken || token.Span.Length == 0)
                continue;
            if (token.Kind != SyntaxKind.IdentifierToken)
            {
                result.Add(new(token.Span, LexicalKind(token.Kind)));
                continue;
            }

            occurrences.TryGetValue(token.Span, out var occurrence);
            var symbol = occurrence?.Symbol ?? model.GetSymbolInfo(token) ??
                         model.LookupSymbols(token.Position).OfType<TypeSymbol>().FirstOrDefault(candidate => candidate.Name == token.Text);
            if (symbol is not null)
                result.Add(occurrence?.Kind switch {
                    SymbolReferenceKind.PatternBinding => new ClassifiedSpan(token.Span, ClassificationKind.PatternVariable,
                                                                             ClassificationModifiers.Declaration | ClassificationModifiers.Definition | ClassificationModifiers.ReadOnly),
                    SymbolReferenceKind.Conformance => new ClassifiedSpan(token.Span, ClassificationKind.ProtocolConformance),
                    SymbolReferenceKind.ProtocolRequirement => new ClassifiedSpan(token.Span, ClassificationKind.ProtocolRequirement),
                    SymbolReferenceKind.ProtocolWitness => new ClassifiedSpan(token.Span, ClassificationKind.ProtocolWitness),
                    _ => ClassifySymbol(token.Span, symbol, occurrence?.Kind == SymbolReferenceKind.Declaration)
                });
            else if (IsUnresolved(token, diagnostics))
                result.Add(new(token.Span, ClassificationKind.UnresolvedIdentifier,
                               ClassificationModifiers.Unresolved));
        }

        return Normalize(result);
    }

    private static ClassifiedSpan ClassifySymbol(TextSpan span, Symbol symbol, bool declaration)
    {
        var kind = symbol switch {
            ProtocolTypeSymbol => ClassificationKind.Protocol,
            EnumTypeSymbol => ClassificationKind.Enum,
            StructTypeSymbol => ClassificationKind.Struct,
            ClassTypeSymbol => ClassificationKind.Class,
            TypeParameterSymbol => ClassificationKind.TypeParameter,
            TypeSymbol => ClassificationKind.Type,
            EnumCaseSymbol => ClassificationKind.EnumCase,
            InitializerSymbol => ClassificationKind.Initializer,
            ProtocolRequirementSymbol => ClassificationKind.ProtocolRequirement,
            MethodSymbol => ClassificationKind.Method,
            PropertySymbol => ClassificationKind.Property,
            FunctionSymbol { IsBuiltIn : true } => ClassificationKind.BuiltIn,
            FunctionSymbol => ClassificationKind.Function,
            ParameterSymbol or SelfParameterSymbol => ClassificationKind.Parameter,
            GlobalVariableSymbol => ClassificationKind.Global,
            LocalVariableSymbol variable => variable.IsReadOnly ? ClassificationKind.ImmutableVariable : ClassificationKind.MutableVariable,
            VariableSymbol => ClassificationKind.Local,
            _ => ClassificationKind.UnresolvedIdentifier
        };
        var modifiers = declaration ? ClassificationModifiers.Declaration | ClassificationModifiers.Definition : ClassificationModifiers.None;
        if (symbol is VariableSymbol { IsReadOnly : true } or PropertySymbol { IsReadOnly : true })
            modifiers |= ClassificationModifiers.ReadOnly;
        if (symbol is MemberSymbol { IsStatic : true })
            modifiers |= ClassificationModifiers.Static;
        if (symbol is FunctionSymbol { IsBuiltIn : true } || symbol is TypeSymbol { Locations.IsDefaultOrEmpty : true })
            modifiers |= ClassificationModifiers.DefaultLibrary;
        return new(span, kind, modifiers);
    }

    private static bool IsUnresolved(SyntaxToken token, ImmutableArray<Diagnostic> diagnostics) => diagnostics.Any(d =>
                                                                                                                       d.Location.Span == token.Span && d.Code is "MRT2001" or "MRT2014" or "MRT2102" or "MRT2145" or "MRT2185");

    private static ClassificationKind LexicalKind(SyntaxKind kind) => kind switch {
        var value when SyntaxFacts.IsKeyword(value) => ClassificationKind.Keyword,
        SyntaxKind.IntegerLiteralToken or SyntaxKind.FloatingPointLiteralToken => ClassificationKind.NumberLiteral,
        SyntaxKind.StringLiteralToken => ClassificationKind.StringLiteral,
        var value when SyntaxFacts.GetBinaryOperatorPrecedence(value) > 0 || SyntaxFacts.GetUnaryOperatorPrecedence(value) > 0 ||
            value is SyntaxKind.EqualToken or SyntaxKind.ArrowToken => ClassificationKind.Operator,
        _ => ClassificationKind.Punctuation
    };

    private static ImmutableArray<ClassifiedSpan> Normalize(IEnumerable<ClassifiedSpan> spans)
    {
        var sorted = spans.Where(span => span.Span.Length > 0).OrderBy(span => span.Span.Start).ThenBy(span => span.Span.Length).ToArray();
        var result = ImmutableArray.CreateBuilder<ClassifiedSpan>();
        foreach (var span in sorted)
        {
            if (result.Count > 0 && span.Span.Start < result[^1].Span.End)
                continue;
            if (result.Count > 0 && result[^1].Kind == span.Kind && result[^1].Modifiers == span.Modifiers && result[^1].Span.End == span.Span.Start)
                result[^1] = result[^1] with { Span = TextSpan.FromBounds(result[^1].Span.Start, span.Span.End) };
            else
                result.Add(span);
        }
        return result.ToImmutable();
    }

    private static bool IsComment(SyntaxTrivia trivia) => trivia.Kind is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia or SyntaxKind.DocumentationCommentTrivia;
    private static IEnumerable<SyntaxNode> Descendants(SyntaxNode node) => new[] { node }.Concat(node.GetChildren().SelectMany(Descendants));
}
