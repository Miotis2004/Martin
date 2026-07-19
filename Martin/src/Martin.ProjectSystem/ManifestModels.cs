using System.Collections.Immutable;

namespace Martin.ProjectSystem;

public sealed record PackageSection(string Name, string Version);
public sealed record TargetSection(string Kind, string Framework, string Entry);

public sealed record SourcesSection
{
    public ImmutableArray<string> Include { get; init; } = ["Sources/**/*.martin"];
    public ImmutableArray<string> Exclude { get; init; } = ["bin/**", "obj/**", ".martin/**"];
}

public sealed record BuildSection
{
    public string Output { get; init; } = "bin";
    public string Intermediate { get; init; } = "obj";
}

public sealed record TestsSection
{
    public ImmutableArray<string> Include { get; init; } = ["Tests/**/*.martin"];
}

public sealed class MartinManifest
{
    public int ManifestVersion { get; init; }
    public required PackageSection Package { get; init; }
    public required TargetSection Target { get; init; }
    public SourcesSection Sources { get; init; } = new();
    public BuildSection Build { get; init; } = new();
    public TestsSection Tests { get; init; } = new();
}
