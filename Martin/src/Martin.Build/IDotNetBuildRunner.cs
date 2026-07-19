namespace Martin.Build;

public interface IDotNetBuildRunner
{
    Task<DotNetBuildResult> RunAsync(
        DotNetBuildRequest request,
        CancellationToken cancellationToken = default);
}
