using System.Collections.Immutable;
using Martin.Compiler.Symbols;

namespace Martin.Compiler.Analysis.Protocols;

public enum ConformanceMismatchKind
{
    MemberKind,
    ParameterCount,
    ParameterLabel,
    ParameterType,
    ReturnType,
    Mutation,
    Throwing,
    ErrorType,
    GenericMetadata,
    PropertyType,
    PropertyReadability,
    PropertyWritability
}

public sealed record ConformanceMismatch(ConformanceMismatchKind Kind, MemberSymbol Candidate, int ParameterIndex = -1);

public sealed record ProtocolRequirementMatch(MemberSymbol? Witness, ImmutableArray<MemberSymbol> AmbiguousWitnesses, ConformanceMismatch? Mismatch)
{
    public bool IsSuccess => Witness is not null;
    public bool IsAmbiguous => !AmbiguousWitnesses.IsDefaultOrEmpty;
}

/// <summary>Matches the complete Martin signature. It deliberately contains no backend policy.</summary>
public static class ProtocolRequirementMatcher
{
    public static ProtocolRequirementMatch Match(ProtocolRequirementSymbol requirement, IEnumerable<MemberSymbol> members, bool containingTypeIsStruct)
    {
        var named = members.Where(member => member.Name == requirement.Name).ToImmutableArray();
        if (named.IsEmpty)
            return new(null, [], null);

        var evaluated = named.Select(member => (member, mismatch: Compare(requirement, member, containingTypeIsStruct))).ToImmutableArray();
        var exact = evaluated.Where(result => result.mismatch is null).Select(result => result.member).ToImmutableArray();
        return exact.Length switch
        {
            1 => new(exact[0], [], null),
            > 1 => new(null, exact, null),
            _ => new(null, [], evaluated.OrderBy(result => Rank(result.mismatch!.Kind)).First().mismatch)
        };
    }

    static ConformanceMismatch? Compare(ProtocolRequirementSymbol requirement, MemberSymbol candidate, bool containingTypeIsStruct)
    {
        if (requirement is ProtocolPropertyRequirementSymbol property)
        {
            if (candidate is not PropertySymbol witness)
                return new(ConformanceMismatchKind.MemberKind, candidate);
            if (!SameType(witness.Type, property.PropertyType))
                return new(ConformanceMismatchKind.PropertyType, candidate);
            if (property.RequiresGetter && !witness.IsStored)
                return new(ConformanceMismatchKind.PropertyReadability, candidate);
            if (property.RequiresSetter && witness.IsReadOnly)
                return new(ConformanceMismatchKind.PropertyWritability, candidate);
            return null;
        }

        var method = (ProtocolMethodRequirementSymbol)requirement;
        if (candidate is not MethodSymbol witnessMethod)
            return new(ConformanceMismatchKind.MemberKind, candidate);
        if (witnessMethod.Parameters.Length != method.Parameters.Length)
            return new(ConformanceMismatchKind.ParameterCount, candidate);
        for (var index = 0; index < method.Parameters.Length; index++)
        {
            if (witnessMethod.Parameters[index].Label != method.Parameters[index].Label)
                return new(ConformanceMismatchKind.ParameterLabel, candidate, index);
            if (!SameType(witnessMethod.Parameters[index].Type, method.Parameters[index].Type))
                return new(ConformanceMismatchKind.ParameterType, candidate, index);
        }
        if (!SameType(witnessMethod.ReturnType, method.ReturnType))
            return new(ConformanceMismatchKind.ReturnType, candidate);
        if (containingTypeIsStruct && witnessMethod.IsMutating != method.IsMutating)
            return new(ConformanceMismatchKind.Mutation, candidate);
        if (witnessMethod.IsThrowing != method.IsThrowing)
            return new(ConformanceMismatchKind.Throwing, candidate);
        if (method.IsThrowing && !SameType(witnessMethod.ErrorType, method.ErrorType))
            return new(ConformanceMismatchKind.ErrorType, candidate);
        if (!SameGenericMetadata(witnessMethod, method))
            return new(ConformanceMismatchKind.GenericMetadata, candidate);
        return null;
    }

    static bool SameGenericMetadata(MethodSymbol witness, ProtocolMethodRequirementSymbol requirement)
    {
        if (witness.TypeParameters.Length != requirement.TypeParameters.Length || witness.GenericRequirements.Length != requirement.GenericRequirements.Length)
            return false;
        return witness.TypeParameters.Zip(requirement.TypeParameters).All(pair => pair.First.Ordinal == pair.Second.Ordinal)
            && witness.GenericRequirements.Zip(requirement.GenericRequirements).All(pair => ConstraintId(pair.First) == ConstraintId(pair.Second));
    }

    static string ConstraintId(GenericConstraint constraint) => constraint switch
    {
        ProtocolConstraint protocol => "protocol:" + protocol.Protocol.Name,
        _ => constraint.GetType().FullName ?? constraint.GetType().Name
    };

    static bool SameType(TypeSymbol? left, TypeSymbol? right) =>
        ReferenceEquals(left, right) ||
        left is OptionalTypeSymbol leftOptional && right is OptionalTypeSymbol rightOptional && SameType(leftOptional.ElementType, rightOptional.ElementType) ||
        left is ConstructedTypeSymbol leftConstructed && right is ConstructedTypeSymbol rightConstructed &&
        ReferenceEquals(leftConstructed.GenericDefinition, rightConstructed.GenericDefinition) &&
        leftConstructed.TypeArguments.Length == rightConstructed.TypeArguments.Length &&
        leftConstructed.TypeArguments.Zip(rightConstructed.TypeArguments).All(pair => SameType(pair.First, pair.Second)) ||
        left is TypeParameterSymbol leftParameter && right is TypeParameterSymbol rightParameter && leftParameter.Ordinal == rightParameter.Ordinal;

    static int Rank(ConformanceMismatchKind kind) => kind switch
    {
        ConformanceMismatchKind.MemberKind => 100,
        ConformanceMismatchKind.ParameterCount => 90,
        _ => 0
    };
}
