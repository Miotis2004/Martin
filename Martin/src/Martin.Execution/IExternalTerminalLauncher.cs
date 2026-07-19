namespace Martin.Execution;

public interface IExternalTerminalLauncher
{
    Task<ExecutionResult> LaunchAsync(ProcessExecutionRequest request, CancellationToken cancellationToken);
}
