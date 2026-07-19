using System.Collections.Immutable;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;

namespace Martin.Compiler.Binding;

/// <summary>The result of checking a switch pattern matrix.</summary>
public sealed record SwitchAnalysisResult(
    bool IsExhaustive,
    ImmutableArray<UnreachablePatternCase> UnreachableCases,
    ImmutableArray<MissingPatternWitness> MissingWitnesses,
    bool HasErrors = false)
{
    public static SwitchAnalysisResult Error { get; } = new(false, [], [], true);
}

/// <summary>A semantic value shape which is not covered by a switch pattern matrix.</summary>
public sealed record MissingPatternWitness(
    PatternConstructor? Constructor,
    ImmutableArray<MissingPatternWitness> Arguments,
    TypeSymbol Type)
{
    public ImmutableArray<MissingPatternWitness> Arguments { get; init; } =
        Arguments.IsDefault ? [] : Arguments;
}

/// <summary>Renders missing witnesses for human-readable diagnostics.</summary>
public static class MissingPatternWitnessDiagnosticRenderer
{
    public static string Render(MissingPatternWitness witness)
    {
        ArgumentNullException.ThrowIfNull(witness);
        if (witness.Constructor is null) return "_";
        var name = witness.Type is ConstructedTypeSymbol &&
                   witness.Constructor.Kind == PatternConstructorKind.EnumCase
            ? witness.Type.Name + witness.Constructor.Name
            : witness.Constructor.Name;
        return witness.Arguments.IsEmpty
            ? name
            : name + "(" + string.Join(", ", witness.Arguments.Select(Render)) + ")";
    }
}

/// <summary>Identifies a source case which cannot match a value not matched earlier.</summary>
public sealed record UnreachablePatternCase(int SourceOrdinal, TextLocation Location);

/// <summary>
/// Implements recursive pattern-matrix usefulness and exhaustiveness checking.
/// The implementation contains Martin types and patterns only and is therefore
/// shared by every backend.
/// </summary>
public sealed class PatternCoverageAnalyzer
{
    public const int DefaultMaximumWitnesses = 5;
    public const int DefaultMaximumUnreachableCases = 256;

    private readonly PatternMatrixLimits _limits;
    private readonly int _maximumWitnesses;
    private readonly int _maximumUnreachableCases;

    public PatternCoverageAnalyzer(
        int maximumWitnesses = DefaultMaximumWitnesses,
        PatternMatrixLimits? limits = null,
        int maximumUnreachableCases = DefaultMaximumUnreachableCases)
    {
        if (maximumWitnesses <= 0) throw new ArgumentOutOfRangeException(nameof(maximumWitnesses));
        if (maximumUnreachableCases <= 0) throw new ArgumentOutOfRangeException(nameof(maximumUnreachableCases));
        _maximumWitnesses = maximumWitnesses;
        _maximumUnreachableCases = maximumUnreachableCases;
        _limits = limits ?? PatternMatrixLimits.Default;
    }

    public SwitchAnalysisResult Analyze(
        TypeSymbol inputType,
        ImmutableArray<BoundPattern> patterns,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputType);
        patterns = patterns.IsDefault ? [] : patterns;
        var usefulRows = ImmutableArray.CreateBuilder<PatternMatrixRow>();
        var unreachable = ImmutableArray.CreateBuilder<UnreachablePatternCase>();

        for (var ordinal = 0; ordinal < patterns.Length; ordinal++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pattern = patterns[ordinal];
            var matrix = new PatternMatrix(usefulRows.ToImmutable(), _limits, cancellationToken);
            if (pattern.HasErrors)
                continue; // Invalid patterns neither prove coverage nor cause cascading reachability errors.
            if (FindWitness(matrix, [pattern], cancellationToken) is null)
            {
                if (unreachable.Count < _maximumUnreachableCases)
                    unreachable.Add(new(ordinal, pattern.Location));
            }
            else
                usefulRows.Add(new([pattern], ordinal));
        }

