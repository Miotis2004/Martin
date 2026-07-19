using Martin.Execution;
using Xunit;

namespace Martin.Execution.Tests;

public sealed class AssemblyMarkerTests
{
    [Fact]
    public void Assembly_marker_is_available()
    {
        Assert.Equal("Martin.Execution", typeof(AssemblyMarker).Assembly.GetName().Name);
    }
}
