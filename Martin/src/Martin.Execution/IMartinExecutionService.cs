using Martin.Build;

namespace Martin.Execution;

public interface IMartinExecutionService
{
    Task<ExecutionResult> RunAsync(BuildResult build, ExecutionOptions options, CancellationToken cancellationToken = default);
}
