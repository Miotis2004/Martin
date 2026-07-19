using System.Collections.Immutable;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Martin.Compiler.Text;

namespace Martin.Compiler.Generics;

internal sealed record GenericCallableConstruction(
    string Name,
    ImmutableArray<TypeParameterSymbol> Parameters,
    ImmutableArray<TypeSymbol> Arguments,
    TextLocation Location);

internal sealed record GenericTypeConstruction(
    NamedTypeSymbol Definition,
    ImmutableArray<TypeSymbol> Arguments,
    TextLocation Location);

/// <summary>Shared binding rules for a generic name in any type-syntax position.</summary>
internal static class GenericTypeBinding
{
    public static TypeSymbol Bind(
        GenericSyntaxNode syntax,
        SourceText text,
        Func<string, TypeSymbol?> lookup,
        Func<SyntaxNode, TypeSymbol> bindArgument,
        GenericTypeFactory factory,
        DiagnosticBag diagnostics,
        ICollection<GenericTypeConstruction> constructions)
    {
        var identifier = (SyntaxToken)syntax.Children[0];
        var definition = lookup(identifier.Text);
        if (definition is not NamedTypeSymbol named || !named.IsGeneric)
        {
            diagnostics.Report(new Diagnostic("MRT2185", DiagnosticSeverity.Error,
                $"Type '{identifier.Text}' is not generic.", identifier.Location(text)));
            return TypeSymbol.Error;
        }

        var argumentList = syntax.Children.OfType<GenericSyntaxNode>()
            .First(node => node.Kind == SyntaxKind.TypeArgumentList);
        var arguments = argumentList.Children
            .Where(IsTypeSyntax)
            .Select(bindArgument)
            .ToImmutableArray();
        if (arguments.Length != named.TypeParameters.Length)
        {
            diagnostics.Report(new Diagnostic("MRT2180", DiagnosticSeverity.Error,
                $"Generic type '{named.Name}' expects {named.TypeParameters.Length} type arguments, but {arguments.Length} were provided.",
                identifier.Location(text)));
            return TypeSymbol.Error;
        }

        // A nested argument has already produced its primary diagnostic. Avoid a
        // second construction diagnostic and never pass an error sentinel to the factory.
        if (arguments.Any(argument => ReferenceEquals(argument, TypeSymbol.Error)))
            return TypeSymbol.Error;

        constructions.Add(new GenericTypeConstruction(named, arguments, identifier.Location(text)));
        return factory.Construct(named, arguments);
    }

    private static bool IsTypeSyntax(SyntaxNode node) => node.Kind is
        SyntaxKind.TypeClause or SyntaxKind.OptionalType or SyntaxKind.GenericName;
}
