using Xunit;
using Martin.ProjectSystem;

namespace Martin.ProjectSystem.Tests;

public sealed class Phase4ProjectSystemTests
{
    [Fact]
    public void Loads_manifest_and_discovers_sources_in_stable_order()
    {
        using var temp = TempProject();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Sources"));
        File.WriteAllText(Path.Combine(temp.Path, "Sources", "b.martin"), "func b() {}\n");
        File.WriteAllText(Path.Combine(temp.Path, "Sources", "a.martin"), "func main() {}\n");
        WriteManifest(temp.Path);

        var result = MartinProjectLoader.Load(new ProjectLoadOptions { ProjectPath = temp.Path });

        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(2, result.Project!.SourceFiles.Length);
        Assert.EndsWith("a.martin", result.Project.SourceFiles[0]);
        Assert.EndsWith("b.martin", result.Project.SourceFiles[1]);
    }

    [Fact]
    public void Discovers_manifest_from_parent_directories()
    {
        using var temp = TempProject();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Sources", "Nested"));
        File.WriteAllText(Path.Combine(temp.Path, "Sources", "main.martin"), "func main() {}\n");
        WriteManifest(temp.Path);

        var result = MartinProjectLoader.Load(new ProjectLoadOptions { WorkingDirectory = Path.Combine(temp.Path, "Sources", "Nested") });

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(temp.Path, "Martin.toml"), result.Project!.ManifestPath);
    }

    [Fact]
    public void Reports_invalid_manifest_version_and_missing_sources()
    {
        using var temp = TempProject();
        File.WriteAllText(Path.Combine(temp.Path, "Martin.toml"), "manifest-version = 99\n");

        var result = MartinProjectLoader.Load(new ProjectLoadOptions { ProjectPath = temp.Path });

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT4005");
    }

    [Fact]
    public void Rejects_include_paths_that_escape_root()
    {
        using var temp = TempProject();
        WriteManifest(temp.Path, include: "../*.martin");

        var result = MartinProjectLoader.Load(new ProjectLoadOptions { ProjectPath = temp.Path });

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT4007");
    }


    [Fact]
    public void Rejects_library_target_kind_as_unavailable_in_alpha()
    {
        using var temp = TempProject();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Sources"));
        File.WriteAllText(Path.Combine(temp.Path, "Sources", "lib.martin"), "func helper() {}\n");
        WriteManifest(temp.Path, kind: "library");

        var result = MartinProjectLoader.Load(new ProjectLoadOptions { ProjectPath = temp.Path });

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "MRT4015" &&
            diagnostic.Message.Contains("not supported", StringComparison.Ordinal));
    }


    [Fact]
    public void Project_locator_reports_conflicting_project_and_manifest_options()
    {
        var diagnostics = System.Collections.Immutable.ImmutableArray.CreateBuilder<ProjectDiagnostic>();
        var manifestPath = new ProjectLocator().ResolveManifest(
            new ProjectLoadOptions { ProjectPath = "project", ManifestPath = "Martin.toml" },
            diagnostics);

        Assert.Null(manifestPath);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "MRT4502");
    }

    [Fact]
    public async Task Project_loader_observes_cancellation()
    {
        using var temp = TempProject();
        WriteManifest(temp.Path);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.Throws<OperationCanceledException>(() => MartinProjectLoader.Load(new ProjectLoadOptions { ProjectPath = temp.Path }, cancellation.Token));
    }

    [Fact]
    public void Manifest_parser_can_be_exercised_independently()
    {
        using var temp = TempProject();
        WriteManifest(temp.Path);
        var diagnostics = System.Collections.Immutable.ImmutableArray.CreateBuilder<ProjectDiagnostic>();

        var manifest = new ManifestParser().Parse(Path.Combine(temp.Path, "Martin.toml"), diagnostics);

        Assert.NotNull(manifest);
        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == ProjectDiagnosticSeverity.Error));
        Assert.Equal("HelloMartin", manifest!.Package.Name);
    }

    [Fact]
    public void Readme_manifest_example_loads_with_production_parser()
    {
        using var temp = TempProject();
        var readme = File.ReadAllText(Path.Combine(RepoRoot(), "README.md"));
        var marker = "Martin projects will use a project manifest named `Martin.toml`.";
        var markerIndex = readme.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "README project manifest section was not found.");

        var fenceStart = readme.IndexOf("```toml", markerIndex, StringComparison.Ordinal);
        Assert.True(fenceStart >= 0, "README project manifest TOML example was not found.");
        var contentStart = readme.IndexOf('\n', fenceStart) + 1;
        var fenceEnd = readme.IndexOf("```", contentStart, StringComparison.Ordinal);
        Assert.True(fenceEnd > contentStart, "README project manifest TOML example was not closed.");

        var manifestText = readme[contentStart..fenceEnd];
        var manifestPath = Path.Combine(temp.Path, "Martin.toml");
        File.WriteAllText(manifestPath, manifestText);
        var diagnostics = System.Collections.Immutable.ImmutableArray.CreateBuilder<ProjectDiagnostic>();

        var manifest = new ManifestParser().Parse(manifestPath, diagnostics);

        Assert.NotNull(manifest);
        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == ProjectDiagnosticSeverity.Error));
        Assert.Equal(ManifestParser.SupportedManifestVersion, manifest!.ManifestVersion);
        Assert.Equal("executable", manifest.Target.Kind);
        Assert.Equal("main", manifest.Target.Entry);
    }


    [Fact]
    public void Manifest_parser_handles_toml_strings_arrays_and_comments()
    {
        using var temp = TempProject();
        File.WriteAllText(Path.Combine(temp.Path, "Martin.toml"), """
manifest-version = 1

[package]
name = "HelloMartin"
version = "0.1.0"

[target]
kind = "executable"
framework = "net8.0"
entry = "main"
# TOML strings keep comment markers inside values for optional arrays below

[sources]
include = ["Sources/**/*.martin", "Generated/**/*.martin"] # comment after array
exclude = ["bin/**", "obj/**", ".martin/**"]
""");
        var diagnostics = System.Collections.Immutable.ImmutableArray.CreateBuilder<ProjectDiagnostic>();

        var manifest = new ManifestParser().Parse(Path.Combine(temp.Path, "Martin.toml"), diagnostics);

        Assert.NotNull(manifest);
        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == ProjectDiagnosticSeverity.Error));
        Assert.Equal("main", manifest!.Target.Entry);
        Assert.Equal(["Sources/**/*.martin", "Generated/**/*.martin"], manifest.Sources.Include.ToArray());
    }

    [Fact]
    public void Manifest_parser_reports_toml_syntax_locations_and_duplicate_keys()
    {
        using var temp = TempProject();
        File.WriteAllText(Path.Combine(temp.Path, "Martin.toml"), """
manifest-version = 1
manifest-version = 1

[package
""");
        var diagnostics = System.Collections.Immutable.ImmutableArray.CreateBuilder<ProjectDiagnostic>();

        var manifest = new ManifestParser().Parse(Path.Combine(temp.Path, "Martin.toml"), diagnostics);

        Assert.Null(manifest);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "MRT4002" && diagnostic.Line is > 0 && diagnostic.Column is > 0);
    }

    [Fact]
    public void Manifest_validation_reports_wrong_types_unknown_keys_and_invalid_values()
    {
        using var temp = TempProject();
        File.WriteAllText(Path.Combine(temp.Path, "Martin.toml"), """
manifest-version = 1
unknown-top = true

[package]
name = "1Invalid"
version = "not-semver"
extra = "warning"

[target]
kind = "executable"
framework = "net7.0"
entry = ""

[sources]
include = ["Sources/**/*.martin", "../*.martin", "***.martin"]
exclude = "bin/**"

[build]
output = "../bin"

[unknown]
key = "value"
""");
        var diagnostics = System.Collections.Immutable.ImmutableArray.CreateBuilder<ProjectDiagnostic>();

        var manifest = new ManifestParser().Parse(Path.Combine(temp.Path, "Martin.toml"), diagnostics);

        Assert.Null(manifest);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "MRT4010" && diagnostic.Severity == ProjectDiagnosticSeverity.Warning);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "MRT4009" && diagnostic.Message.Contains("sources.exclude", StringComparison.Ordinal));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "MRT4013");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "MRT4014");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "MRT4004" && diagnostic.Message.Contains("target.framework", StringComparison.Ordinal));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "MRT4004" && diagnostic.Message.Contains("target.entry", StringComparison.Ordinal));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "MRT4007");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "MRT4008");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "MRT4011");
    }

    [Fact]
    public void Manifest_validation_rejects_required_key_type_mismatches()
    {
        using var temp = TempProject();
        File.WriteAllText(Path.Combine(temp.Path, "Martin.toml"), """
manifest-version = "1"

[package]
name = "HelloMartin"
version = "0.1.0"

[target]
kind = "executable"
framework = "net8.0"
entry = "main"
""");
        var diagnostics = System.Collections.Immutable.ImmutableArray.CreateBuilder<ProjectDiagnostic>();

        var manifest = new ManifestParser().Parse(Path.Combine(temp.Path, "Martin.toml"), diagnostics);

        Assert.Null(manifest);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "MRT4009" && diagnostic.Message.Contains("manifest-version", StringComparison.Ordinal));
    }

    [Fact]
    public void Source_discovery_uses_path_policy_and_stable_order()
    {
        using var temp = TempProject();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Sources"));
        File.WriteAllText(Path.Combine(temp.Path, "Sources", "z.martin"), "func z() {}\n");
        File.WriteAllText(Path.Combine(temp.Path, "Sources", "a.martin"), "func a() {}\n");
        var diagnostics = System.Collections.Immutable.ImmutableArray.CreateBuilder<ProjectDiagnostic>();

        var files = new SourceDiscovery().Discover(
            temp.Path,
            ["Sources/**/*.martin", "../*.martin"],
            [],
            reportInvalidIncludePatterns: true,
            diagnostics);

        Assert.Equal(2, files.Length);
        Assert.EndsWith("a.martin", files[0]);
        Assert.EndsWith("z.martin", files[1]);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "MRT4007");
    }

    [Fact]
    public void Source_discovery_supports_documented_globs_and_default_exclusions()
    {
        using var temp = TempProject();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Sources", "Nested"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Sources", "Generated"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "bin"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "obj"));
        Directory.CreateDirectory(Path.Combine(temp.Path, ".martin"));
        File.WriteAllText(Path.Combine(temp.Path, "Sources", "main.martin"), "func main() {}\n");
        File.WriteAllText(Path.Combine(temp.Path, "Sources", "Nested", "helper.martin"), "func helper() {}\n");
        File.WriteAllText(Path.Combine(temp.Path, "Sources", "Generated", "skip.martin"), "func skip() {}\n");
        File.WriteAllText(Path.Combine(temp.Path, "bin", "generated.martin"), "func generated() {}\n");
        File.WriteAllText(Path.Combine(temp.Path, "obj", "generated.martin"), "func generated() {}\n");
        File.WriteAllText(Path.Combine(temp.Path, ".martin", "state.martin"), "func state() {}\n");
        var diagnostics = System.Collections.Immutable.ImmutableArray.CreateBuilder<ProjectDiagnostic>();

        var files = new SourceDiscovery().Discover(
            temp.Path,
            ["Sources/**/*.martin", "Sources/Nested/helpe?.martin"],
            ["Sources/Generated/**", "bin/**", "obj/**"],
            reportInvalidIncludePatterns: true,
            diagnostics);

        Assert.Equal(2, files.Length);
        Assert.EndsWith(Path.Combine("Sources", "Nested", "helper.martin"), files[0]);
        Assert.EndsWith(Path.Combine("Sources", "main.martin"), files[1]);
    }

    [Fact]
    public void Source_discovery_normalizes_platform_separators()
    {
        using var temp = TempProject();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Sources", "Nested"));
        File.WriteAllText(Path.Combine(temp.Path, "Sources", "Nested", "main.martin"), "func main() {}\n");
        var diagnostics = System.Collections.Immutable.ImmutableArray.CreateBuilder<ProjectDiagnostic>();

        var files = new SourceDiscovery().Discover(
            temp.Path,
            [@"Sources\Nested\m?in.martin"],
            [],
            reportInvalidIncludePatterns: true,
            diagnostics);

        Assert.Single(files);
        Assert.EndsWith(Path.Combine("Sources", "Nested", "main.martin"), files[0]);
    }


    [Fact]
    public void Project_creator_creates_complete_project_that_loads()
    {
        using var temp = TempProject();

        var result = new MartinProjectCreator().Create(new MartinProjectCreationOptions
        {
            ProjectName = "HelloMartin",
            BasePath = temp.Path,
            CreateGitIgnore = true
        });

        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.True(File.Exists(Path.Combine(result.ProjectDirectory!, "Martin.toml")));
        Assert.True(File.Exists(Path.Combine(result.ProjectDirectory!, "Sources", "main.martin")));
        Assert.True(File.Exists(Path.Combine(result.ProjectDirectory!, ".gitignore")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(temp.Path, ".HelloMartin.staging-*"));

        var loaded = MartinProjectLoader.Load(new ProjectLoadOptions { ProjectPath = result.ProjectDirectory });
        Assert.True(loaded.Success, string.Join("\n", loaded.Diagnostics.Select(d => d.Message)));
        Assert.Equal("HelloMartin", loaded.Project!.Manifest.Package.Name);
    }

    [Fact]
    public void Project_creator_preserves_existing_content_and_leaves_no_staging_directory()
    {
        using var temp = TempProject();
        var destination = Path.Combine(temp.Path, "HelloMartin");
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "keep.txt"), "user content");

        var result = new MartinProjectCreator().Create(new MartinProjectCreationOptions
        {
            ProjectName = "HelloMartin",
            BasePath = temp.Path,
            Force = true
        });

        Assert.False(result.Success);
        Assert.True(File.Exists(Path.Combine(destination, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(destination, "Martin.toml")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(temp.Path, ".HelloMartin.staging-*"));
    }


    [Fact]
    public void Clean_removes_output_and_intermediate_and_missing_targets_succeed()
    {
        using var temp = TempProject();
        var bin = Path.Combine(temp.Path, "bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "app.dll"), "binary");

        var result = new MartinProjectCleaner().Clean(new ProjectCleanOptions
        {
            ProjectRoot = temp.Path,
            TargetPaths = ["bin", "obj"],
        });

        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.False(Directory.Exists(bin));
    }

    [Fact]
    public void Clean_dry_run_deletes_nothing_and_returns_plan()
    {
        using var temp = TempProject();
        var bin = Path.Combine(temp.Path, "bin");
        Directory.CreateDirectory(bin);

        var result = new MartinProjectCleaner().Clean(new ProjectCleanOptions
        {
            ProjectRoot = temp.Path,
            TargetPaths = ["bin"],
            DryRun = true,
        });

        Assert.True(result.Success);
        Assert.True(Directory.Exists(bin));
        Assert.Contains(Path.GetFullPath(bin), result.Plan.Directories);
    }

    [Fact]
    public void Clean_rejects_project_root_filesystem_root_and_escaping_paths_before_deleting_anything()
    {
        using var temp = TempProject();
        var safe = Path.Combine(temp.Path, "bin");
        Directory.CreateDirectory(safe);
        File.WriteAllText(Path.Combine(safe, "keep.txt"), "safe");

        var plan = new MartinProjectCleaner().Plan(new ProjectCleanOptions
        {
            ProjectRoot = temp.Path,
            TargetPaths = ["bin", ".", "..", Path.GetPathRoot(temp.Path)!],
        });

        Assert.False(plan.CanExecute);
        Assert.Contains(plan.Diagnostics, diagnostic => diagnostic.Code == "MRT4513");

        var result = new MartinProjectCleaner().Clean(new ProjectCleanOptions
        {
            ProjectRoot = temp.Path,
            TargetPaths = ["bin", "."],
        });

        Assert.False(result.Success);
        Assert.True(Directory.Exists(safe));
    }



    [Fact]
    public void Clean_rejects_reparse_point_targets()
    {
        if (OperatingSystem.IsWindows()) return;
        using var temp = TempProject();
        var outside = Path.Combine(System.IO.Path.GetTempPath(), "MartinTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            var link = Path.Combine(temp.Path, "bin");
            Directory.CreateSymbolicLink(link, outside);

            var result = new MartinProjectCleaner().Clean(new ProjectCleanOptions
            {
                ProjectRoot = temp.Path,
                TargetPaths = ["bin"],
            });

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MRT4513");
            Assert.True(Directory.Exists(outside));
        }
        finally
        {
            if (Directory.Exists(outside)) Directory.Delete(outside, true);
        }
    }


    static void WriteManifest(string root, string include = "Sources/**/*.martin", string kind = "executable") => File.WriteAllText(Path.Combine(root, "Martin.toml"), $"""
manifest-version = 1

[package]
name = "HelloMartin"
version = "0.1.0"

[target]
kind = "{kind}"
framework = "net8.0"
entry = "main"

[sources]
include = ["{include}"]
exclude = ["bin/**", "obj/**", ".martin/**"]
""");

    static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "README.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    static TempDir TempProject() => new();
    sealed class TempDir : IDisposable { public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MartinTests", Guid.NewGuid().ToString("N")); public TempDir() => Directory.CreateDirectory(Path); public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); } }
}
