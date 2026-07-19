using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Martin.Compiler.Symbols;

namespace Martin.Compiler.Binding;

/// <summary>The semantic kind of a constructor used by pattern coverage analysis.</summary>
public enum PatternConstructorKind
{
    EnumCase,
    OptionalSome,
    OptionalNone,
    BooleanTrue,
    BooleanFalse,
    Literal,
}

/// <summary>A backend-neutral value constructor and the product space carried by it.</summary>
public sealed record PatternConstructor
{
    private PatternConstructor(
        PatternConstructorKind kind,
        string name,
        ImmutableArray<TypeSymbol> argumentTypes,
        object? value = null,
        EnumCaseSymbol? enumCase = null)
    {
        Kind = kind;
        Name = name;
        ArgumentTypes = argumentTypes.IsDefault ? [] : argumentTypes;
        Value = value;
        EnumCase = enumCase;
    }

    public PatternConstructorKind Kind { get; }
    public string Name { get; }
    public ImmutableArray<TypeSymbol> ArgumentTypes { get; }
    public int Arity => ArgumentTypes.Length;
    public object? Value { get; }
    public EnumCaseSymbol? EnumCase { get; }

    public static PatternConstructor ForEnumCase(EnumCaseSymbol @case) =>
        new(PatternConstructorKind.EnumCase, "." + @case.Name,
            @case.AssociatedValues.Select(parameter => parameter.Type).ToImmutableArray(), enumCase: @case);

    public static PatternConstructor OptionalSome(TypeSymbol elementType) =>
        new(PatternConstructorKind.OptionalSome, ".some", [elementType]);

    public static PatternConstructor OptionalNone { get; } =
        new(PatternConstructorKind.OptionalNone, "nil", []);

    public static PatternConstructor Boolean(bool value) => value
        ? new(PatternConstructorKind.BooleanTrue, "true", [], true)
        : new(PatternConstructorKind.BooleanFalse, "false", [], false);

    public static PatternConstructor Literal(object? value) =>
        new(PatternConstructorKind.Literal, FormatLiteral(value), [], value);

    public bool Matches(PatternConstructor other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Kind != other.Kind || Arity != other.Arity) return false;
        return Kind switch
        {
            PatternConstructorKind.EnumCase => ReferenceEquals(EnumCase, other.EnumCase),
            PatternConstructorKind.Literal => Equals(Value, other.Value),
            _ => true,
        };
    }

    private static string FormatLiteral(object? value) => value switch
    {
        string text => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)?.ToLowerInvariant() ?? "nil",
    };
}

/// <summary>Describes whether every constructor of a type can be enumerated.</summary>
public sealed class PatternDomain
{
    private PatternDomain(TypeSymbol type, bool isFinite, ImmutableArray<PatternConstructor> constructors)
    {
        Type = type;
        IsFinite = isFinite;
        Constructors = constructors;
    }

    public TypeSymbol Type { get; }
    public bool IsFinite { get; }
    public bool HasRemainder => !IsFinite;
    public ImmutableArray<PatternConstructor> Constructors { get; }

    public static PatternDomain Create(TypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type switch
        {
            EnumTypeSymbol enumType => new(type, true,
                enumType.Cases.Select(PatternConstructor.ForEnumCase).ToImmutableArray()),
            ConstructedTypeSymbol { GenericDefinition: EnumTypeSymbol } constructed => new(type, true,
                constructed.Cases.Select(PatternConstructor.ForEnumCase).ToImmutableArray()),
            OptionalTypeSymbol optional => new(type, true,
                [PatternConstructor.OptionalNone, PatternConstructor.OptionalSome(optional.ElementType)]),
            _ when type == TypeSymbol.Bool => new(type, true,
                [PatternConstructor.Boolean(false), PatternConstructor.Boolean(true)]),
            _ => new(type, false, []),
        };
    }
}

/// <summary>An immutable row in a pattern matrix. Patterns are stored left-to-right.</summary>
public sealed record PatternMatrixRow
{
    public PatternMatrixRow(ImmutableArray<BoundPattern> patterns, int sourceOrdinal)
    {
        Patterns = patterns.IsDefault ? [] : patterns;
        SourceOrdinal = sourceOrdinal;
    }

    public ImmutableArray<BoundPattern> Patterns { get; }
    public int SourceOrdinal { get; }
    public bool HasErrors => Patterns.Any(pattern => pattern.HasErrors);
}

public sealed record PatternMatrixLimits(int MaximumRows = 4096, int MaximumColumns = 256)
{
    public static PatternMatrixLimits Default { get; } = new();
}

/// <summary>Indicates that pattern analysis cannot continue within its configured capacity.</summary>
public sealed class PatternMatrixCapacityException(string message) : InvalidOperationException(message);

