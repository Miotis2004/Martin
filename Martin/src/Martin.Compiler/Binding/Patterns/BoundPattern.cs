using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;

namespace Martin.Compiler.Binding;

/// <summary>A backend-independent semantic representation of a Martin pattern.</summary>
public abstract class BoundPattern : BoundNode
{
    protected BoundPattern(
        TypeSymbol inputType,
        ImmutableArray<LocalVariableSymbol> declaredVariables,
        TextLocation location,
        bool hasErrors)
    {
        InputType = inputType ?? throw new ArgumentNullException(nameof(inputType));
        DeclaredVariables = declaredVariables.IsDefault ? [] : declaredVariables;
        Location = location;
        HasErrors = hasErrors;
    }

    public TypeSymbol InputType { get; }
    public ImmutableArray<LocalVariableSymbol> DeclaredVariables { get; }
    public TextLocation Location { get; }
    public bool HasErrors { get; }

    /// <summary>Whether this pattern covers every value of <see cref="InputType"/>.</summary>
    public virtual bool IsIrrefutable => false;

    public abstract void Accept(BoundPatternVisitor visitor);
    public abstract TResult Accept<TResult>(BoundPatternVisitor<TResult> visitor);
}

public sealed class BoundWildcardPattern(TypeSymbol inputType, TextLocation location, bool hasErrors = false)
    : BoundPattern(inputType, [], location, hasErrors)
{
    public override BoundNodeKind Kind => BoundNodeKind.WildcardPattern;
    public override bool IsIrrefutable => !HasErrors;
    public override void Accept(BoundPatternVisitor visitor) => visitor.VisitWildcardPattern(this);
    public override TResult Accept<TResult>(BoundPatternVisitor<TResult> visitor) => visitor.VisitWildcardPattern(this);
}

public sealed class BoundLiteralPattern(object? value, TypeSymbol inputType, TextLocation location, bool hasErrors = false)
    : BoundPattern(inputType, [], location, hasErrors)
{
    public override BoundNodeKind Kind => BoundNodeKind.LiteralPattern;
    public object? Value { get; } = value;
    public override void Accept(BoundPatternVisitor visitor) => visitor.VisitLiteralPattern(this);
    public override TResult Accept<TResult>(BoundPatternVisitor<TResult> visitor) => visitor.VisitLiteralPattern(this);
}

public sealed class BoundEnumCasePattern(
    TypeSymbol enumType,
    EnumCaseSymbol @case,
    ImmutableArray<BoundPattern> associatedPatterns,
    TextLocation location,
    bool hasErrors = false)
    : BoundPattern(enumType, CollectVariables(associatedPatterns), location, hasErrors || HasChildErrors(associatedPatterns))
{
    public override BoundNodeKind Kind => BoundNodeKind.EnumCasePattern;
    public TypeSymbol EnumType { get; } = enumType ?? throw new ArgumentNullException(nameof(enumType));
    public EnumCaseSymbol Case { get; } = @case ?? throw new ArgumentNullException(nameof(@case));
    public ImmutableArray<BoundPattern> AssociatedPatterns { get; } = associatedPatterns.IsDefault ? [] : associatedPatterns;
    public override void Accept(BoundPatternVisitor visitor) => visitor.VisitEnumCasePattern(this);
    public override TResult Accept<TResult>(BoundPatternVisitor<TResult> visitor) => visitor.VisitEnumCasePattern(this);

    private static ImmutableArray<LocalVariableSymbol> CollectVariables(ImmutableArray<BoundPattern> patterns) =>
        patterns.IsDefault ? [] : patterns.SelectMany(pattern => pattern.DeclaredVariables).ToImmutableArray();
    private static bool HasChildErrors(ImmutableArray<BoundPattern> patterns) =>
        !patterns.IsDefault && patterns.Any(pattern => pattern.HasErrors);
}

public sealed class BoundOptionalSomePattern(
    OptionalTypeSymbol optionalType,
    BoundPattern valuePattern,
    TextLocation location,
    bool hasErrors = false)
    : BoundPattern(optionalType, valuePattern.DeclaredVariables, location, hasErrors || valuePattern.HasErrors)
{
    public override BoundNodeKind Kind => BoundNodeKind.OptionalSomePattern;
    public OptionalTypeSymbol OptionalType { get; } = optionalType;
    public BoundPattern ValuePattern { get; } = valuePattern;
    public override void Accept(BoundPatternVisitor visitor) => visitor.VisitOptionalSomePattern(this);
    public override TResult Accept<TResult>(BoundPatternVisitor<TResult> visitor) => visitor.VisitOptionalSomePattern(this);
}

