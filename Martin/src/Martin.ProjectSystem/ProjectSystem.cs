using System.Collections.Immutable;

namespace Martin.ProjectSystem;

public static class MartinProjectLoader
{
    public const int SupportedManifestVersion = ManifestParser.SupportedManifestVersion;

    public static ProjectLoadResult Load(ProjectLoadOptions options, CancellationToken cancellationToken = default)
    {
        var diagnostics = ImmutableArray.CreateBuilder<ProjectDiagnostic>();
        var locator = new ProjectLocator();
        var parser = new ManifestParser();
        var sourceDiscovery = new SourceDiscovery();

        cancellationToken.ThrowIfCancellationRequested();
        var manifestPath = locator.ResolveManifest(options, diagnostics);
        if (manifestPath is null) return new() { Diagnostics = diagnostics.ToImmutable() };

        var rootDirectory = Path.GetDirectoryName(manifestPath)!;
        cancellationToken.ThrowIfCancellationRequested();
        var manifest = parser.Parse(manifestPath, diagnostics, cancellationToken);
        if (manifest is null) return new() { Diagnostics = diagnostics.ToImmutable() };

        var generatedExcludePatterns = ImmutableArray.Create(
            manifest.Build.Output + "/**",
            manifest.Build.Intermediate + "/**");
        var sourceExcludePatterns = manifest.Sources.Exclude.AddRange(generatedExcludePatterns);

        var sourceFiles = sourceDiscovery.Discover(
            rootDirectory,
            manifest.Sources.Include,
            sourceExcludePatterns,
            reportInvalidIncludePatterns: true,
            diagnostics,
            cancellationToken);
        var testFiles = sourceDiscovery.Discover(
            rootDirectory,
            manifest.Tests.Include,
            generatedExcludePatterns,
            reportInvalidIncludePatterns: false,
            diagnostics,
            cancellationToken);

        if (sourceFiles.Length == 0)
        {
            diagnostics.Add(ProjectDiagnostics.Error("MRT4006", "No Martin source files were found.", manifestPath));
        }

        return new()
        {
            Project = new()
            {
                RootDirectory = rootDirectory,
                ManifestPath = manifestPath,
                Manifest = manifest,
                SourceFiles = sourceFiles,
                TestFiles = testFiles
            },
            Diagnostics = diagnostics.ToImmutable()
        };
    }

    public static MartinManifest? ParseManifest(string path, ImmutableArray<ProjectDiagnostic>.Builder diagnostics, CancellationToken cancellationToken = default) =>
        new ManifestParser().Parse(path, diagnostics, cancellationToken);
}
