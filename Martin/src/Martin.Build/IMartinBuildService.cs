using Martin.Compiler;

namespace Martin.Build;

public interface IMartinBuildService
{
    Task<BuildResult> BuildAsync(Compilation compilation, BuildOptions options, CancellationToken cancellationToken = default);
}