/// <summary>
/// Immutable pattern-matrix operations shared by usefulness and exhaustiveness analysis.
/// Error rows deliberately participate in neither specialization nor the default matrix,
/// so malformed source can never prove coverage.
/// </summary>
public sealed class PatternMatrix
{
    public PatternMatrix(
        ImmutableArray<PatternMatrixRow> rows,
        PatternMatrixLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        Limits = limits ?? PatternMatrixLimits.Default;
        Rows = rows.IsDefault ? [] : rows;
        Validate(Rows, Limits, cancellationToken);
    }

    public ImmutableArray<PatternMatrixRow> Rows { get; }
    public PatternMatrixLimits Limits { get; }
    public int ColumnCount => Rows.IsEmpty ? 0 : Rows[0].Patterns.Length;

    public PatternMatrix Specialize(PatternConstructor constructor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        var rows = ImmutableArray.CreateBuilder<PatternMatrixRow>();
        foreach (var row in Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.HasErrors || row.Patterns.IsEmpty) continue;
            var head = row.Patterns[0];
            var tail = row.Patterns.RemoveAt(0);
            if (TryGetConstructor(head, out var actual))
            {
                if (actual.Matches(constructor))
                    rows.Add(new PatternMatrixRow(GetArguments(head).AddRange(tail), row.SourceOrdinal));
            }
            else if (IsWildcard(head))
            {
                var wildcards = constructor.ArgumentTypes
                    .Select(type => (BoundPattern)new BoundWildcardPattern(type, head.Location))
                    .ToImmutableArray();
                rows.Add(new PatternMatrixRow(wildcards.AddRange(tail), row.SourceOrdinal));
            }
        }
        return new PatternMatrix(rows.ToImmutable(), Limits, cancellationToken);
    }

    public PatternMatrix Default(CancellationToken cancellationToken = default)
    {
        var rows = ImmutableArray.CreateBuilder<PatternMatrixRow>();
        foreach (var row in Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!row.HasErrors && !row.Patterns.IsEmpty && IsWildcard(row.Patterns[0]))
                rows.Add(new PatternMatrixRow(row.Patterns.RemoveAt(0), row.SourceOrdinal));
        }
        return new PatternMatrix(rows.ToImmutable(), Limits, cancellationToken);
    }

    public string ToDebugString()
    {
        var builder = new StringBuilder();
        foreach (var row in Rows.OrderBy(row => row.SourceOrdinal))
        {
            builder.Append('[').Append(row.SourceOrdinal).Append("] ");
            builder.AppendJoin(" | ", row.Patterns.Select(BoundPatternPrinter.Print));
            if (row.HasErrors) builder.Append(" <error-row>");
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static bool IsWildcard(BoundPattern pattern) =>
        pattern is BoundWildcardPattern or BoundValueBindingPattern;

    private static bool TryGetConstructor(BoundPattern pattern, out PatternConstructor constructor)
    {
        constructor = pattern switch
        {
            BoundEnumCasePattern enumCase => PatternConstructor.ForEnumCase(enumCase.Case),
            BoundOptionalSomePattern some => PatternConstructor.OptionalSome(some.OptionalType.ElementType),
            BoundNilPattern => PatternConstructor.OptionalNone,
            BoundLiteralPattern { Value: bool value } => PatternConstructor.Boolean(value),
            BoundLiteralPattern literal => PatternConstructor.Literal(literal.Value),
            _ => null!,
        };
        return constructor is not null;
    }

    private static ImmutableArray<BoundPattern> GetArguments(BoundPattern pattern) => pattern switch
    {
        BoundEnumCasePattern enumCase => enumCase.AssociatedPatterns,
        BoundOptionalSomePattern some => [some.ValuePattern],
        _ => [],
    };

    private static void Validate(
        ImmutableArray<PatternMatrixRow> rows,
        PatternMatrixLimits limits,
        CancellationToken cancellationToken)
    {
        if (limits.MaximumRows <= 0 || limits.MaximumColumns <= 0)
            throw new ArgumentOutOfRangeException(nameof(limits), "Pattern matrix limits must be positive.");
        if (rows.Length > limits.MaximumRows)
            throw new PatternMatrixCapacityException($"Pattern matrix row limit ({limits.MaximumRows}) exceeded.");
        var width = rows.IsEmpty ? 0 : rows[0].Patterns.Length;
        if (width > limits.MaximumColumns)
            throw new PatternMatrixCapacityException($"Pattern matrix column limit ({limits.MaximumColumns}) exceeded.");
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.Patterns.Length != width)
                throw new ArgumentException("Every pattern matrix row must have the same width.", nameof(rows));
        }
    }
}
