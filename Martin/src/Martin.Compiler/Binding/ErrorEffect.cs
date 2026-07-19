using Martin.Compiler.Generics;
using Martin.Compiler.Symbols;

namespace Martin.Compiler.Binding;

/// <summary>Describes the typed error that can escape a bound expression.</summary>
/// <remarks>Error effects are deliberately independent of an expression's value type.</remarks>
public readonly record struct ErrorEffect
{
    public ErrorEffect(bool canThrow, TypeSymbol? errorType)
        : this(canThrow, errorType, isConflicting: false)
    {
    }

    private ErrorEffect(bool canThrow, TypeSymbol? errorType, bool isConflicting)
    {
        if (canThrow != (errorType is not null))
            throw new ArgumentException("A throwing effect must have an error type, and a nonthrowing effect must not.", nameof(errorType));
        if (isConflicting && (!canThrow || errorType != TypeSymbol.Error))
            throw new ArgumentException("A conflicting effect must be represented as a throwing recovery effect.", nameof(isConflicting));
        CanThrow = canThrow;
        ErrorType = errorType;
        IsConflicting = isConflicting;
    }

    public bool CanThrow { get; }
    public TypeSymbol? ErrorType { get; }
    public bool IsConflicting { get; }
    public static ErrorEffect None => default;
    public static ErrorEffect Conflicting => new(true, TypeSymbol.Error, isConflicting: true);

    public static ErrorEffect FromCallable(bool isThrowing, TypeSymbol? errorType) =>
        isThrowing && errorType is not null ? new(true, errorType) : None;

    public static ErrorEffect Combine(params ErrorEffect[] effects)
    {
        TypeSymbol? result = null;
        foreach (var effect in effects)
        {
            if (!effect.CanThrow)
                continue;
            if (effect.IsConflicting)
                return Conflicting;
            if (result is null)
                result = effect.ErrorType;
            else if (!TypeIdentity.Create(result).Equals(TypeIdentity.Create(effect.ErrorType!)))
                return Conflicting;
        }
        return result is null ? None : new(true, result);
    }

    public static ErrorEffect Combine(IEnumerable<ErrorEffect> effects, CancellationToken cancellationToken = default)
    {
        var result = None;
        foreach (var effect in effects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = Combine(result, effect);
        }
        return result;
    }
}
