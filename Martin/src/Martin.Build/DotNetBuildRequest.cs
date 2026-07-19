namespace Martin.Build;

public sealed record DotNetBuildRequest
{
    public required string ProjectPath { get; init; }
    public required string WorkingDirectory { get; init; }
    public required BuildConfiguration Configuration { get; init; }
    public IReadOnlyList<string> AdditionalArguments { get; init; } = [];
}
