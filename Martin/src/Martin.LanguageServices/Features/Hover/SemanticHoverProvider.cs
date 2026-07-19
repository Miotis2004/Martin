using System.Collections.Immutable;
using System.Text;
using Martin.Compiler;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Martin.Compiler.Text;

namespace Martin.LanguageServices;

internal static class SemanticHoverProvider
{
    static readonly ImmutableDictionary<string, string> BuiltInDocumentation = new Dictionary<string, string>(StringComparer.Ordinal) {
        ["print"] = "Writes a value to standard output.",
        ["readLine"] = "Reads one line from standard input.",
        ["argumentCount"] = "The number of command-line arguments.",
        ["argument"] = "Returns a command-line argument by index.",
        ["writeError"] = "Writes text to standard error.",
        ["exit"] = "Terminates the process with an exit code.",
        ["Int"] = "A signed integer value.",
        ["Double"] = "A double-precision floating-point value.",
        ["String"] = "A sequence of text characters.",
        ["Bool"] = "A Boolean truth value.",
        ["Void"] = "The absence of a return value.",
        ["Nil"] = "The nil value."
    }
                                                                                   .ToImmutableDictionary(StringComparer.Ordinal);

    public static HoverInfo? Get(LanguageProjectSnapshot project, ProjectAnalysis analysis, LanguageDocumentSnapshot document, int position, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (position < 0 || position > document.Text.Length)
            return null;
        if (!analysis.SemanticModels.TryGetValue(document.Id, out var model))
            return null;
        var token = model.FindToken(position);
        if (token is null && position > 0)
            token = model.FindToken(position - 1);
        if (token is null)
            return null;

        if (token.Kind is Martin.Compiler.Syntax.SyntaxKind.TryKeyword or Martin.Compiler.Syntax.SyntaxKind.ThrowKeyword or Martin.Compiler.Syntax.SyntaxKind.CatchKeyword)
        {
            var node = model.FindNode(token.Span) ?? model.FindNode(token.Span.Start);
            var info = node is null ? null : model.GetTypedErrorInfo(node);
            if (info is null && node is not null)
                info = Descendants(node).Select(model.GetTypedErrorInfo).FirstOrDefault(i => i is not null);
            if (info is not null)
            {
                var errorType = info.DeclaredErrorType ?? info.Effect.ErrorType;
                var errorMarkdown = new StringBuilder("```martin\n").Append(token.Text).Append(errorType is null ? string.Empty : $" {Type(errorType)}").Append("\n```\n\n").Append("**typed error effect:** ").Append(info.Effect.CanThrow ? $"`throws {Type(errorType ?? TypeSymbol.Error)}`" : "none");
                if (info.IsAcknowledged)
                    errorMarkdown.Append("  \n**Acknowledged by:** `try`");
                if (info.IsCaught)
                    errorMarkdown.Append("  \n**Caught:** yes");
                if (info.IsPropagated)
                    errorMarkdown.Append("  \n**Propagated:** yes");
                return new HoverInfo(token.Span, errorMarkdown.ToString());
            }
        }

        if (token.Kind != Martin.Compiler.Syntax.SyntaxKind.IdentifierToken)
            return null;

        var occurrence = model.GetSymbolReferences()
                             .Where(reference => Contains(reference.Location.Span, position) || position == reference.Location.Span.End)
                             .OrderBy(reference => reference.Location.Span.Length)
                             .FirstOrDefault();
        var symbol = occurrence?.Symbol ?? model.GetSymbolInfo(token);
        if (symbol is null)
            return null;

        var signature = FormatSignature(symbol);
        var markdown = new StringBuilder("```martin\n").Append(signature).Append("\n```\n\n").Append("**symbol kind:** ").Append(KindName(symbol));
        var generic = model.GetGenericInfo(occurrence?.Syntax ?? token);
        if (generic is not null && !generic.TypeArguments.IsDefaultOrEmpty)
        {
            markdown.Append("  \n**Type arguments").Append(generic.TypeArgumentsWereInferred ? " (inferred)" : "").Append(":** `").Append(string.Join(", ", generic.TypeArguments.Select(Type))).Append('`');
            if (!generic.ConstraintResults.IsDefaultOrEmpty)
                markdown.Append("  \n**Constraints:** ")
                    .Append(generic.ConstraintResults.All(result => result.Succeeded) ? "satisfied" : "not satisfied");
        }
        var containing = Containing(symbol);
        if (containing is not null)
            markdown.Append("  \n**Containing type:** `").Append(containing).Append('`');

        if (symbol is ProtocolTypeSymbol protocol)
        {
            var conformingTypes = analysis.Phase13.Conformances.Where(c => ReferenceEquals(c.Protocol, protocol))
                                      .Select(c => c.Type.Name)
                                      .Distinct(StringComparer.Ordinal)
                                      .OrderBy(n => n, StringComparer.Ordinal)
                                      .ToArray();
            if (conformingTypes.Length > 0)
                markdown.Append("  \n**Conforming types:** ").Append(string.Join(", ", conformingTypes.Select(n => $"`{n}`")));
        }
        if (symbol is ProtocolRequirementSymbol requirement)
        {
            var witnesses = analysis.Phase13.Conformances.Where(c => ReferenceEquals(c.Protocol, requirement.ContainingProtocol))
                                .Select(c => c.Witnesses.TryGetValue(requirement, out var witness) ? $"{c.Type.Name}.{witness.Name}" : null)
                                .OfType<string>()
                                .OrderBy(n => n, StringComparer.Ordinal)
                                .ToArray();
            if (witnesses.Length > 0)
                markdown.Append("  \n**Witnesses:** ").Append(string.Join(", ", witnesses.Select(n => $"`{n}`")));
        }
        if (symbol is MemberSymbol witness)
        {
            var requirements = analysis.Phase13.Conformances.SelectMany(c =>
                                                                            c.RequirementsByWitness.TryGetValue(witness, out var values) ? values : [])
                                   .Distinct()
                                   .ToArray();
            if (requirements.Length > 0)
                markdown.Append("  \n**Witness for:** ").Append(string.Join(", ", requirements.Select(r => $"`{r.ContainingProtocol.Name}.{r.Name}`")));
        }

        var documentation = GetDocumentation(project, symbol);
        if (documentation is not null)
            markdown.Append("\n\n---\n\n").Append(documentation);
        return new HoverInfo(token.Span, markdown.ToString());
    }

