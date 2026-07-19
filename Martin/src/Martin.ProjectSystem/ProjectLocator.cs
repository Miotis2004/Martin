using System.Collections.Immutable;

namespace Martin.ProjectSystem;

public sealed class ProjectLocator
{
    public string? ResolveManifest(ProjectLoadOptions options, ImmutableArray<ProjectDiagnostic>.Builder diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(options.ProjectPath) && !string.IsNullOrWhiteSpace(options.ManifestPath))
        {
            diagnostics.Add(ProjectDiagnostics.Error("MRT4502", "Project path cannot be used with --manifest-path."));
            return null;
        }

        if (!string.IsNullOrWhiteSpace(options.ManifestPath))
        {
            return CheckManifest(Path.GetFullPath(options.ManifestPath), diagnostics);
        }

        if (!string.IsNullOrWhiteSpace(options.ProjectPath))
        {
            var projectPath = Path.GetFullPath(options.ProjectPath);
            return File.Exists(projectPath)
                       ? CheckManifest(projectPath, diagnostics)
                       : CheckManifest(Path.Combine(projectPath, "Martin.toml"), diagnostics);
        }

        var directory = Path.GetFullPath(options.WorkingDirectory ?? Environment.CurrentDirectory);
        while (directory is not null)
        {
            var manifestPath = Path.Combine(directory, "Martin.toml");
            if (File.Exists(manifestPath))
            {
                return manifestPath;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        diagnostics.Add(ProjectDiagnostics.Error("MRT4001", "Martin.toml was not found."));
        return null;
    }

    static string? CheckManifest(string path, ImmutableArray<ProjectDiagnostic>.Builder diagnostics)
    {
        if (File.Exists(path))
        {
            return Path.GetFullPath(path);
        }

        diagnostics.Add(ProjectDiagnostics.Error("MRT4001", "Martin.toml was not found.", path));
        return null;
    }
}
