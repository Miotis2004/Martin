using System.Collections.Immutable;

namespace Martin.ProjectSystem;

public sealed record ProjectCleanOptions
{
    public required string ProjectRoot { get; init; }
    public required IEnumerable<string> TargetPaths { get; init; }
    public bool DryRun { get; init; }
}

public sealed record ProjectCleanPlan
{
    public ImmutableArray<string> Directories { get; init; } = [];
    public ImmutableArray<ProjectDiagnostic> Diagnostics { get; init; } = [];
    public bool CanExecute => Diagnostics.All(diagnostic => diagnostic.Severity != ProjectDiagnosticSeverity.Error);
}

public sealed record ProjectCleanResult
{
    public required ProjectCleanPlan Plan { get; init; }
    public ImmutableArray<ProjectDiagnostic> Diagnostics { get; init; } = [];
    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != ProjectDiagnosticSeverity.Error);
}

public sealed class MartinProjectCleaner
{
    public ProjectCleanPlan Plan(ProjectCleanOptions options)
    {
        var diagnostics = ImmutableArray.CreateBuilder<ProjectDiagnostic>();
        var directories = ImmutableArray.CreateBuilder<string>();
        var root = Path.GetFullPath(options.ProjectRoot);
        var comparison = ProjectPathPolicy.PathComparison;

        if (Path.GetPathRoot(root) == root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar)
        {
            diagnostics.Add(ProjectDiagnostics.Error("MRT4513", $"Project root '{root}' resolves to a filesystem root."));
            return new ProjectCleanPlan { Diagnostics = diagnostics.ToImmutable() };
        }

        foreach (var target in options.TargetPaths)
        {
            var candidate = Path.GetFullPath(Path.IsPathRooted(target) ? target : Path.Combine(root, target));
            if (!ProjectPathPolicy.IsContainedPath(root, candidate, comparison))
            {
                diagnostics.Add(ProjectDiagnostics.Error("MRT4513", $"Clean target '{target}' is outside the project root or is the project root.", candidate));
                continue;
            }

            var candidateRoot = Path.GetPathRoot(candidate);
            if (!string.IsNullOrEmpty(candidateRoot) && string.Equals(NormalizeRoot(candidateRoot), NormalizeRoot(candidate), comparison))
            {
                diagnostics.Add(ProjectDiagnostics.Error("MRT4513", $"Clean target '{target}' resolves to a filesystem root.", candidate));
                continue;
            }

            var reparse = FindReparsePoint(root, candidate, comparison);
            if (reparse is not null)
            {
                diagnostics.Add(ProjectDiagnostics.Error("MRT4513", $"Clean target '{target}' crosses reparse point '{reparse}'.", reparse));
                continue;
            }

            directories.Add(candidate);
        }

        return new ProjectCleanPlan
        {
            Directories = directories.ToImmutable(),
            Diagnostics = diagnostics.ToImmutable()
        };
    }

    public ProjectCleanResult Clean(ProjectCleanOptions options)
    {
        var plan = Plan(options);
        var diagnostics = ImmutableArray.CreateBuilder<ProjectDiagnostic>();
        diagnostics.AddRange(plan.Diagnostics);
        if (!plan.CanExecute || options.DryRun)
        {
            return new ProjectCleanResult { Plan = plan, Diagnostics = diagnostics.ToImmutable() };
        }

        foreach (var directory in plan.Directories.Distinct(StringComparerForPlatform()))
        {
            if (!Directory.Exists(directory)) continue;
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                diagnostics.Add(ProjectDiagnostics.Error("MRT4514", $"Failed to remove clean target '{directory}': {ex.Message}", directory));
            }
        }

        return new ProjectCleanResult { Plan = plan, Diagnostics = diagnostics.ToImmutable() };
    }

    static string? FindReparsePoint(string root, string candidate, StringComparison comparison)
    {
        var current = Path.GetFullPath(root);
        var relative = Path.GetRelativePath(root, candidate);
        foreach (var segment in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current) && !File.Exists(current)) continue;
            var attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) != 0) return current;
            if (string.Equals(current, candidate, comparison)) break;
        }

        return null;
    }

    static string NormalizeRoot(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    static StringComparer StringComparerForPlatform() => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
