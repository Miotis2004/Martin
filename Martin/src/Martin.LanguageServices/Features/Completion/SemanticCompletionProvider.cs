using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Martin.Compiler.Text;

namespace Martin.LanguageServices;

internal static class SemanticCompletionProvider
{
    private static readonly string[] TopLevelKeywords = ["func", "struct", "class", "enum", "protocol", "let", "var"];
    private static readonly string[] StatementKeywords = ["let", "var", "if", "while", "return", "throw", "switch", "do", "catch"];
    private static readonly string[] ExpressionKeywords = ["self", "true", "false", "nil", "try"];

    public static ImmutableArray<CompletionItem> Complete(LanguageWorkspaceSnapshot workspace, LanguageProjectSnapshot project,
                                                          ProjectAnalysis analysis, LanguageDocumentSnapshot document, int position, int maximumResults, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        position = Math.Clamp(position, 0, document.Text.Length);
        if (!analysis.SemanticModels.TryGetValue(document.Id, out var model))
            return [];
        if (IsInCommentOrString(model, position))
            return [];

        var replacement = ReplacementSpan(document.Text, position);
        var prefix = document.Text.Substring(replacement.Start, replacement.Length);
        var context = DetectContext(model, document.Text, position, replacement.Start);
        var expectedType = model.GetExpectedType(position);
        var identity = new SymbolIdentityFactory(project.Id);
        var candidates = new List<Candidate>();

        if (TryProtocolConformanceContext(model, document, position, replacement, prefix, candidates, identity))
            return Finish(candidates, maximumResults);

        AddMissingRequirementStubs(model, analysis, document, position, replacement, prefix, candidates);

        // A type-parameter constraint is a protocol position, rather than an
        // ordinary type position.  Keeping this semantic filter here prevents
        // classes and structs from being offered after `T:` while the parser is
        // still recovering an incomplete declaration.
        if (IsThrowsTypeContext(model, position))
        {
            foreach (var errorType in model.LookupSymbols(position).OfType<TypeSymbol>().Where(candidateType => IsErrorType(candidateType, analysis)))
                AddSymbol(candidates, errorType, replacement, prefix, null, identity);
            return Finish(candidates, maximumResults);
        }

        if (IsConstraintContext(model, position))
        {
            foreach (var protocol in model.LookupSymbols(position).OfType<ProtocolTypeSymbol>())
                AddSymbol(candidates, protocol, replacement, prefix, null, identity);
            return Finish(candidates, maximumResults);
        }

        // Patterns have a deliberately restricted namespace.  In particular, a
        // leading dot is not ordinary member access: it means a case of the
        // switch input type.  Handle this before general expression completion so
        // malformed/in-progress case clauses still receive useful results.
        if (TryCatchPatternContext(model, analysis, document, position, replacement, prefix, candidates, cancellationToken) ||
            TryPatternContext(model, analysis, document, position, replacement, prefix, candidates, cancellationToken))
            return Finish(candidates, maximumResults);

        IEnumerable<Symbol> symbols = context.MemberReceiver is
        {
        }
        receiver
            ? model.GetTypeInfo(receiver) is
        {
        }
        receiverType ? model.LookupMembers(receiverType, position) : [] : model.LookupSymbols(position);
        foreach (var symbol in symbols)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ValidInContext(symbol, context))
                continue;
            AddSymbol(candidates, symbol, replacement, prefix, expectedType, identity);
        }

        if (context.ArgumentList is {} invocation)
            AddParameterLabels(candidates, model, invocation, document.Text, position, replacement, prefix);

        if (context.MemberReceiver is null)
        {
            var keywords = context.IsType ? Array.Empty<string>() : context.IsTopLevel ? TopLevelKeywords
                                                                : context.IsExpression ? ExpressionKeywords.Concat(StatementKeywords)
                                                                                       : StatementKeywords;
            foreach (var keyword in keywords.Distinct(StringComparer.Ordinal))
            {
                if (keyword == "throw" && !CanThrowHere(model, position))
                    continue;
                candidates.Add(Create(keyword, keyword, CompletionItemKind.Keyword, replacement, prefix, 80, keyword));
            }

            // Optional types and snippets are secondary additions, not the semantic symbol source.
            if (context.IsType)
                foreach (var typeSymbol in model.LookupSymbols(position).OfType<TypeSymbol>().Where(t => t != TypeSymbol.Void && t != TypeSymbol.Nil))
                    candidates.Add(Create(typeSymbol.Name + "?", typeSymbol.Name + "?", CompletionItemKind.Type, replacement, prefix, 65, "optional type"));
            if (!context.IsType)
                candidates.Add(Create("if let", "if let ${1:name} = ${2:optional} {\n    $0\n}", CompletionItemKind.Snippet,
                                      replacement, prefix, 100, "optional binding", InsertTextFormat.Snippet));
        }

        return Finish(candidates, maximumResults);
    }

    private static bool TryProtocolConformanceContext(SemanticModel model, LanguageDocumentSnapshot document, int position,
                                                      TextSpan replacement, string prefix, List<Candidate> candidates, SymbolIdentityFactory identity)
    {
        var declaration = Descendants(model.SyntaxTree.Root).FirstOrDefault(node => (node.Kind is SyntaxKind.StructDeclaration or SyntaxKind.ClassDeclaration or SyntaxKind.EnumDeclaration) && node.FullSpan.Start <= position && position <= node.FullSpan.End);
        if (declaration is null)
            return false;
        var openBrace = declaration.GetChildren().OfType<SyntaxToken>().FirstOrDefault(t => t.Kind == SyntaxKind.OpenBraceToken);
        var colon = declaration.GetChildren().SelectMany(Descendants).OfType<SyntaxToken>().FirstOrDefault(t => t.Kind == SyntaxKind.ColonToken && (openBrace is null || t.Span.Start < openBrace.Span.Start));
        if (colon is null || position <= colon.Span.End || openBrace is not null && position > openBrace.Span.Start)
            return false;

        var listed = declaration.GetChildren().Where(n => n.Kind == SyntaxKind.ProtocolConformanceClause).SelectMany(Descendants).OfType<SyntaxToken>().Where(t => t.Kind == SyntaxKind.IdentifierToken && t.Span.End <= position).Select(t => t.Text).Where(name => !name.Equals(prefix, StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        foreach (var protocol in model.LookupSymbols(position).OfType<ProtocolTypeSymbol>().Where(p => !listed.Contains(p.Name)).DistinctBy(p => p.Name))
            AddSymbol(candidates, protocol, replacement, prefix, null, identity);
        return true;
    }

    private static void AddMissingRequirementStubs(SemanticModel model, ProjectAnalysis analysis,
                                                   LanguageDocumentSnapshot document, int position, TextSpan replacement, string prefix, List<Candidate> candidates)
    {
        var declaration = Descendants(model.SyntaxTree.Root).FirstOrDefault(node => (node.Kind is SyntaxKind.StructDeclaration or SyntaxKind.ClassDeclaration or SyntaxKind.EnumDeclaration) && node.FullSpan.Start <= position && position <= node.FullSpan.End);
        if (declaration is null)
            return;
        var type = model.GetDeclaredSymbol(declaration) as NamedTypeSymbol;
        if (type is null)
            return;
        foreach (var conformance in analysis.Phase13.ConformanceAttempts.Where(c => ReferenceEquals(c.Type, type)))
            foreach (var requirement in conformance.Protocol.Requirements.Where(r => !conformance.Witnesses.ContainsKey(r)))
            {
                var insertion = RequirementStub(requirement);
                var kind = requirement is ProtocolMethodRequirementSymbol ? CompletionItemKind.Method : CompletionItemKind.Property;
                candidates.Add(Create(requirement.Name, insertion, kind, replacement, prefix, 1,
                                      $"missing requirement of {conformance.Protocol.Name}: {RequirementSignature(requirement)}", InsertTextFormat.Snippet));
            }
    }

    private static string RequirementStub(ProtocolRequirementSymbol requirement) => requirement switch {
        ProtocolMethodRequirementSymbol method =>
            $"{(method.IsMutating ? "mutating " : string.Empty)}func {method.Name}{TypeParameters(method.TypeParameters)}" +
            $"({string.Join(", ", method.Parameters.Select(Parameter))})" +
            $"{(method.IsThrowing ? " throws " + TypeName(method.ErrorType ?? TypeSymbol.Error) : string.Empty)}" +
            $" -> {TypeName(method.ReturnType)} {{\n    ${{0:<#code#>}}\n}}",
        ProtocolPropertyRequirementSymbol property =>
            $"{(property.RequiresSetter ? "var" : "let")} {property.Name}: {TypeName(property.PropertyType)}",
        _ => requirement.Name
    };

    private static string RequirementSignature(ProtocolRequirementSymbol requirement) => requirement switch {
        ProtocolMethodRequirementSymbol method => $"func {method.Name}{TypeParameters(method.TypeParameters)}({string.Join(", ", method.Parameters.Select(Parameter))}) -> {TypeName(method.ReturnType)}",
        ProtocolPropertyRequirementSymbol property => $"{(property.RequiresSetter ? "var" : "let")} {property.Name}: {TypeName(property.PropertyType)}",
        _ => requirement.Name
    };
    private static string Parameter(ParameterSymbol parameter) => $"{parameter.Label ?? "_"} {parameter.Name}: {TypeName(parameter.Type)}";
    private static string TypeParameters(ImmutableArray<TypeParameterSymbol> parameters) => parameters.IsDefaultOrEmpty ? string.Empty : $"<{string.Join(", ", parameters.Select(p => p.Name))}>";
    private static string TypeName(TypeSymbol type) => type switch {
        OptionalTypeSymbol optional => TypeName(optional.ElementType) + "?",
        ConstructedTypeSymbol constructed => constructed.GenericDefinition.Name + "<" + string.Join(", ", constructed.TypeArguments.Select(TypeName)) + ">",
        _ => type.Name
    };

    private static ImmutableArray<CompletionItem> Finish(List<Candidate> candidates, int maximumResults) => candidates
                                                                                                                .GroupBy(c => (c.Item.Label, c.Item.TextEdit.NewText), StringTupleComparer.Instance)
                                                                                                                .Select(g => g.MinBy(c => c.Rank)!)
                                                                                                                .OrderBy(c => c.Rank)
                                                                                                                .ThenBy(c => c.Item.SortText, StringComparer.Ordinal)
                                                                                                                .ThenBy(c => c.Item.Label, StringComparer.Ordinal)
                                                                                                                .Take(Math.Clamp(maximumResults, 1, 1000))
                                                                                                                .Select(c => c.Item)
                                                                                                                .ToImmutableArray();

    private static bool TryCatchPatternContext(SemanticModel model, ProjectAnalysis analysis, LanguageDocumentSnapshot document,
                                               int position, TextSpan replacement, string prefix, List<Candidate> candidates, CancellationToken cancellationToken)
    {
        var catchNode = Descendants(model.SyntaxTree.Root).FirstOrDefault(n => n.Kind == SyntaxKind.CatchClause && n.FullSpan.Start <= position && position <= n.FullSpan.End);
        if (catchNode is null)
            return false;
        var before = document.Text[catchNode.FullSpan.Start..position];
        var catchOffset = before.IndexOf("catch", StringComparison.Ordinal);
        var braceOffset = before.IndexOf('{');
        if (catchOffset < 0 || braceOffset >= 0)
            return false;
        var fragment = before[(catchOffset + 5)..].TrimStart();
        if (fragment.Length > 0 && !fragment.StartsWith('.') && !fragment.StartsWith('_') && !fragment.StartsWith("let", StringComparison.Ordinal) && !IsIdentifier(fragment[0]))
            return false;

        cancellationToken.ThrowIfCancellationRequested();
        var errorType = model.GetCatchInfo(catchNode)?.DeclaredErrorType ?? model.GetCatchInfo(catchNode)?.Effect.ErrorType;
        if (errorType is null)
            return false;
        foreach (var enumCase in Cases(errorType))
            AddCatchPattern(candidates, document.Text, replacement, prefix, enumCase, 1, $"catch {enumCase.ContainingType.Name}.{enumCase.Name}");
        if (!Cases(errorType).Any() || errorType is TypeParameterSymbol)
            candidates.Add(Create("_", "_", CompletionItemKind.Keyword, replacement, prefix, 10, "wildcard catch"));
        return true;
    }

    private static void AddCatchPattern(List<Candidate> candidates, string text, TextSpan replacement, string prefix,
                                        EnumCaseSymbol enumCase, int rank, string detail)
    {
        var insertion = CaseSnippet(enumCase);
        if (replacement.Start > 0 && text[replacement.Start - 1] == '.' && insertion.StartsWith('.'))
            insertion = insertion[1..];
        candidates.Add(Create('.' + enumCase.Name, insertion, CompletionItemKind.EnumCase, replacement, prefix, rank, detail,
                              enumCase.HasAssociatedValues ? InsertTextFormat.Snippet : InsertTextFormat.PlainText));
    }

    private static bool TryPatternContext(SemanticModel model, ProjectAnalysis analysis, LanguageDocumentSnapshot document,
                                          int position, TextSpan replacement, string prefix, List<Candidate> candidates, CancellationToken cancellationToken)
    {
        var switchNode = Descendants(model.SyntaxTree.Root).OfType<StatementSyntax>().Where(n => n.Kind == SyntaxKind.SwitchStatement && n.FullSpan.Start <= position && position <= n.FullSpan.End).OrderBy(n => n.FullSpan.Length).FirstOrDefault();
        if (switchNode is null)
            return false;
        var before = document.Text[switchNode.FullSpan.Start..position];
        var caseOffset = before.LastIndexOf("case", StringComparison.Ordinal);
        if (caseOffset < 0 || before.LastIndexOf(':') > caseOffset + 4)
            return false;
        var fragment = before[(caseOffset + 4)..].TrimStart();
        if (fragment.Length > 0 && !fragment.StartsWith('.') && !fragment.StartsWith("nil", StringComparison.Ordinal) &&
            !fragment.StartsWith("true", StringComparison.Ordinal) && !fragment.StartsWith("false", StringComparison.Ordinal) &&
            !fragment.StartsWith('_') && !IsIdentifier(fragment[0]))
            return false;

        cancellationToken.ThrowIfCancellationRequested();
        var input = switchNode.GetChildren().OfType<ExpressionSyntax>().FirstOrDefault();
        var inputType = input is null ? null : model.GetTypeInfo(input);
        if (inputType is null)
            return false;
        var indexed = analysis.Phase13.Switches.FirstOrDefault(s => s.DocumentId == document.Id && s.Span == switchNode.Span);
        var missing = indexed?.Analysis.MissingWitnesses ?? [];

        void AddPattern(string label, string insertion, CompletionItemKind kind, int rank, string detail, bool snippet = false) => candidates.Add(Create(label, replacement.Start > 0 && document.Text[replacement.Start - 1] == '.' && insertion.StartsWith('.') ? insertion[1..] : insertion,
                                                                                                                                                         kind, replacement, prefix, rank, detail,
                                                                                                                                                         snippet ? InsertTextFormat.Snippet : InsertTextFormat.PlainText));

        foreach (var witness in missing)
        {
            var label = MissingPatternWitnessDiagnosticRenderer.Render(witness);
            AddPattern(label, MissingPatternWitnessSnippetRenderer.Render(witness),
                       witness.Constructor?.Name.StartsWith('.') == true ? CompletionItemKind.EnumCase : CompletionItemKind.Keyword,
                       0, "missing switch case", !witness.Arguments.IsEmpty);
        }

        if (inputType is OptionalTypeSymbol)
        {
            AddPattern(".some", ".some(let ${1:value})", CompletionItemKind.EnumCase, 15, "optional some pattern", true);
            AddPattern("nil", "nil", CompletionItemKind.Keyword, 16, "optional nil pattern");
        }
        else if (inputType == TypeSymbol.Bool)
        {
            AddPattern("true", "true", CompletionItemKind.Keyword, 15, "Boolean pattern");
            AddPattern("false", "false", CompletionItemKind.Keyword, 15, "Boolean pattern");
        }
        else
        {
            foreach (var enumCase in Cases(inputType))
                AddPattern('.' + enumCase.Name, CaseSnippet(enumCase), CompletionItemKind.EnumCase,
                           missing.Any(w => w.Constructor?.EnumCase is not null && ReferenceEquals(w.Constructor.EnumCase, enumCase)) ? 1 : 20,
                           $"case {enumCase.ContainingType.Name}.{enumCase.Name}", enumCase.HasAssociatedValues);
            if (!Cases(inputType).Any())
                AddPattern("_", "_", CompletionItemKind.Keyword, 15, "wildcard pattern");
        }
        return true;
    }

    private static IEnumerable<EnumCaseSymbol> Cases(TypeSymbol type) => type switch {
        EnumTypeSymbol e => e.Cases,
        ConstructedTypeSymbol c => c.Cases,
        _ => []
    };

    private static string CaseSnippet(EnumCaseSymbol c) => '.' + c.Name + (c.HasAssociatedValues ? "(" + string.Join(", ", c.AssociatedValues.Select((p, i) => $"let ${{{i + 1}:{BindingName(p, c.AssociatedValues.Length)}}}")) + ")" : string.Empty);

    private static string BindingName(ParameterSymbol parameter, int count) =>
        string.IsNullOrWhiteSpace(parameter.Name) || char.IsUpper(parameter.Name[0])
            ? count == 1 ? "value" : $"value{parameter.Ordinal + 1}"
            : parameter.Name;

    private static class MissingPatternWitnessSnippetRenderer
    {
        public static string Render(MissingPatternWitness witness)
        {
            var placeholder = 0;
            return Render(witness, ref placeholder);
        }

        private static string Render(MissingPatternWitness witness, ref int placeholder)
        {
            if (witness.Constructor is null)
            {
                placeholder++;
                var name = placeholder == 1 ? "value" : $"value{placeholder}";
                return $"let ${{{placeholder}:{name}}}";
            }

            if (witness.Arguments.IsEmpty)
                return witness.Constructor.Name;
            var arguments = ImmutableArray.CreateBuilder<string>(witness.Arguments.Length);
            foreach (var argument in witness.Arguments)
                arguments.Add(Render(argument, ref placeholder));
            return witness.Constructor.Name + "(" + string.Join(", ", arguments) + ")";
        }
    }

    private static void AddSymbol(List<Candidate> result, Symbol symbol, TextSpan span, string prefix, TypeSymbol? expected,
                                  SymbolIdentityFactory identity)
    {
        var kind = Kind(symbol);
        var insertion = symbol.Name;
        if (symbol is InitializerSymbol)
            insertion = "init";
        var symbolType = ResultType(symbol);
        var expectedMatch = expected is not null && symbolType is not null && symbolType.Name == expected.Name;
        var locality = symbol.Kind switch { SymbolKind.LocalVariable => 10, SymbolKind.Parameter => 12, SymbolKind.Property or SymbolKind.Method => 18,
                                            _ => 30 };
        var prefixRank = symbol.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? 0 : 20;
        var rank = prefixRank + locality - (expectedMatch ? 8 : 0);
        var signature = identity.DisplaySignature(symbol);
        var item = Create(symbol.Name, insertion, kind, span, prefix, rank, signature).Item with { SymbolId = identity.Create(symbol, declarationDiscriminator: symbol.DeclarationLocation?.Span.Start.ToString()), IsRecommended = expectedMatch, CommitCharacters = symbol is FunctionSymbol or MethodSymbol or InitializerSymbol ? ['('] : symbol is MemberSymbol ? ['.']
                                                                                                                                                                                                                                                                                                                                                                     : [] };
        result.Add(new(item, rank));
    }

    private static void AddParameterLabels(List<Candidate> result, SemanticModel model, SyntaxNode invocation, string text,
                                           int position, TextSpan replacement, string prefix)
    {
        var used = invocation.GetChildren().Where(n => n.Kind == SyntaxKind.Argument).Select(n => n.GetChildren().OfType<SyntaxToken>().FirstOrDefault()).Where(t => t?.Kind == SyntaxKind.IdentifierToken && t.Span.Start < position).Select(t => t!.Text).ToHashSet(StringComparer.Ordinal);
        var argumentIndex = text[invocation.FullSpan.Start..Math.Min(position, invocation.FullSpan.End)].Count(c => c == ',');
        foreach (var callable in model.GetInvocationCandidates(invocation))
        {
            var parameters = callable switch {
                FunctionSymbol f => f.Parameters,
                MethodSymbol m => m.Parameters,
                InitializerSymbol i => i.Parameters,
                EnumCaseSymbol e => e.AssociatedValues,
                _ => []
            };
            foreach (var parameter in parameters.Where(p => p.Ordinal >= argumentIndex && p.Label is not null && p.Label != "_" && !used.Contains(p.Label)))
                result.Add(Create(parameter.Label!, parameter.Label + ": ", CompletionItemKind.Parameter, replacement, prefix,
                                  parameter.Ordinal == argumentIndex ? 2 : 8, $"parameter: {parameter.Type.Name}"));
        }
    }

    private static Context DetectContext(SemanticModel model, string text, int position, int fragmentStart)
    {
        var node = model.FindNode(position) ?? model.FindNode(Math.Max(0, position - 1));
        var chain = Ancestors(model.SyntaxTree.Root, node).ToArray();
        var type = chain.Any(n => n.Kind is SyntaxKind.TypeClause or SyntaxKind.ReturnTypeClause or SyntaxKind.TypeArgumentList or SyntaxKind.TypeParameterList);
        var top = !chain.Any(n => n.Kind == SyntaxKind.BlockStatement);
        var argument = chain.FirstOrDefault(n => n.Kind == SyntaxKind.CallExpression && n.FullSpan.Start <= position && position <= n.FullSpan.End);
        ExpressionSyntax? receiver = null;
        var dot = fragmentStart - 1;
        if (dot >= 0 && text[dot] == '.')
        {
            var member = chain.FirstOrDefault(n => n.Kind == SyntaxKind.MemberAccessExpression) ??
                         Descendants(model.SyntaxTree.Root).FirstOrDefault(n => n.Kind == SyntaxKind.MemberAccessExpression && n.FullSpan.Start <= dot && dot <= n.FullSpan.End);
            receiver = member?.GetChildren().OfType<ExpressionSyntax>().FirstOrDefault();
        }
        return new(type, top, !type && !top, receiver, argument);
    }

    private static bool IsThrowsTypeContext(SemanticModel model, int position)
    {
        var node = model.FindNode(position) ?? model.FindNode(Math.Max(0, position - 1));
        var throws = Ancestors(model.SyntaxTree.Root, node).FirstOrDefault(n => n.Kind == SyntaxKind.ThrowsClause) ??
                     Descendants(model.SyntaxTree.Root).FirstOrDefault(n => n.Kind == SyntaxKind.ThrowsClause && n.FullSpan.Start <= position && position <= n.FullSpan.End);
        if (throws is null)
            return false;
        var keyword = throws.GetChildren().OfType<SyntaxToken>().FirstOrDefault(t => t.Kind == SyntaxKind.ThrowsKeyword);
        return keyword is not null && keyword.Span.End <= position;
    }

    private static bool IsErrorType(TypeSymbol type, ProjectAnalysis analysis)
    {
        if (type is TypeParameterSymbol parameter)
            return parameter.Constraints.OfType<ProtocolConstraint>().Any(c => c.Protocol.Name == "Error");
        var named = type as NamedTypeSymbol ?? (type as ConstructedTypeSymbol)?.GenericDefinition;
        return named is not null && analysis.Phase13.Conformances.Any(c => ReferenceEquals(c.Type, named) && c.Protocol.Name == "Error");
    }

    private static bool CanThrowHere(SemanticModel model, int position) => model.GetEnclosingSymbol(position) switch {
        FunctionSymbol f => f.IsThrowing,
        MethodSymbol m => m.IsThrowing,
        InitializerSymbol i => i.IsThrowing,
        _ => false
    };

    private static bool IsConstraintContext(SemanticModel model, int position)
    {
        var node = model.FindNode(position) ?? model.FindNode(Math.Max(0, position - 1));
        var parameter = Ancestors(model.SyntaxTree.Root, node).FirstOrDefault(n => n.Kind == SyntaxKind.TypeParameter);
        if (parameter is null)
            return false;
        var colon = parameter.GetChildren().OfType<SyntaxToken>().FirstOrDefault(t => t.Kind == SyntaxKind.ColonToken);
        return colon is not null && colon.Span.End <= position;
    }

    private static bool IsInCommentOrString(SemanticModel model, int position)
    {
        var token = model.FindToken(position == model.SyntaxTree.Text.Length && position > 0 ? position - 1 : position, true);
        if (token is null)
            return false;
        if (token.Kind == SyntaxKind.StringLiteralToken && token.Span.Start < position && position < token.Span.End)
            return true;
        return token.LeadingTrivia.Concat(token.TrailingTrivia).Any(t => t.Span.Start <= position && position <= t.Span.End && t.Kind is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia or SyntaxKind.DocumentationCommentTrivia);
    }

    private static TextSpan ReplacementSpan(string text, int position)
    {
        var start = position;
        while (start > 0 && IsIdentifier(text[start - 1]))
            start--;
        var end = position;
        while (end < text.Length && IsIdentifier(text[end]))
            end++;
        return TextSpan.FromBounds(start, end);
    }
    private static bool IsIdentifier(char c) => char.IsLetterOrDigit(c) || c == '_';
    private static bool ValidInContext(Symbol symbol, Context context) => context.IsType ? symbol is TypeSymbol : context.MemberReceiver is not null ? symbol is MemberSymbol
                                                                                                                                                     : symbol is not InitializerSymbol;
    private static TypeSymbol? ResultType(Symbol s) => s switch {
        VariableSymbol v => v.Type,
        PropertySymbol p => p.Type,
        FunctionSymbol f => f.ReturnType,
        MethodSymbol m => m.ReturnType,
        EnumCaseSymbol e => e.ContainingType,
        _ => s as TypeSymbol
    };
    private static CompletionItemKind Kind(Symbol s) => s switch {
        TypeParameterSymbol => CompletionItemKind.TypeParameter,
        TypeSymbol => CompletionItemKind.Type,
        FunctionSymbol => CompletionItemKind.Function,
        MethodSymbol => CompletionItemKind.Method,
        PropertySymbol => CompletionItemKind.Property,
        ParameterSymbol => CompletionItemKind.Parameter,
        EnumCaseSymbol => CompletionItemKind.EnumCase,
        InitializerSymbol => CompletionItemKind.Initializer,
        _ => CompletionItemKind.Variable
    };
    private static Candidate Create(string label, string insertion, CompletionItemKind kind, TextSpan span, string prefix, int rank,
                                    string? detail, InsertTextFormat format = InsertTextFormat.PlainText) => new(new CompletionItem(label, insertion, kind.ToString(), detail) with { TextEdit = new(span, insertion), FilterText = label, SortText = $"{rank:D3}:{label}", InsertTextFormat = format }, rank);
    private static IEnumerable<SyntaxNode> Descendants(SyntaxNode n) => n.GetChildren().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    private static IEnumerable<SyntaxNode> Ancestors(SyntaxNode root, SyntaxNode? target)
    {
        if (target is null)
            yield break;
        var path = new List<SyntaxNode>();
        if (!Find(root, target, path))
            yield break;
        for (var i = path.Count - 1; i >= 0; i--)
            yield return path[i];
        static bool Find(SyntaxNode n, SyntaxNode target, List<SyntaxNode> path)
        {
            path.Add(n);
            if (ReferenceEquals(n, target))
                return true;
            foreach (var c in n.GetChildren())
                if (Find(c, target, path))
                    return true;
            path.RemoveAt(path.Count - 1);
            return false;
        }
    }
    private sealed record Candidate(CompletionItem Item, int Rank);
    private sealed record Context(bool IsType, bool IsTopLevel, bool IsExpression, ExpressionSyntax? MemberReceiver, SyntaxNode? ArgumentList);
    private sealed class StringTupleComparer : IEqualityComparer<(string, string)>
    {
        public static StringTupleComparer Instance { get; } = new();
        public bool Equals((string, string)x, (string, string)y) => StringComparer.Ordinal.Equals(x.Item1, y.Item1) && StringComparer.Ordinal.Equals(x.Item2, y.Item2);
        public int GetHashCode((string, string)obj) => HashCode.Combine(StringComparer.Ordinal.GetHashCode(obj.Item1), StringComparer.Ordinal.GetHashCode(obj.Item2));
    }
}
