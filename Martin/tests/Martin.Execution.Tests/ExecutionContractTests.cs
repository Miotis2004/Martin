using Martin.Build;
using Martin.Execution;
using Xunit;

namespace Martin.Execution.Tests;

public sealed class ExecutionContractTests
{
    [Fact]
    public void Execution_result_reports_status_flags()
    {
        var result = new ExecutionResult { Status = ExecutionStatus.Completed, ExitCode = 0 };

        Assert.True(result.Started);
        Assert.True(result.Completed);
        Assert.False(result.WasCancelled);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Execution_options_capture_process_contract_inputs()
    {
        var options = new ExecutionOptions
        {
            Arguments = ["--verbose"],
            WorkingDirectory = "/tmp/martin",
            StandardInput = "input",
            EnvironmentVariables = new Dictionary<string, string?> { ["MARTIN_TEST"] = "1" }
        };

        Assert.Equal(["--verbose"], options.Arguments);
        Assert.Equal("/tmp/martin", options.WorkingDirectory);
        Assert.Equal("input", options.StandardInput);
        Assert.False(options.UseExternalTerminal);
        Assert.Equal("1", options.EnvironmentVariables["MARTIN_TEST"]);
    }

    [Fact]
    public void Execution_service_contract_accepts_execution_options()
    {
        var method = typeof(IMartinExecutionService).GetMethod(nameof(IMartinExecutionService.RunAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(ExecutionOptions), method!.GetParameters()[1].ParameterType);
    }

    [Fact]
    public void Execution_project_references_build_contracts_without_reverse_reference()
    {
        var executionReferences = typeof(IMartinExecutionService).Assembly.GetReferencedAssemblies().Select(name => name.Name).ToArray();
        var buildReferences = typeof(BuildResult).Assembly.GetReferencedAssemblies().Select(name => name.Name).ToArray();

        Assert.Contains("Martin.Build", executionReferences);
        Assert.DoesNotContain("Martin.Execution", buildReferences);
    }
}
