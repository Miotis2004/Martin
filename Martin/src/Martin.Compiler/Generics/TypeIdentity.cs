using System.Collections.Immutable;
using Martin.Compiler.Symbols;

namespace Martin.Compiler.Generics;

public enum TypeIdentityKind { Intrinsic, Definition, TypeParameter, Optional, Constructed }

/// <summary>A structural, ordered identity for every compiler type form.</summary>
public sealed class TypeIdentity : IEquatable<TypeIdentity>
{
    private TypeIdentity(TypeIdentityKind kind, string name, SymbolIdentity? declaration, int ordinal, ImmutableArray<TypeIdentity> arguments)
    {
        Kind = kind; Name = name; Declaration = declaration; Ordinal = ordinal; TypeArguments = arguments.IsDefault ? [] : arguments;
    }
    public TypeIdentityKind Kind { get; }
    public string Name { get; }
    public SymbolIdentity? Declaration { get; }
    public int Ordinal { get; }
    public ImmutableArray<TypeIdentity> TypeArguments { get; }

    public static TypeIdentity Create(TypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type switch
        {
            ConstructedTypeSymbol c => new(TypeIdentityKind.Constructed, c.GenericDefinition.Name, SymbolIdentity.Create(c.GenericDefinition), -1, c.TypeArguments.Select(Create).ToImmutableArray()),
            OptionalTypeSymbol o => new(TypeIdentityKind.Optional, "?", null, -1, [Create(o.ElementType)]),
            TypeParameterSymbol p => new(TypeIdentityKind.TypeParameter, string.Empty, SymbolIdentity.Create(p.ContainingSymbol), p.Ordinal, []),
            NamedTypeSymbol n => new(TypeIdentityKind.Definition, n.Name, SymbolIdentity.Create(n), -1, []),
            _ => new(TypeIdentityKind.Intrinsic, type.Name, null, -1, [])
        };
    }

    internal string StableKey => $"{(int)Kind}:{Name.Length}#{Name}:{Declaration?.StableKey ?? string.Empty}:{Ordinal}:" +
        string.Concat(TypeArguments.Select(a => $"{a.StableKey.Length}#{a.StableKey}"));
    public bool Equals(TypeIdentity? other) => other is not null && Kind == other.Kind && Name == other.Name && Nullable.Equals(Declaration, other.Declaration) && Ordinal == other.Ordinal && TypeArguments.SequenceEqual(other.TypeArguments);
    public override bool Equals(object? obj) => obj is TypeIdentity other && Equals(other);
    public override int GetHashCode() { unchecked { var hash = 2166136261u; foreach (var c in StableKey) hash = (hash ^ c) * 16777619; return (int)hash; } }
    public override string ToString() => StableKey;
}