    static IEnumerable<SyntaxNode> Descendants(SyntaxNode n) => n.GetChildren().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    static bool Contains(TextSpan span, int position) => span.Start <= position && position < span.End;

    static string? GetDocumentation(LanguageProjectSnapshot project, Symbol symbol)
    {
        if (symbol.DeclarationLocation is { FilePath : {} path } location)
        {
            var document = project.Documents.FirstOrDefault(d => PathEquals(d.FilePath, path));
            if (document is not null)
            {
                var lines = DocumentationLines(document.Text, location.Span.Start);
                if (lines.Count > 0)
                    return new MarkdownDocumentationRenderer().Render(new DocumentationCommentParser().Parse(lines));
            }
        }
        return BuiltInDocumentation.GetValueOrDefault(symbol.Name);
    }

    static List<string> DocumentationLines(string text, int declarationPosition)
    {
        var before = text[..Math.Clamp(declarationPosition, 0, text.Length)].Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var result = new List<string>();
        for (var index = before.Length - 2; index >= 0; index--)
        {
            var line = before[index].TrimStart();
            if (!line.StartsWith("///", StringComparison.Ordinal))
                break;
            result.Insert(0, line);
        }
        return result;
    }

    static string FormatSignature(Symbol symbol) => symbol switch {
        FunctionSymbol f => $"func {f.Name}{Generics(f.TypeParameters)}{Parameters(f.Parameters)}{Throws(f.IsThrowing, f.ErrorType)} -> {Type(f.ReturnType)}",
        MethodSymbol m => $"{(m.IsMutating ? "mutating " : string.Empty)}func {m.Name}{Generics(m.TypeParameters)}{Parameters(m.Parameters)}{Throws(m.IsThrowing, m.ErrorType)} -> {Type(m.ReturnType)}",
        InitializerSymbol i => $"init{Parameters(i.Parameters)}{Throws(i.IsThrowing, i.ErrorType)}",
        PropertySymbol p => $"{(p.IsReadOnly ? "let" : "var")} {p.Name}: {Type(p.Type)}",
        VariableSymbol v => $"{(v.IsReadOnly ? "let" : "var")} {v.Name}: {Type(v.Type)}",
        StructTypeSymbol t => $"struct {t.Name}{Generics(t.TypeParameters)}{Constraints(t.TypeParameters)}",
        ClassTypeSymbol t => $"class {t.Name}{Generics(t.TypeParameters)}{Constraints(t.TypeParameters)}",
        EnumTypeSymbol t => $"enum {t.Name}{Generics(t.TypeParameters)}{Constraints(t.TypeParameters)}",
        ProtocolTypeSymbol t => $"protocol {t.Name}",
        ProtocolMethodRequirementSymbol m => $"{(m.IsMutating ? "mutating " : string.Empty)}func {m.Name}{Generics(m.TypeParameters)}{Parameters(m.Parameters)}{Throws(m.IsThrowing, m.ErrorType)} -> {Type(m.ReturnType)}",
        ProtocolPropertyRequirementSymbol p => $"{(p.RequiresSetter ? "var" : "let")} {p.Name}: {Type(p.PropertyType)}",
        TypeParameterSymbol t => t.Name + Constraint(t),
        ConstructedTypeSymbol t => Type(t),
        TypeSymbol t => t.Name,
        EnumCaseSymbol c => $"case {c.Name}{Parameters(c.AssociatedValues)}",
        _ => symbol.Name
    };

