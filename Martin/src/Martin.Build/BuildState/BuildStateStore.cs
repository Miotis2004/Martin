using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Text;

namespace Martin.Build.BuildState;

public sealed record BuildStateInputs
{
    public required string ProjectRoot { get; init; }
    public required string ManifestPath { get; init; }
    public required IEnumerable<string> SourceFiles { get; init; }
    public required string CompilerVersion { get; init; }
    public required string RuntimeVersion { get; init; }
    public required BuildConfiguration Configuration { get; init; }
    public required string TargetFramework { get; init; }
    public required string AssemblyName { get; init; }
}

public sealed record BuildStateWriteResult
{
    public string? StatePath { get; init; }
    public ImmutableArray<Diagnostic> Diagnostics { get; init; } = [];
    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}

public sealed class BuildStateStore
{
    public const string FileName = ".martin-build-state.json";

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public BuildStateDocument CreateDocument(BuildStateInputs inputs, BuildResult result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Success)
            throw new ArgumentException("Build state can only be created for a successful build.", nameof(result));
        if (string.IsNullOrWhiteSpace(result.OutputDirectory))
            throw new ArgumentException("A successful build result must include an output directory.", nameof(result));
        if (string.IsNullOrWhiteSpace(result.EntryPointPath))
            throw new ArgumentException("A successful build result must include an entry point path.", nameof(result));

        var outputRoot = Path.GetFullPath(result.OutputDirectory);
        var artifacts = result.Artifacts
            .OrderBy(artifact => Relative(outputRoot, artifact.Path), StringComparer.Ordinal)
            .Select(artifact =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new BuildArtifactState
                {
                    Kind = artifact.Kind,
                    RelativePath = Relative(outputRoot, artifact.Path),
                    Sha256 = Sha256(artifact.Path, cancellationToken),
                    Length = new FileInfo(artifact.Path).Length
                };
            })
            .ToImmutableArray();

        cancellationToken.ThrowIfCancellationRequested();
        var runtimeArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Kind == BuildArtifactKind.RuntimeLibrary);
        var runtimeSha256 = runtimeArtifact is not null && File.Exists(runtimeArtifact.Path) ? Sha256(runtimeArtifact.Path, cancellationToken) : string.Empty;

        var projectRoot = Path.GetFullPath(inputs.ProjectRoot);
        return new BuildStateDocument
        {
            ProjectRoot = projectRoot,
            ManifestPath = Path.GetFullPath(inputs.ManifestPath),
            ManifestSha256 = Sha256(inputs.ManifestPath, cancellationToken),
            Sources = inputs.SourceFiles
                .Select(Path.GetFullPath)
                .Order(StringComparer.Ordinal)
                .Select(source => Fingerprint(projectRoot, source, cancellationToken))
                .ToImmutableArray(),
            CompilerVersion = inputs.CompilerVersion,
            RuntimeVersion = inputs.RuntimeVersion,
            RuntimeSha256 = runtimeSha256,
            Configuration = inputs.Configuration,
            TargetFramework = inputs.TargetFramework,
            AssemblyName = inputs.AssemblyName,
            EntryPointPath = Relative(outputRoot, result.EntryPointPath),
            Artifacts = artifacts
        };
    }

    public BuildStateWriteResult Write(BuildStateDocument document, string outputDirectory, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var statePath = Path.Combine(Path.GetFullPath(outputDirectory), FileName);
        var tempPath = statePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            cancellationToken.ThrowIfCancellationRequested();
            var json = JsonSerializer.Serialize(document, JsonOptions) + Environment.NewLine;
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(tempPath, json);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, statePath, overwrite: true);
            return new BuildStateWriteResult { StatePath = statePath };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            TryDelete(tempPath);
            return new BuildStateWriteResult { Diagnostics = [Diag("MRT4515", $"Build-state write failed: {ex.Message}")] };
        }
    }

    static BuildInputFingerprint Fingerprint(string projectRoot, string path, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        return new BuildInputFingerprint
        {
            RelativePath = Relative(projectRoot, path),
            Sha256 = Sha256(path, cancellationToken),
            Length = info.Length,
            LastWriteTimeUtc = info.LastWriteTimeUtc
        };
    }

    static string Relative(string root, string path) => Path.GetRelativePath(root, Path.GetFullPath(path)).Replace(Path.DirectorySeparatorChar, '/');
    static string Sha256(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }
    static Diagnostic Diag(string code, string message) => new(code, DiagnosticSeverity.Error, message, new TextLocation(SourceText.From(string.Empty), new TextSpan(0, 0)));
    static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
