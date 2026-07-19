using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;

namespace Martin.LanguageServices;

/// <summary>Produces call assistance from the recovered syntax tree and compiler symbols.</summary>
internal static class SemanticSignatureHelpProvider
{
    public static SignatureHelp? Get(LanguageProjectSnapshot project, ProjectAnalysis analysis, LanguageDocumentSnapshot document, int position,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        position = Math.Clamp(position, 0, document.Text.Length);
        if (!analysis.SemanticModels.TryGetValue(document.Id, out var model)) return null;
        var tree = model.SyntaxTree;
        if (InTriviaOrString(model, position)) return null;

        var invocation = DescendantsAndSelf(tree.Root)
            .Where(n => n.Kind == SyntaxKind.CallExpression && EnclosesCaret(n, position))
            .OrderByDescending(OpenParenthesisPosition).FirstOrDefault();
        if (invocation is null)
            return PatternHelp(project, model, document, position);

        var candidates = model.GetInvocationCandidates(invocation).ToList();
        var name = CalleeName(invocation);
        // Binding can select one declaration. Add same-name declarations so overload help remains complete.
        foreach (var otherModel in analysis.SemanticModels.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var symbol in otherModel.GetDeclaredSymbols().Where(s => MatchesGlobalOverload(s, name)))
                if (!candidates.Contains(symbol)) candidates.Add(symbol);
        }
        candidates = candidates.Where(IsCallable).Where(c => LabelsAreCompatible(c, invocation, position)).ToList();
        if (candidates.Count == 0) return null;

