namespace Martin.Runtime;

/// <summary>
/// Base class for exceptions used to transport a Martin error value through the
/// CLR exception mechanism.
/// </summary>
/// <remarks>
/// Host exceptions do not derive from this type, allowing generated catch
/// boundaries to distinguish Martin errors from unrelated runtime failures.
/// </remarks>
public abstract class MartinThrownError : Exception
{
    protected MartinThrownError(string message)
        : base(message)
    {
    }

    /// <summary>Gets the original Martin error value.</summary>
    public abstract object ErrorValue { get; }

    /// <summary>Gets the runtime type declared by the Martin throws clause.</summary>
    public abstract Type DeclaredErrorType { get; }
}
