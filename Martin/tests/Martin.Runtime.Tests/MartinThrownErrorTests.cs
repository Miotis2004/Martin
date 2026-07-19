using Martin.Runtime;
using Xunit;

namespace Martin.Runtime.Tests;

public sealed class MartinThrownErrorTests
{
    [Fact]
    public void Carrier_preserves_reference_identity_and_declared_type()
    {
        var value = new ReferenceError("missing.txt");

        var error = new MartinThrownErrorValue(value, typeof(ReferenceError));

        Assert.Same(value, error.ErrorValue);
        Assert.Equal(typeof(ReferenceError), error.DeclaredErrorType);
        Assert.Equal("A Martin ReferenceError was thrown.", error.Message);
    }

    [Fact]
    public void Carrier_preserves_boxed_value_and_payload()
    {
        var value = new ValueError(17, "payload");

        var error = new MartinThrownErrorValue(value, typeof(ValueError));

        Assert.Equal(value, Assert.IsType<ValueError>(error.ErrorValue));
    }

    [Fact]
    public void Carrier_supports_constructed_generic_error_types()
    {
        var value = new GenericError<int>(42);

        var error = new MartinThrownErrorValue(value, typeof(GenericError<int>));

        Assert.Same(value, error.ErrorValue);
        Assert.Equal(typeof(GenericError<int>), error.DeclaredErrorType);
        Assert.Equal("A Martin GenericError`1 was thrown.", error.Message);
    }

    [Fact]
    public void Carrier_uses_an_explicit_message_verbatim()
    {
        var error = new MartinThrownErrorValue(new ReferenceError("x"), typeof(ReferenceError), "custom");

        Assert.Equal("custom", error.Message);
    }

    [Fact]
    public void Carrier_rejects_null_contract_values()
    {
        Assert.Throws<ArgumentNullException>(() => new MartinThrownErrorValue(null!, typeof(ValueError)));
        Assert.Throws<ArgumentNullException>(() => new MartinThrownErrorValue(new ValueError(0, ""), null!));
    }

    [Fact]
    public void Host_exceptions_remain_distinguishable()
    {
        Assert.False(new InvalidOperationException() is MartinThrownError);
        Assert.IsAssignableFrom<Exception>(
            new MartinThrownErrorValue(new ValueError(0, ""), typeof(ValueError)));
    }

    private sealed record ReferenceError(string Path);

    private readonly record struct ValueError(int Code, string Payload);

    private sealed record GenericError<T>(T Value);
}
