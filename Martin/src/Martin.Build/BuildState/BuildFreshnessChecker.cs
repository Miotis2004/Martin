using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Martin.Build.BuildState;

public enum BuildFreshnessStatus
{
    Fresh,
    MissingState,
    InvalidState,
    ManifestChanged,
    SourceSetChanged,
    SourceChanged,
    CompilerChanged,
    RuntimeChanged,
    ConfigurationChanged,
    TargetFrameworkChanged,
    AssemblyNameChanged,
    ArtifactMissing
}

public sealed record BuildFreshnessCheckInputs
{
    public required string ProjectRoot { get; init; }
    public required string ManifestPath { get; init; }
    public required IEnumerable<string> SourceFiles { get; init; }
    public required string CompilerVersion { get; init; }
    public required string RuntimeVersion { get; init; }
    public required string RuntimeSha256 { get; init; }
    public required BuildConfiguration Configuration { get; init; }
    public required string TargetFramework { get; init; }
    public required string AssemblyName { get; init; }
}

public sealed record BuildFreshnessResult
{
    public required BuildFreshnessStatus Status { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? EntryPointPath { get; init; }
    public bool IsFresh => Status == BuildFreshnessStatus.Fresh;
}

public sealed class BuildFreshnessChecker
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public BuildFreshnessResult Check(string outputDirectory, BuildFreshnessCheckInputs inputs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(inputs);

        var outputRoot = NormalizeRoot(Path.GetFullPath(outputDirectory));
        var statePath = Path.Combine(outputRoot, BuildStateStore.FileName);
        if (!File.Exists(statePath))
            return Stale(BuildFreshnessStatus.MissingState, "build-state file is missing");

        BuildStateDocument? state;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            state = JsonSerializer.Deserialize<BuildStateDocument>(File.ReadAllText(statePath), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Stale(BuildFreshnessStatus.InvalidState, "build-state file is invalid");
        }

        if (state is null || state.SchemaVersion != BuildStateDocument.CurrentSchemaVersion)
            return Stale(BuildFreshnessStatus.InvalidState, "build-state schema is unsupported");

        var projectRoot = Path.GetFullPath(inputs.ProjectRoot);
        if (!SamePath(state.ProjectRoot, projectRoot) || !SamePath(state.ManifestPath, inputs.ManifestPath) || !File.Exists(inputs.ManifestPath) || state.ManifestSha256 != Sha256(inputs.ManifestPath, cancellationToken))
            return Stale(BuildFreshnessStatus.ManifestChanged, "manifest changed");

        var currentSources = inputs.SourceFiles.Select(Path.GetFullPath).Order(StringComparer.Ordinal).Select(path => Fingerprint(projectRoot, path, cancellationToken)).ToImmutableArray();
        if (!state.Sources.Select(source => source.RelativePath).SequenceEqual(currentSources.Select(source => source.RelativePath), StringComparer.Ordinal))
            return Stale(BuildFreshnessStatus.SourceSetChanged, "source file set changed");

        for (var i = 0; i < state.Sources.Length; i++)
        {
            if (state.Sources[i].Sha256 != currentSources[i].Sha256)
                return Stale(BuildFreshnessStatus.SourceChanged, $"source file '{currentSources[i].RelativePath}' changed");
        }

        if (!StringComparer.Ordinal.Equals(state.CompilerVersion, inputs.CompilerVersion))
            return Stale(BuildFreshnessStatus.CompilerChanged, "compiler version changed");
        if (!StringComparer.Ordinal.Equals(state.RuntimeVersion, inputs.RuntimeVersion))
            return Stale(BuildFreshnessStatus.RuntimeChanged, "runtime version changed");
        if (!StringComparer.OrdinalIgnoreCase.Equals(state.RuntimeSha256, inputs.RuntimeSha256))
            return Stale(BuildFreshnessStatus.RuntimeChanged, "runtime changed");
        if (state.Configuration != inputs.Configuration)
            return Stale(BuildFreshnessStatus.ConfigurationChanged, "build configuration changed");
        if (!StringComparer.Ordinal.Equals(state.TargetFramework, inputs.TargetFramework))
            return Stale(BuildFreshnessStatus.TargetFrameworkChanged, "target framework changed");
        if (!StringComparer.Ordinal.Equals(state.AssemblyName, inputs.AssemblyName))
            return Stale(BuildFreshnessStatus.AssemblyNameChanged, "assembly name changed");

        if (!TryResolveStatePath(outputRoot, state.EntryPointPath, out var entryPoint, out var invalidEntryPointReason))
            return Stale(BuildFreshnessStatus.InvalidState, invalidEntryPointReason);
        if (!File.Exists(entryPoint))
            return Stale(BuildFreshnessStatus.ArtifactMissing, "entry point is missing");
        if (ContainsReparsePoint(outputRoot, entryPoint))
            return Stale(BuildFreshnessStatus.InvalidState, $"state path '{state.EntryPointPath}' crosses a reparse point");

        foreach (var artifact in state.Artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveStatePath(outputRoot, artifact.RelativePath, out var artifactPath, out var invalidArtifactReason))
                return Stale(BuildFreshnessStatus.InvalidState, invalidArtifactReason);
            if (!File.Exists(artifactPath))
                return Stale(BuildFreshnessStatus.ArtifactMissing, $"artifact '{artifact.RelativePath}' is missing");
            if (ContainsReparsePoint(outputRoot, artifactPath))
                return Stale(BuildFreshnessStatus.InvalidState, $"state path '{artifact.RelativePath}' crosses a reparse point");
            var info = new FileInfo(artifactPath);
            if (info.Length != artifact.Length || Sha256(artifactPath, cancellationToken) != artifact.Sha256)
                return artifact.Kind == BuildArtifactKind.RuntimeLibrary
                    ? Stale(BuildFreshnessStatus.RuntimeChanged, "runtime artifact changed")
                    : Stale(BuildFreshnessStatus.ArtifactMissing, $"artifact '{artifact.RelativePath}' changed");
        }

