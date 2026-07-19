using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Martin.Build.BuildState;

public sealed record BuildStateDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string ProjectRoot { get; init; }
    public required string ManifestPath { get; init; }
    public required string ManifestSha256 { get; init; }
    public ImmutableArray<BuildInputFingerprint> Sources { get; init; } = [];
    public required string CompilerVersion { get; init; }
    public required string RuntimeVersion { get; init; }
    public required string RuntimeSha256 { get; init; }
    public required BuildConfiguration Configuration { get; init; }
    public required string TargetFramework { get; init; }
    public required string AssemblyName { get; init; }
    public required string EntryPointPath { get; init; }
    public ImmutableArray<BuildArtifactState> Artifacts { get; init; } = [];
}

public sealed record BuildInputFingerprint
{
    public required string RelativePath { get; init; }
    public required string Sha256 { get; init; }
    public long Length { get; init; }
    public DateTimeOffset LastWriteTimeUtc { get; init; }
}

public sealed record BuildArtifactState
{
    [JsonConverter(typeof(JsonStringEnumConverter<BuildArtifactKind>))]
    public required BuildArtifactKind Kind { get; init; }
    public required string RelativePath { get; init; }
    public required string Sha256 { get; init; }
    public long Length { get; init; }
}
