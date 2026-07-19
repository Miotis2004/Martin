using System.Collections.Concurrent;
using System.Collections.Immutable;
using Martin.Compiler.Symbols;

namespace Martin.Compiler.Generics;

/// <summary>Owns and canonicalizes constructed generic types for one compilation.</summary>
public sealed class GenericTypeFactory
{
    private readonly ConcurrentDictionary<ConstructedTypeKey, Construction> _constructions = new();

    /// <summary>The number of canonical constructions currently owned by this compilation.</summary>
    public int ConstructionCount => _constructions.Count;

    public ConstructedTypeSymbol Construct(NamedTypeSymbol genericDefinition, ImmutableArray<TypeSymbol> typeArguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(genericDefinition);
        if (!genericDefinition.IsGeneric)
            throw new ArgumentException($"Type '{genericDefinition.Name}' is not generic.", nameof(genericDefinition));
        if (typeArguments.IsDefault || typeArguments.Length != genericDefinition.TypeParameters.Length)
            throw new ArgumentException($"Generic type '{genericDefinition.Name}' expects {genericDefinition.TypeParameters.Length} type arguments.", nameof(typeArguments));
        if (typeArguments.Any(argument => argument is null || ReferenceEquals(argument, TypeSymbol.Error)))
            throw new ArgumentException("Type arguments must be valid types.", nameof(typeArguments));

        var key = new ConstructedTypeKey(genericDefinition, typeArguments);
        var construction = _constructions.GetOrAdd(key, _ => new Construction(genericDefinition, typeArguments));
        return Complete(key, construction, cancellationToken);
    }

    private ConstructedTypeSymbol Complete(ConstructedTypeKey key, Construction construction, CancellationToken cancellationToken)
    {
        lock (construction.Gate)
        {
            while (construction.IsBuilding && construction.BuilderThreadId != Environment.CurrentManagedThreadId)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Monitor.Wait(construction.Gate, TimeSpan.FromMilliseconds(25));
            }
            if (construction.Failure is not null)
                throw new InvalidOperationException("Generic type construction failed.", construction.Failure);
            if (construction.IsComplete || construction.IsBuilding)
                return construction.Symbol;

            construction.IsBuilding = true;
            construction.BuilderThreadId = Environment.CurrentManagedThreadId;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var definition = construction.Symbol.GenericDefinition;
                var substitution = new TypeSubstitution(
                    definition.TypeParameters.Zip(construction.Symbol.TypeArguments)
                        .ToImmutableDictionary(pair => pair.First, pair => pair.Second),
                    this, cancellationToken);
                var members = definition.Members.Select(member =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return (MemberSymbol)(member switch
                    {
                        PropertySymbol property => substitution.Substitute(property, construction.Symbol),
                        MethodSymbol method => substitution.Substitute(method, construction.Symbol),
                        InitializerSymbol initializer => substitution.Substitute(initializer, construction.Symbol),
                        EnumCaseSymbol enumCase => substitution.Substitute(enumCase, construction.Symbol),
                        _ => member
                    });
                }).ToImmutableArray();
                construction.Symbol.CompleteMembers(members);
                construction.IsComplete = true;
                return construction.Symbol;
            }
            catch (Exception exception)
            {
                construction.Failure = exception;
                _constructions.TryRemove(key, out _);
                throw;
            }
            finally
            {
                construction.IsBuilding = false;
                construction.BuilderThreadId = 0;
                Monitor.PulseAll(construction.Gate);
            }
        }
    }

    private sealed class Construction(NamedTypeSymbol definition, ImmutableArray<TypeSymbol> arguments)
    {
        public object Gate { get; } = new();
        public ConstructedTypeSymbol Symbol { get; } = new(definition, arguments, []);
        public bool IsBuilding { get; set; }
        public bool IsComplete { get; set; }
        public int BuilderThreadId { get; set; }
        public Exception? Failure { get; set; }
    }
}
