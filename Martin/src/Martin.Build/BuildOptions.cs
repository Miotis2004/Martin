using System.Collections.Immutable;
using Martin.CodeGeneration;

namespace Martin.Build;

public sealed record ProjectBuildStateInputs
{
    public required string ProjectRoot { get; init; }
    public required string ManifestPath { get; init; }
    public required ImmutableArray<string> SourceFiles { get; init; }
    public required string CompilerVersion { get; init; }
    public required string RuntimeVersion { get; init; }
}

public sealed record BuildOptions
{
    public required string OutputDirectory { get; init; }
    public required string AssemblyName { get; init; }
    public string TargetFramework { get; init; } = "net8.0";
    public BuildConfiguration Configuration { get; init; } = BuildConfiguration.Debug;
    public OutputKind OutputKind { get; init; } = OutputKind.ConsoleApplication;
    public bool KeepGeneratedFiles { get; init; }
    public bool UseAppHost { get; init; } = true;
    public bool EmitPortablePdb { get; init; } = true;
    public bool Deterministic { get; init; } = true;
    public string                  ? RuntimePath { get; init; }
    public string                  ? RuntimeDirectory { get; init; }
    public ProjectBuildStateInputs ? BuildStateInputs { get; init; }
}
