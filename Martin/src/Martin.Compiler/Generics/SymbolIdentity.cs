using Martin.Compiler.Symbols;

namespace Martin.Compiler.Generics;

/// <summary>Stable semantic identity for a declaration, without object identity or runtime hash codes.</summary>
public readonly record struct SymbolIdentity(SymbolKind Kind, string ContainingScope, string Name, int GenericArity, string DeclarationIdentity)
{
    public static SymbolIdentity Create(Symbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        var scope = symbol switch
        {
            TypeParameterSymbol parameter => Create(parameter.ContainingSymbol).StableKey,
            MemberSymbol member => TypeIdentity.Create(member.ContainingType).StableKey,
            _ => string.Empty
        };
        var arity = symbol switch
        {
            NamedTypeSymbol type => type.TypeParameters.Length,
            FunctionSymbol function => function.TypeParameters.Length,
            MethodSymbol method => method.TypeParameters.Length,
            _ => 0
        };
        return new(symbol.Kind, scope, symbol.Name, arity, GetDeclarationIdentity(symbol));
    }

    internal string StableKey => $"{(int)Kind}:{Escape(ContainingScope)}:{Escape(Name)}:{GenericArity}:{Escape(DeclarationIdentity)}";

    private static string GetDeclarationIdentity(Symbol symbol)
    {
        if (symbol is TypeParameterSymbol parameter)
            return $"parameter:{parameter.Ordinal}";
        var location = symbol.DeclarationLocation;
        if (location is null)
            return "intrinsic";
        var path = (location.Value.FilePath ?? string.Empty).Replace('\\', '/');
        return $"{path}:{location.Value.Span.Start}:{location.Value.Span.Length}";
    }

    private static string Escape(string value) => $"{value.Length}#{value}";
}
