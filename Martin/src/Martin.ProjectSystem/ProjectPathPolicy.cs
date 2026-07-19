namespace Martin.ProjectSystem;

public sealed class ProjectPathPolicy
{
    public static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public bool IsRootEscapingPattern(string pattern) =>
        Path.IsPathRooted(pattern) ||
        pattern.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == "..");

    public static bool IsContainedPath(string root, string candidate, StringComparison comparison)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullCandidate = Path.GetFullPath(candidate);
        var relative = Path.GetRelativePath(fullRoot, fullCandidate);

        if (Path.IsPathRooted(relative))
            return false;
        if (relative == ".")
            return false;

        return relative != ".." &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, comparison) &&
               !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, comparison);
    }
}