        return new BuildFreshnessResult { Status = BuildFreshnessStatus.Fresh, EntryPointPath = entryPoint };
    }

    static BuildFreshnessResult Stale(BuildFreshnessStatus status, string reason) => new() { Status = status, Reason = reason };
    static BuildInputFingerprint Fingerprint(string projectRoot, string path, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        return new() { RelativePath = Relative(projectRoot, path), Sha256 = Sha256(path, cancellationToken), Length = info.Length, LastWriteTimeUtc = info.LastWriteTimeUtc };
    }
    static bool SamePath(string left, string right) => StringComparer.OrdinalIgnoreCase.Equals(NormalizeRoot(Path.GetFullPath(left)), NormalizeRoot(Path.GetFullPath(right)));
    static string Relative(string root, string path) => Path.GetRelativePath(root, Path.GetFullPath(path)).Replace(Path.DirectorySeparatorChar, '/');

    static bool TryResolveStatePath(string outputRoot, string statePath, out string resolvedPath, out string reason)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(statePath))
        {
            reason = "build-state path is empty";
            return false;
        }

        if (statePath.Contains('\\', StringComparison.Ordinal))
        {
            reason = $"build-state path '{statePath}' is not normalized";
            return false;
        }

        if (Path.IsPathRooted(statePath) || LooksLikeWindowsRootedPath(statePath))
        {
            reason = $"build-state path '{statePath}' is rooted";
            return false;
        }

        var parts = statePath.Split('/', StringSplitOptions.None);
        if (parts.Any(part => part.Length == 0 || part == "."))
        {
            reason = $"build-state path '{statePath}' is not normalized";
            return false;
        }

        if (parts.Any(part => part == ".."))
        {
            reason = $"build-state path '{statePath}' escapes the output directory";
            return false;
        }

        resolvedPath = Path.GetFullPath(Path.Combine(outputRoot, Path.Combine(parts)));
        if (!IsInsideRoot(outputRoot, resolvedPath) || SamePath(outputRoot, resolvedPath))
        {
            reason = $"build-state path '{statePath}' resolves outside the output directory";
            resolvedPath = string.Empty;
            return false;
        }

        reason = string.Empty;
        return true;
    }

    static bool LooksLikeWindowsRootedPath(string path) =>
        path.StartsWith("//", StringComparison.Ordinal) ||
        (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && (path[2] == '/' || path[2] == '\\'));

    static bool IsInsideRoot(string root, string path)
    {
        var normalizedRoot = NormalizeRoot(root);
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    static string NormalizeRoot(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    static bool ContainsReparsePoint(string outputRoot, string path)
    {
        var current = path;
        while (IsInsideRoot(outputRoot, current) || SamePath(outputRoot, current))
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                var attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    return true;
            }

            if (SamePath(outputRoot, current))
                return false;

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || SamePath(parent, current))
                return false;
            current = parent;
        }

        return true;
    }
    static string Sha256(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }
}
