using Xunit;

namespace Martin.Build.Tests;

public sealed class BuildContractTests
{
    [Fact]
    public void Build_result_reports_status_flags()
    {
        var succeeded = new BuildResult { Status = BuildStatus.Succeeded };
        var cancelled = new BuildResult { Status = BuildStatus.Cancelled };

        Assert.True(succeeded.Success);
        Assert.False(succeeded.WasCancelled);
        Assert.False(cancelled.Success);
        Assert.True(cancelled.WasCancelled);
    }
}