public sealed class BoundNilPattern(OptionalTypeSymbol optionalType, TextLocation location, bool hasErrors = false)
    : BoundPattern(optionalType, [], location, hasErrors)
{
    public override BoundNodeKind Kind => BoundNodeKind.NilPattern;
    public OptionalTypeSymbol OptionalType { get; } = optionalType;
    public override void Accept(BoundPatternVisitor visitor) => visitor.VisitNilPattern(this);
    public override TResult Accept<TResult>(BoundPatternVisitor<TResult> visitor) => visitor.VisitNilPattern(this);
}

public sealed class BoundValueBindingPattern(LocalVariableSymbol variable, TypeSymbol valueType, TextLocation location, bool hasErrors = false)
    : BoundPattern(valueType, [variable], location, hasErrors)
{
    public override BoundNodeKind Kind => BoundNodeKind.ValueBindingPattern;
    public LocalVariableSymbol Variable { get; } = variable;
    public TypeSymbol ValueType { get; } = valueType;
    public override bool IsIrrefutable => !HasErrors;
    public override void Accept(BoundPatternVisitor visitor) => visitor.VisitValueBindingPattern(this);
    public override TResult Accept<TResult>(BoundPatternVisitor<TResult> visitor) => visitor.VisitValueBindingPattern(this);
}

public abstract class BoundPatternVisitor
{
    public virtual void Visit(BoundPattern pattern) => pattern.Accept(this);
    protected virtual void DefaultVisit(BoundPattern pattern) { }
    public virtual void VisitWildcardPattern(BoundWildcardPattern pattern) => DefaultVisit(pattern);
    public virtual void VisitLiteralPattern(BoundLiteralPattern pattern) => DefaultVisit(pattern);
    public virtual void VisitEnumCasePattern(BoundEnumCasePattern pattern) { foreach (var child in pattern.AssociatedPatterns) Visit(child); }
    public virtual void VisitOptionalSomePattern(BoundOptionalSomePattern pattern) => Visit(pattern.ValuePattern);
    public virtual void VisitNilPattern(BoundNilPattern pattern) => DefaultVisit(pattern);
    public virtual void VisitValueBindingPattern(BoundValueBindingPattern pattern) => DefaultVisit(pattern);
}

public abstract class BoundPatternVisitor<TResult>
{
    public virtual TResult Visit(BoundPattern pattern) => pattern.Accept(this);
    protected abstract TResult DefaultVisit(BoundPattern pattern);
    public virtual TResult VisitWildcardPattern(BoundWildcardPattern pattern) => DefaultVisit(pattern);
    public virtual TResult VisitLiteralPattern(BoundLiteralPattern pattern) => DefaultVisit(pattern);
    public virtual TResult VisitEnumCasePattern(BoundEnumCasePattern pattern) => DefaultVisit(pattern);
    public virtual TResult VisitOptionalSomePattern(BoundOptionalSomePattern pattern) => DefaultVisit(pattern);
    public virtual TResult VisitNilPattern(BoundNilPattern pattern) => DefaultVisit(pattern);
    public virtual TResult VisitValueBindingPattern(BoundValueBindingPattern pattern) => DefaultVisit(pattern);
}

/// <summary>Produces a stable, source-like representation for diagnostics and tests.</summary>
public static class BoundPatternPrinter
{
    public static string Print(BoundPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var builder = new StringBuilder();
        Write(pattern, builder);
        return builder.ToString();
    }

    private static void Write(BoundPattern pattern, StringBuilder builder)
    {
        switch (pattern)
        {
            case BoundWildcardPattern: builder.Append('_'); break;
            case BoundLiteralPattern literal when literal.Value is string text: builder.Append('"').Append(text.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"'); break;
            case BoundLiteralPattern literal: builder.Append(Convert.ToString(literal.Value, CultureInfo.InvariantCulture)?.ToLowerInvariant() ?? "nil"); break;
            case BoundEnumCasePattern enumCase:
                builder.Append('.').Append(enumCase.Case.Name);
                WriteChildren(enumCase.AssociatedPatterns, builder);
                break;
            case BoundOptionalSomePattern some:
                builder.Append(".some("); Write(some.ValuePattern, builder); builder.Append(')');
                break;
            case BoundNilPattern: builder.Append("nil"); break;
            case BoundValueBindingPattern binding: builder.Append("let ").Append(binding.Variable.Name); break;
            default: throw new ArgumentOutOfRangeException(nameof(pattern));
        }
        if (pattern.HasErrors) builder.Append(" /* error */");
    }

    private static void WriteChildren(ImmutableArray<BoundPattern> children, StringBuilder builder)
    {
        if (children.IsEmpty) return;
        builder.Append('(');
        for (var i = 0; i < children.Length; i++) { if (i > 0) builder.Append(", "); Write(children[i], builder); }
        builder.Append(')');
    }
}