        var coverage = new PatternMatrix(usefulRows.ToImmutable(), _limits, cancellationToken);
        var missing = FindWitnesses(coverage, inputType, cancellationToken);
        return new(missing.IsEmpty, unreachable.ToImmutable(), missing);
    }

    private ImmutableArray<MissingPatternWitness> FindWitnesses(
        PatternMatrix matrix,
        TypeSymbol inputType,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<MissingPatternWitness>();
        foreach (var witness in EnumerateWitnesses(matrix, [inputType], cancellationToken))
        {
            result.Add(witness[0]);
            if (result.Count == _maximumWitnesses) break;
        }
        return result.ToImmutable();
    }

    private IEnumerable<ImmutableArray<MissingPatternWitness>> EnumerateWitnesses(
        PatternMatrix matrix,
        ImmutableArray<TypeSymbol> types,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (types.IsEmpty)
        {
            if (matrix.Rows.IsEmpty) yield return [];
            yield break;
        }

        var headType = types[0];
        var tailTypes = types.RemoveAt(0);
        var domain = PatternDomain.Create(headType);
        if (!domain.IsFinite)
        {
            foreach (var tail in EnumerateWitnesses(matrix.Default(cancellationToken), tailTypes, cancellationToken))
                yield return tail.Insert(0, new MissingPatternWitness(null, [], headType));
            yield break;
        }

        foreach (var constructor in domain.Constructors)
        {
            var specializedTypes = constructor.ArgumentTypes.AddRange(tailTypes);
            foreach (var specialized in EnumerateWitnesses(
                         matrix.Specialize(constructor, cancellationToken), specializedTypes, cancellationToken))
            {
                var arguments = specialized.Take(constructor.Arity).ToImmutableArray();
                var tail = specialized.RemoveRange(0, constructor.Arity);
                yield return tail.Insert(0, new MissingPatternWitness(constructor, arguments, headType));
            }
        }
    }

    private ImmutableArray<MissingPatternWitness>? FindWitness(
        PatternMatrix matrix,
        ImmutableArray<BoundPattern> candidate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (candidate.IsEmpty) return matrix.Rows.IsEmpty ? [] : null;

        var head = candidate[0];
        var tail = candidate.RemoveAt(0);
        if (TryConstructor(head, out var constructor, out var arguments))
        {
            var specialized = arguments.AddRange(tail);
            var found = FindWitness(matrix.Specialize(constructor, cancellationToken), specialized, cancellationToken);
            if (found is null) return null;
            var children = found.Value.Take(constructor.Arity).ToImmutableArray();
            return found.Value.RemoveRange(0, constructor.Arity).Insert(0, new MissingPatternWitness(constructor, children, head.InputType));
        }

        var domain = PatternDomain.Create(head.InputType);
        if (!domain.IsFinite)
        {
            var found = FindWitness(matrix.Default(cancellationToken), tail, cancellationToken);
            return found is null ? null : found.Value.Insert(0, new MissingPatternWitness(null, [], head.InputType));
        }

        foreach (var item in domain.Constructors)
        {
            var wildcardArguments = item.ArgumentTypes
                .Select(type => (BoundPattern)new BoundWildcardPattern(type, head.Location)).ToImmutableArray();
            var found = FindWitness(matrix.Specialize(item, cancellationToken), wildcardArguments.AddRange(tail), cancellationToken);
            if (found is null) continue;
            var children = found.Value.Take(item.Arity).ToImmutableArray();
            return found.Value.RemoveRange(0, item.Arity).Insert(0, new MissingPatternWitness(item, children, head.InputType));
        }
        return null;
    }

    private static bool TryConstructor(
        BoundPattern pattern,
        out PatternConstructor constructor,
        out ImmutableArray<BoundPattern> arguments)
    {
        (constructor, arguments) = pattern switch
        {
            BoundEnumCasePattern value => (PatternConstructor.ForEnumCase(value.Case), value.AssociatedPatterns),
            BoundOptionalSomePattern value => (PatternConstructor.OptionalSome(value.OptionalType.ElementType), [value.ValuePattern]),
            BoundNilPattern => (PatternConstructor.OptionalNone, []),
            BoundLiteralPattern { Value: bool value } => (PatternConstructor.Boolean(value), []),
            BoundLiteralPattern value => (PatternConstructor.Literal(value.Value), []),
            _ => (null!, []),
        };
        return constructor is not null;
    }

}
