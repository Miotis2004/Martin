using System.Collections.Immutable;
using Martin.Compiler.Symbols;

namespace Martin.Compiler.Generics;

/// <summary>Describes where an exact generic-inference equation originated.</summary>
public enum InferenceSource
{
    Argument,
    ExpectedType,
    Receiver
}

/// <summary>An equation between a declaration signature type and an observed type.</summary>
public readonly record struct InferenceEquation(
    TypeSymbol ParameterType,
    TypeSymbol ObservedType,
    InferenceSource Source = InferenceSource.Argument);

/// <summary>All distinct exact candidates collected for one conflicting parameter.</summary>
public sealed record InferenceConflict(
    TypeParameterSymbol Parameter,
    ImmutableArray<TypeSymbol> Candidates);

/// <summary>The deterministic outcome of one generic-inference operation.</summary>
public sealed class GenericInferenceResult
{
    internal GenericInferenceResult(
        ImmutableArray<TypeParameterSymbol> parameters,
        ImmutableArray<TypeSymbol> typeArguments,
        ImmutableDictionary<TypeParameterSymbol, TypeSymbol> substitutions,
        ImmutableArray<InferenceConflict> conflicts,
        ImmutableArray<TypeParameterSymbol> unresolvedParameters)
    {
        TypeParameters = parameters;
        TypeArguments = typeArguments;
        Substitutions = substitutions;
        Conflicts = conflicts;
        UnresolvedParameters = unresolvedParameters;
    }

    public ImmutableArray<TypeParameterSymbol> TypeParameters { get; }
    /// <summary>Ordered by type-parameter ordinal; empty when inference failed.</summary>
    public ImmutableArray<TypeSymbol> TypeArguments { get; }
    public ImmutableDictionary<TypeParameterSymbol, TypeSymbol> Substitutions { get; }
    public ImmutableArray<InferenceConflict> Conflicts { get; }
    public ImmutableArray<TypeParameterSymbol> UnresolvedParameters { get; }
    public bool Succeeded => Conflicts.IsEmpty && UnresolvedParameters.IsEmpty;

    /// <summary>Produces stable, actionable descriptions suitable for binder diagnostics.</summary>
    public ImmutableArray<string> FailureMessages =>
        Conflicts.Select(conflict =>
                $"Conflicting types were inferred for '{conflict.Parameter.Name}': {string.Join(", ", conflict.Candidates.Select(type => type.Name))}.")
            .Concat(UnresolvedParameters.Select(parameter =>
                $"Type parameter '{parameter.Name}' could not be inferred; specify explicit type arguments."))
            .ToImmutableArray();
}

/// <summary>
/// Collects exact candidates from direct, optional, and matching constructed type
/// positions. It is stateless and safe to reuse across compilations and threads.
/// </summary>
public sealed class GenericInferenceEngine
{
    public GenericInferenceResult Infer(
        ImmutableArray<TypeParameterSymbol> typeParameters,
        IEnumerable<InferenceEquation> equations,
        CancellationToken cancellationToken = default)
    {
        if (typeParameters.IsDefault)
            throw new ArgumentException("Type parameters must be initialized.", nameof(typeParameters));
        ArgumentNullException.ThrowIfNull(equations);

        cancellationToken.ThrowIfCancellationRequested();
        var ordered = typeParameters.OrderBy(parameter => parameter.Ordinal).ToImmutableArray();
        if (ordered.Distinct().Count() != ordered.Length ||
            ordered.Select(parameter => parameter.Ordinal).Distinct().Count() != ordered.Length)
            throw new ArgumentException("Type parameters must be distinct and have unique ordinals.", nameof(typeParameters));

        var candidates = ordered.ToDictionary(
            parameter => parameter,
            _ => new List<(TypeIdentity Identity, TypeSymbol Type)>());

        foreach (var equation in equations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(equation.ParameterType);
            ArgumentNullException.ThrowIfNull(equation.ObservedType);
            Collect(equation.ParameterType, equation.ObservedType, candidates, cancellationToken);
        }

        var substitutions = ImmutableDictionary.CreateBuilder<TypeParameterSymbol, TypeSymbol>();
        var conflicts = ImmutableArray.CreateBuilder<InferenceConflict>();
        var unresolved = ImmutableArray.CreateBuilder<TypeParameterSymbol>();
        foreach (var parameter in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = candidates[parameter];
            if (values.Count == 0)
                unresolved.Add(parameter);
            else if (values.Count > 1)
                conflicts.Add(new InferenceConflict(parameter, values.Select(value => value.Type).ToImmutableArray()));
            else
                substitutions.Add(parameter, values[0].Type);
        }

        var successful = conflicts.Count == 0 && unresolved.Count == 0;
        var arguments = successful
            ? ordered.Select(parameter => substitutions[parameter]).ToImmutableArray()
            : ImmutableArray<TypeSymbol>.Empty;
        return new GenericInferenceResult(ordered, arguments, substitutions.ToImmutable(),
            conflicts.ToImmutable(), unresolved.ToImmutable());
    }

    private static void Collect(
        TypeSymbol pattern,
        TypeSymbol observed,
        Dictionary<TypeParameterSymbol, List<(TypeIdentity Identity, TypeSymbol Type)>> candidates,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (pattern is TypeParameterSymbol parameter && candidates.TryGetValue(parameter, out var parameterCandidates))
        {
            var identity = TypeIdentity.Create(observed);
            if (!parameterCandidates.Any(candidate => candidate.Identity.Equals(identity)))
                parameterCandidates.Add((identity, observed));
            return;
        }

        if (pattern is OptionalTypeSymbol patternOptional && observed is OptionalTypeSymbol observedOptional)
        {
            Collect(patternOptional.ElementType, observedOptional.ElementType, candidates, cancellationToken);
            return;
        }

        if (pattern is ConstructedTypeSymbol patternConstructed &&
            observed is ConstructedTypeSymbol observedConstructed &&
            SymbolIdentity.Create(patternConstructed.GenericDefinition)
                .Equals(SymbolIdentity.Create(observedConstructed.GenericDefinition)) &&
            patternConstructed.TypeArguments.Length == observedConstructed.TypeArguments.Length)
        {
            for (var index = 0; index < patternConstructed.TypeArguments.Length; index++)
                Collect(patternConstructed.TypeArguments[index], observedConstructed.TypeArguments[index],
                    candidates, cancellationToken);
        }
    }
}