        var identity = new SymbolIdentityFactory(project.Id);
        var signatures = candidates.Select(c => Information(c, identity)).DistinctBy(s => s.Label).ToImmutableArray();
        var activeParameter = invocation.GetChildren().OfType<SyntaxToken>()
            .Count(t => t.Kind == SyntaxKind.CommaToken && t.Span.Start < position);
        var activeSignature = 0;
        for (var i = 0; i < signatures.Length; i++)
            if (activeParameter < signatures[i].Parameters.Length) { activeSignature = i; break; }
        return new SignatureHelp { Signatures = signatures, ActiveSignature = activeSignature, ActiveParameter = activeParameter };
    }

    private static SignatureHelp? PatternHelp(LanguageProjectSnapshot project, SemanticModel model,
        LanguageDocumentSnapshot document, int position)
    {
        var pattern = DescendantsAndSelf(model.SyntaxTree.Root).OfType<EnumCasePatternSyntax>()
            .Where(p => p.Arguments is not null && p.Arguments.FullSpan.Start <= position && position <= p.Arguments.FullSpan.End)
            .OrderBy(p => p.FullSpan.Length).FirstOrDefault();
        if (pattern is null || model.GetPatternInfo(pattern)?.EnumCase is not { } enumCase) return null;
        var identity = new SymbolIdentityFactory(project.Id);
        var info = Information(enumCase, identity);
        var start = Math.Clamp(pattern.Arguments!.FullSpan.Start, 0, document.Text.Length);
        var end = Math.Clamp(position, start, document.Text.Length);
        var active = document.Text[start..end].Count(c => c == ',');
        return new SignatureHelp { Signatures = [info], ActiveParameter = Math.Min(active, Math.Max(0, info.Parameters.Length - 1)) };
    }

    private static SignatureInformation Information(Symbol symbol, SymbolIdentityFactory identity)
    {
        var parameters = Parameters(symbol).Select(p => new ParameterInformation(
            p.Label is null or "_" ? p.Name : p.Label + ": " + p.Name, TypeName(p.Type))).ToImmutableArray();
        var (name, result, generic, errorType) = symbol switch
        {
            FunctionSymbol f => (f.Name, TypeName(f.ReturnType), f.TypeParameters, f.IsThrowing ? f.ErrorType : null),
            MethodSymbol m => (m.Name, TypeName(m.ReturnType), m.TypeParameters, m.IsThrowing ? m.ErrorType : null),
            InitializerSymbol i => (TypeName(i.ContainingType), TypeName(i.ContainingType), ImmutableArray<TypeParameterSymbol>.Empty, i.IsThrowing ? i.ErrorType : null),
            EnumCaseSymbol e => (e.Name, TypeName(e.ContainingType), ImmutableArray<TypeParameterSymbol>.Empty, (TypeSymbol?)null),
            ProtocolMethodRequirementSymbol r => (r.Name, TypeName(r.ReturnType), r.TypeParameters, r.IsThrowing ? r.ErrorType : null),
            _ => (symbol.Name, "Void", ImmutableArray<TypeParameterSymbol>.Empty, (TypeSymbol?)null)
        };
        var genericText = generic.IsDefaultOrEmpty ? "" : "<" + string.Join(", ", generic.Select(t => t.Name)) + ">";
        var parameterText = string.Join(", ", parameters.Select(p => $"{p.Label}: {p.Type}"));
        var label = $"{name}{genericText}({parameterText}){(errorType is null ? "" : " throws " + TypeName(errorType))} -> {result}";
        return new SignatureInformation { Name = name, Label = label, ReturnType = result, Parameters = parameters,
            SymbolId = identity.Create(symbol, declarationDiscriminator: symbol.DeclarationLocation?.Span.Start.ToString()) };
    }

    private static string TypeName(TypeSymbol type) => type switch
    {
        OptionalTypeSymbol optional => TypeName(optional.ElementType) + "?",
        ConstructedTypeSymbol constructed => constructed.GenericDefinition.Name + "<" +
            string.Join(", ", constructed.TypeArguments.Select(TypeName)) + ">",
        _ => type.Name
    };

    private static bool LabelsAreCompatible(Symbol symbol, SyntaxNode invocation, int position)
    {
        var supplied = invocation.GetChildren().Where(n => n.Kind == SyntaxKind.Argument && n.Span.Start < position)
            .Select(n => n.GetChildren().OfType<SyntaxToken>().Take(2).ToArray())
            .Select((tokens, index) => (index, label: tokens.Length == 2 && tokens[1].Kind == SyntaxKind.ColonToken ? tokens[0].Text : null));
        var parameters = Parameters(symbol);
        return supplied.All(a => a.index >= parameters.Length || a.label is null || parameters[a.index].Label == a.label);
    }

    private static ImmutableArray<ParameterSymbol> Parameters(Symbol symbol) => symbol switch
    { FunctionSymbol f => f.Parameters, MethodSymbol m => m.Parameters, InitializerSymbol i => i.Parameters,
      EnumCaseSymbol e => e.AssociatedValues, ProtocolMethodRequirementSymbol r => r.Parameters, _ => [] };
    private static bool IsCallable(Symbol s) => s is FunctionSymbol or MethodSymbol or InitializerSymbol or EnumCaseSymbol or ProtocolMethodRequirementSymbol;
    private static bool MatchesGlobalOverload(Symbol s, string name) => s is FunctionSymbol { IsBuiltIn: false } && s.Name == name;
    private static string CalleeName(SyntaxNode call) => call.GetChildren().FirstOrDefault() is { } callee
        ? DescendantsAndSelf(callee).OfType<SyntaxToken>().LastOrDefault(t => t.Kind == SyntaxKind.IdentifierToken)?.Text ?? "" : "";
    private static int OpenParenthesisPosition(SyntaxNode n) => n.GetChildren().OfType<SyntaxToken>().First(t => t.Kind == SyntaxKind.OpenParenthesisToken).Span.Start;
    private static bool EnclosesCaret(SyntaxNode n, int p) { var open = OpenParenthesisPosition(n); var close = n.GetChildren().OfType<SyntaxToken>().LastOrDefault(t => t.Kind == SyntaxKind.CloseParenthesisToken); return open < p && (close is null || close.IsMissing || p <= close.Span.Start); }
    private static bool InTriviaOrString(SemanticModel model, int p) { var token=model.FindToken(p==model.SyntaxTree.Text.Length&&p>0?p-1:p,true); return token is not null && (token.Kind==SyntaxKind.StringLiteralToken&&token.Span.Start<p&&p<token.Span.End || token.LeadingTrivia.Concat(token.TrailingTrivia).Any(t=>t.Span.Start<=p&&p<=t.Span.End&&t.Kind is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia or SyntaxKind.DocumentationCommentTrivia)); }
    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode n) { yield return n; foreach (var c in n.GetChildren()) foreach (var d in DescendantsAndSelf(c)) yield return d; }
}
