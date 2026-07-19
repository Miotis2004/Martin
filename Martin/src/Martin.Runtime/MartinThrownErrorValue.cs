namespace Martin.Runtime;

/// <summary>
/// Non-generic exception carrier for a thrown Martin error value.
/// </summary>
public sealed class MartinThrownErrorValue : MartinThrownError
{
    /// <summary>Creates a carrier for <paramref name="errorValue"/>.</summary>
    /// <param name="errorValue">The original, non-null Martin error value.</param>
    /// <param name="declaredErrorType">The non-null runtime type from the throws clause.</param>
    /// <param name="message">
    /// An optional diagnostic message. When omitted, a deterministic message is
    /// produced from <paramref name="declaredErrorType"/>.
    /// </param>
    public MartinThrownErrorValue(
        object errorValue,
        Type declaredErrorType,
        string? message = null)
        : base(CreateMessage(declaredErrorType, message))
    {
        ErrorValue = errorValue ?? throw new ArgumentNullException(nameof(errorValue));
        DeclaredErrorType = declaredErrorType;
    }

    public override object ErrorValue { get; }

    public override Type DeclaredErrorType { get; }

    private static string CreateMessage(Type declaredErrorType, string? message)
    {
        ArgumentNullException.ThrowIfNull(declaredErrorType);
        return message ?? $"A Martin {declaredErrorType.Name} was thrown.";
    }
}
