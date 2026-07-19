using System.Collections.Immutable;
using Martin.Compiler.Symbols;

namespace Martin.Compiler.Generics;

/// <summary>A structural key formed from a generic definition and ordered semantic arguments.</summary>
public sealed class ConstructedTypeKey : IEquatable<ConstructedTypeKey>
{
    public ConstructedTypeKey(NamedTypeSymbol definition, ImmutableArray<TypeSymbol> arguments) : this(SymbolIdentity.Create(definition), arguments.Select(TypeIdentity.Create).ToImmutableArray()) { }
    public ConstructedTypeKey(SymbolIdentity definition, ImmutableArray<TypeIdentity> arguments) { GenericDefinition = definition; TypeArguments = arguments.IsDefault ? [] : arguments; }
    public SymbolIdentity GenericDefinition { get; }
    public ImmutableArray<TypeIdentity> TypeArguments { get; }
    public bool Equals(ConstructedTypeKey? other) => other is not null && GenericDefinition.Equals(other.GenericDefinition) && TypeArguments.SequenceEqual(other.TypeArguments);
    public override bool Equals(object? obj) => obj is ConstructedTypeKey other && Equals(other);
    public override int GetHashCode() { unchecked { var hash = 17; hash = hash * 31 + GenericDefinition.GetHashCode(); foreach (var argument in TypeArguments) hash = hash * 31 + argument.GetHashCode(); return hash; } }
}
