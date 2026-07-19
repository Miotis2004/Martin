using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace Martin.ProjectSystem;

public sealed record MartinProjectCreationOptions
{
    public required string ProjectName { get; init; }
    public string? BasePath { get; init; }
    public bool Force { get; init; }
    public bool CreateGitIgnore { get; init; } = true;
    public string TargetFramework { get; init; } = "net8.0";
}

public sealed record MartinProjectCreationResult
{
    public string? ProjectDirectory { get; init; }
    public ImmutableArray<ProjectDiagnostic> Diagnostics { get; init; } = [];
    public bool Success => ProjectDirectory is not null && Diagnostics.All(d => d.Severity != ProjectDiagnosticSeverity.Error);
}

public sealed class MartinProjectCreator
{
    static readonly Regex ProjectNamePattern = new("^[A-Za-z_][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    public MartinProjectCreationResult Create(MartinProjectCreationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var diagnostics = ImmutableArray.CreateBuilder<ProjectDiagnostic>();

        if (!ProjectNamePattern.IsMatch(options.ProjectName))
        {
            diagnostics.Add(ProjectDiagnostics.Error("MRT4506", $"Project name '{options.ProjectName}' is invalid."));
            return new() { Diagnostics = diagnostics.ToImmutable() };
        }

        var basePath = Path.GetFullPath(options.BasePath ?? Environment.CurrentDirectory);
        var destination = Path.GetFullPath(Path.Combine(basePath, options.ProjectName));
        var staging = Path.Combine(basePath, $".{options.ProjectName}.staging-{Guid.NewGuid():N}");

        if (!IsChildOf(destination, basePath))
        {
            diagnostics.Add(ProjectDiagnostics.Error("MRT4506", $"Project destination '{destination}' must be inside '{basePath}'."));
            return new() { Diagnostics = diagnostics.ToImmutable() };
        }

        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
        {
            diagnostics.Add(ProjectDiagnostics.Error("MRT4505", $"Project directory '{destination}' is not empty."));
            return new() { Diagnostics = diagnostics.ToImmutable() };
        }

        if (File.Exists(destination))
        {
            diagnostics.Add(ProjectDiagnostics.Error("MRT4505", $"Project destination '{destination}' already exists as a file."));
            return new() { Diagnostics = diagnostics.ToImmutable() };
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(staging);
            WriteScaffold(staging, options, cancellationToken);

            var loaded = MartinProjectLoader.Load(new ProjectLoadOptions { ProjectPath = staging });
            diagnostics.AddRange(loaded.Diagnostics);
            if (!loaded.Success)
            {
                return new() { Diagnostics = diagnostics.ToImmutable() };
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(destination))
            {
                if (Directory.EnumerateFileSystemEntries(destination).Any())
                {
                    diagnostics.Add(ProjectDiagnostics.Error("MRT4505", $"Project directory '{destination}' is not empty."));
                    return new() { Diagnostics = diagnostics.ToImmutable() };
                }

                Directory.Delete(destination);
            }

            Directory.Move(staging, destination);
            staging = string.Empty;
            return new() { ProjectDirectory = destination, Diagnostics = diagnostics.ToImmutable() };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            diagnostics.Add(ProjectDiagnostics.Error("MRT4511", $"Project creation failed: {ex.Message}", destination));
            return new() { Diagnostics = diagnostics.ToImmutable() };
        }
        finally
        {
            if (!string.IsNullOrEmpty(staging) && Directory.Exists(staging))
            {
                try { Directory.Delete(staging, recursive: true); }
                catch { }
            }
        }
    }

    static void WriteScaffold(string root, MartinProjectCreationOptions options, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.Combine(root, "Sources"));
        Directory.CreateDirectory(Path.Combine(root, "Tests"));
        AtomicWrite(Path.Combine(root, "Martin.toml"), ManifestText(options.ProjectName, options.TargetFramework), cancellationToken);
        AtomicWrite(Path.Combine(root, "Sources", "main.martin"), "func main() {\n    print(\"Hello from Martin!\")\n}\n", cancellationToken);
        if (options.CreateGitIgnore)
        {
            AtomicWrite(Path.Combine(root, ".gitignore"), "bin/\nobj/\n.martin/\n", cancellationToken);
        }

        AtomicWrite(Path.Combine(root, "README.md"), $"# {options.ProjectName}\n\nA Martin project.\n", cancellationToken);
    }

    static void AtomicWrite(string path, string contents, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var temp = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temp, contents, new UTF8Encoding(false));
        File.Move(temp, path, overwrite: false);
    }

    static string ManifestText(string name, string framework) => $"manifest-version = 1\n\n[package]\nname = \"{name}\"\nversion = \"0.1.0\"\n\n[target]\nkind = \"executable\"\nframework = \"{framework}\"\nentry = \"main\"\n\n[sources]\ninclude = [\"Sources/**/*.martin\"]\nexclude = [\"bin/**\", \"obj/**\", \".martin/**\"]\n\n[build]\noutput = \"bin\"\nintermediate = \"obj\"\n\n[tests]\ninclude = [\"Tests/**/*.martin\"]\n";

    static bool IsChildOf(string path, string parent)
    {
        var relative = Path.GetRelativePath(parent, path);
        return relative != "." && !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }
}
