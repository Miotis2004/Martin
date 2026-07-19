using Martin.Runtime;
using Xunit;

namespace Martin.Runtime.Tests;

public sealed class OptionalTests
{
    [Fact]
    public void Optional_value_reports_presence_and_value()
    {
        var optional = new Optional<int>(42);

        Assert.True(optional.HasValue);
        Assert.Equal(42, optional.Value);
    }

    [Fact]
    public void Optional_none_throws_when_value_is_read()
    {
        var optional = Optional<int>.None;

        Assert.False(optional.HasValue);
        Assert.Throws<InvalidOperationException>(() => optional.Value);
    }
}
