using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Martin.Compiler.Symbols;

namespace Martin.LanguageServices;

/// <summary>Creates stable IDs from semantic signature data; never from object identity or hash codes.</summary>
public sealed class SymbolIdentityFactory(ProjectId projectId)
{
    public SymbolId Create(Symbol symbol, SymbolId? containingSymbolId = null, string? declarationDiscriminator = null)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        var signature = IdentitySignature(symbol, containingSymbolId, declarationDiscriminator);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(signature));
        return new SymbolId("mrt:" + Convert.ToHexString(digest).ToLowerInvariant());
    }

    public string DisplaySignature(Symbol symbol) => symbol switch {
        FunctionSymbol f => Callable(f.Name, f.TypeParameters.Length, f.Parameters, f.ReturnType, f.IsThrowing, f.ErrorType),
        MethodSymbol m => Callable(m.Name, m.TypeParameters.Length, m.Parameters, m.ReturnType, m.IsThrowing, m.ErrorType),
        InitializerSymbol i => Callable("init", 0, i.Parameters, i.ContainingType, i.IsThrowing, i.ErrorType),
        ProtocolMethodRequirementSymbol m => Callable(m.Name, m.TypeParameters.Length, m.Parameters, m.ReturnType, m.IsThrowing, m.ErrorType),
        ProtocolPropertyRequirementSymbol p => $"{p.Name}: {TypeName(p.Type)}",
        PropertySymbol p => $"{p.Name}: {TypeName(p.Type)}",
        VariableSymbol v => $"{v.Name}: {TypeName(v.Type)}",
        NamedTypeSymbol t => t.Name + GenericSuffix(t.TypeParameters),
        ConstructedTypeSymbol t => TypeName(t),
        TypeParameterSymbol t => t.Name,
        EnumCaseSymbol c => c.Name + Parameters(c.AssociatedValues),
        _ => symbol.Name
    };

    string IdentitySignature(Symbol symbol, SymbolId? containing, string? discriminator)
    {
        var builtIn = IsBuiltIn(symbol);
        var scope = builtIn ? "builtin" : projectId.Value.ToString("N");
        var owner = containing?.Value ?? ContainingTypeSignature(symbol) ?? "root";
        var shape = symbol switch {
            FunctionSymbol f => Callable(f.Name, f.TypeParameters.Length, f.Parameters, f.ReturnType, f.IsThrowing, f.ErrorType),
            MethodSymbol m => Callable(m.Name, m.TypeParameters.Length, m.Parameters, m.ReturnType, m.IsThrowing, m.ErrorType),
            InitializerSymbol i => Callable("init", 0, i.Parameters, i.ContainingType, i.IsThrowing, i.ErrorType),
            ProtocolMethodRequirementSymbol m => Callable(m.Name, m.TypeParameters.Length, m.Parameters, m.ReturnType, m.IsThrowing, m.ErrorType),
            ProtocolPropertyRequirementSymbol p => $"{p.Name}:{TypeName(p.Type)}:{p.RequiresSetter}",
            PropertySymbol p => $"{p.Name}:{TypeName(p.Type)}:{p.IsReadOnly}",
            EnumCaseSymbol c => c.Name + Parameters(c.AssociatedValues),
            ParameterSymbol p => $"{p.Ordinal}:{p.Label ?? "_"}:{p.Name}:{TypeName(p.Type)}",
            TypeParameterSymbol t => $"{t.Ordinal}:{t.Name}",
            VariableSymbol v => $"{v.Name}:{TypeName(v.Type)}:{v.IsReadOnly}",
            ConstructedTypeSymbol t => TypeName(t),
            NamedTypeSymbol t => t.Name + GenericSuffix(t.TypeParameters),
            TypeSymbol t => TypeName(t),
            _ => symbol.Name
        };
        // Source discriminator is needed only for declarations whose semantic signature can repeat in a scope.
        var source = symbol is LocalVariableSymbol or ParameterSymbol or TypeParameterSymbol ? discriminator ?? string.Empty : string.Empty;
        return $"v1|{scope}|{symbol.Kind}|{owner}|{shape}|{source}";
    }

    static string? ContainingTypeSignature(Symbol symbol) => symbol switch {
        MemberSymbol m => TypeName(m.ContainingType),
        SelfParameterSymbol s => TypeName(s.ContainingType),
        TypeParameterSymbol t => $"{t.ContainingSymbol.Kind}:{t.ContainingSymbol.Name}",
        _ => null
    };

    static bool IsBuiltIn(Symbol symbol) => symbol is FunctionSymbol { IsBuiltIn : true } ||
                                            symbol is TypeSymbol and not NamedTypeSymbol and not TypeParameterSymbol and not ConstructedTypeSymbol && symbol.Locations.IsDefaultOrEmpty;

    static string Callable(string name, int arity, ImmutableArray<ParameterSymbol> parameters, TypeSymbol result, bool throwing, TypeSymbol? error) =>
        $"{name}`{arity}{Parameters(parameters)}->{TypeName(result)}" + (throwing ? $" throws {TypeName(error ?? TypeSymbol.Error)}" : string.Empty);

    static string Parameters(ImmutableArray<ParameterSymbol> parameters) =>
        "(" + string.Join(",", parameters.Select(p => $"{p.Label ?? "_"}:{TypeName(p.Type)}")) + ")";

    static string GenericSuffix(ImmutableArray<TypeParameterSymbol> parameters) => parameters.Length == 0 ? string.Empty : $"`{parameters.Length}";

    static string TypeName(TypeSymbol type) => type switch {
        OptionalTypeSymbol optional => TypeName(optional.ElementType) + "?",
        ConstructedTypeSymbol constructed => constructed.GenericDefinition.Name + "<" + string.Join(",", constructed.TypeArguments.Select(TypeName)) + ">",
        TypeParameterSymbol parameter => "!" + parameter.Ordinal,
        _ => type.Name
    };
}
