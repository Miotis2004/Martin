using System.Collections.Immutable;
using Martin.Compiler.Symbols;

namespace Martin.Compiler.Generics;

/// <summary>Validates generic arguments against declared Martin constraints.</summary>
public sealed class GenericConstraintValidator
{
    private readonly ImmutableArray<ProtocolConformance> _conformances;

    public GenericConstraintValidator(IEnumerable<ProtocolConformance> conformances)
    {
        ArgumentNullException.ThrowIfNull(conformances);
        _conformances = conformances.ToImmutableArray();
    }

    public ConstraintValidationResult Validate(TypeParameterSymbol parameter, TypeSymbol argument,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(argument);

        var proofs = ImmutableArray.CreateBuilder<ProtocolConstraintProof>();
        var failures = ImmutableArray.CreateBuilder<GenericConstraint>();
        foreach (var constraint in parameter.Constraints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (constraint is not ProtocolConstraint protocolConstraint)
            {
                failures.Add(constraint);
                continue;
            }

            var proof = FindProof(argument, protocolConstraint.Protocol);
            if (proof is null)
                failures.Add(constraint);
            else
                proofs.Add(proof);
        }

        return new ConstraintValidationResult(parameter, argument, proofs.ToImmutable(), failures.ToImmutable());
    }

    private ProtocolConstraintProof? FindProof(TypeSymbol argument, ProtocolTypeSymbol protocol)
    {
        if (argument is TypeParameterSymbol openParameter &&
            openParameter.Constraints.OfType<ProtocolConstraint>().Any(c => ReferenceEquals(c.Protocol, protocol)))
            return new ProtocolConstraintProof(protocol, null, openParameter);

        var definition = argument switch
        {
            NamedTypeSymbol named => named,
            ConstructedTypeSymbol constructed => constructed.GenericDefinition,
            _ => null
        };
        if (definition is null)
            return null;

        var conformance = _conformances.FirstOrDefault(c =>
            ReferenceEquals(c.Type, definition) && ReferenceEquals(c.Protocol, protocol));
        return conformance is null ? null : new ProtocolConstraintProof(protocol, conformance, null);
    }
}

public sealed record ProtocolConstraintProof(
    ProtocolTypeSymbol Protocol,
    ProtocolConformance? Conformance,
    TypeParameterSymbol? OpenParameter);

public sealed record ConstraintValidationResult(
    TypeParameterSymbol Parameter,
    TypeSymbol Argument,
    ImmutableArray<ProtocolConstraintProof> Proofs,
    ImmutableArray<GenericConstraint> UnsatisfiedConstraints)
{
    public bool Succeeded => UnsatisfiedConstraints.IsEmpty;
}