    static string Parameters(ImmutableArray<ParameterSymbol> parameters) => "(" + string.Join(", ", parameters.Select(p => $"{p.Label ?? "_"} {p.Name}: {Type(p.Type)}")) + ")";
    static string Generics(ImmutableArray<TypeParameterSymbol> parameters) => parameters.Length == 0 ? string.Empty : "<" + string.Join(", ", parameters.Select(p => p.Name)) + ">";
    static string Constraints(ImmutableArray<TypeParameterSymbol> parameters) => parameters.Length == 0 ? string.Empty : string.Concat(parameters.Select(Constraint));
    static string Constraint(TypeParameterSymbol parameter) => parameter.Constraints.Length == 0 ? string.Empty : " where " + parameter.Name + ": " + string.Join(" & ", parameter.Constraints.OfType<ProtocolConstraint>().Select(c => c.Protocol.Name));
    static string Throws(bool throwing, TypeSymbol? error) => throwing ? $" throws {Type(error ?? TypeSymbol.Error)}" : string.Empty;
    static string Type(TypeSymbol type) => type switch { OptionalTypeSymbol o => Type(o.ElementType) + "?", ConstructedTypeSymbol c => c.GenericDefinition.Name + "<" + string.Join(", ", c.TypeArguments.Select(Type)) + ">",
                                                         _ => type.Name };
    static string KindName(Symbol symbol) => symbol.Kind.ToString();
    static string? Containing(Symbol symbol) => symbol switch { MemberSymbol member => Type(member.ContainingType), SelfParameterSymbol self => self.ContainingType.Name, TypeParameterSymbol typeParameter => typeParameter.ContainingSymbol.Name,
                                                                _ => null };
    static bool PathEquals(string left, string right) => StringComparerForPaths.Equals(Path.GetFullPath(left), Path.GetFullPath(right));
    static StringComparer StringComparerForPaths => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
