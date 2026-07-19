using System.Collections.Immutable;
using Martin.Compiler.Generics;

namespace Martin.Compiler.Symbols;

/// <summary>Recursively replaces type parameters in types and callable signatures.</summary>
public sealed class TypeSubstitution
{
    public const int MaximumDepth = 256;
    private readonly GenericTypeFactory _typeFactory;
    private readonly CancellationToken _cancellationToken;

    public TypeSubstitution(
        ImmutableDictionary<TypeParameterSymbol, TypeSymbol> mappings,
        GenericTypeFactory typeFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        ArgumentNullException.ThrowIfNull(typeFactory);
        Mappings = mappings;
        _typeFactory = typeFactory;
        _cancellationToken = cancellationToken;
    }

    public ImmutableDictionary<TypeParameterSymbol, TypeSymbol> Mappings { get; }

    public TypeSymbol Substitute(TypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Substitute(type, new HashSet<TypeParameterSymbol>(), 0);
    }

    private TypeSymbol Substitute(TypeSymbol type, HashSet<TypeParameterSymbol> activeMappings, int depth)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (depth > MaximumDepth)
            throw new InvalidOperationException($"Generic substitution exceeded the supported nesting limit of {MaximumDepth}.");
        if (ReferenceEquals(type, TypeSymbol.Error))
            return type;

        if (type is TypeParameterSymbol parameter && Mappings.TryGetValue(parameter, out var replacement))
        {
            // A self- or mutually-referential recovery mapping must remain open rather than recurse forever.
            if (!activeMappings.Add(parameter))
                return parameter;
            var result = Substitute(replacement, activeMappings, depth + 1);
            activeMappings.Remove(parameter);
            return result;
        }

        if (type is OptionalTypeSymbol optional)
        {
            var element = Substitute(optional.ElementType, activeMappings, depth + 1);
            return ReferenceEquals(element, optional.ElementType) ? optional : new OptionalTypeSymbol(element);
        }

        if (type is ConstructedTypeSymbol constructed)
        {
            var arguments = constructed.TypeArguments
                                .Select(argument => Substitute(argument, activeMappings, depth + 1))
                                .ToImmutableArray();
            if (arguments.Any(argument => ReferenceEquals(argument, TypeSymbol.Error)))
                return TypeSymbol.Error;
            if (Enumerable.SequenceEqual<TypeSymbol>(
                    arguments,
                    constructed.TypeArguments,
                    ReferenceEqualityComparer.Instance))
                return constructed;
            return _typeFactory.Construct(constructed.GenericDefinition, arguments, _cancellationToken);
        }

        return type;
    }

    public GenericConstraint Substitute(GenericConstraint constraint) => constraint switch {
        // Protocol constraints currently contain no substitutable type positions.
        ProtocolConstraint protocol => protocol,
        _ => constraint
    };

    public ParameterSymbol Substitute(ParameterSymbol parameter) => new(
        parameter.Name, parameter.Label, parameter.Ordinal, Substitute(parameter.Type), parameter.Locations,
        parameter.IsVariadic, parameter.HasDefaultValue);

    public PropertySymbol Substitute(PropertySymbol property, TypeSymbol containingType) => new(
        property.Name, containingType, Substitute(property.Type), property.IsReadOnly,
        property.DefaultValueSyntax, property.Locations, property.OriginalDefinition);

    public MethodSymbol Substitute(MethodSymbol method, TypeSymbol containingType) => new(
        method.Name, containingType, method.Parameters.Select(Substitute).ToImmutableArray(),
        Substitute(method.ReturnType), method.Declaration, method.IsMutating, method.Locations,
        method.IsThrowing, SubstituteNullable(method.ErrorType), method.TypeParameters,
        method.GenericRequirements.Select(Substitute).ToImmutableArray(), method.OriginalDefinition);

    /// <summary>Substitutes all type-bearing protocol method metadata, including its error type.</summary>
    public ProtocolMethodRequirementSymbol Substitute(ProtocolMethodRequirementSymbol requirement) => new(
        requirement.Name, requirement.ContainingProtocol,
        requirement.Parameters.Select(Substitute).ToImmutableArray(), Substitute(requirement.ReturnType),
        requirement.IsMutating, requirement.IsThrowing, SubstituteNullable(requirement.ErrorType),
        requirement.TypeParameters, requirement.GenericRequirements.Select(Substitute).ToImmutableArray(),
        requirement.StableId, requirement.Locations, requirement.IsStatic);

    public InitializerSymbol Substitute(InitializerSymbol initializer, TypeSymbol containingType) => new(
        containingType, initializer.Parameters.Select(Substitute).ToImmutableArray(), initializer.Declaration,
        initializer.IsSynthesized, initializer.Locations, initializer.IsThrowing,
        SubstituteNullable(initializer.ErrorType), initializer.OriginalDefinition);

    public EnumCaseSymbol Substitute(EnumCaseSymbol enumCase, TypeSymbol containingType) => new(
        enumCase.Name, containingType, enumCase.AssociatedValues.Select(Substitute).ToImmutableArray(),
        enumCase.Locations, enumCase.OriginalDefinition);

    public FunctionSymbol Substitute(FunctionSymbol function) => new(
        function.Name, function.Parameters.Select(Substitute).ToImmutableArray(), Substitute(function.ReturnType),
        function.Declaration, function.IsBuiltIn, function.Locations, function.TypeParameters,
        function.IsThrowing, SubstituteNullable(function.ErrorType), function.OriginalDefinition);

    private TypeSymbol? SubstituteNullable(TypeSymbol? type) => type is null ? null : Substitute(type);
}
