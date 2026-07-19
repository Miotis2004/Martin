using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Martin.ProjectSystem;

public sealed class SourceDiscovery
{
    readonly ProjectPathPolicy _pathPolicy;

    public SourceDiscovery(ProjectPathPolicy? pathPolicy = null) =>
        _pathPolicy = pathPolicy ?? new ProjectPathPolicy();

    public ImmutableArray<string> Discover(
        string rootDirectory,
        ImmutableArray<string> includePatterns,
        ImmutableArray<string> excludePatterns,
        bool reportInvalidIncludePatterns,
        ImmutableArray<ProjectDiagnostic>.Builder diagnostics,
        CancellationToken cancellationToken = default)
    {
        var rootFullPath = Path.GetFullPath(rootDirectory);
        var patterns = NormalizePatterns(includePatterns);
        var excludes = NormalizePatterns(excludePatterns.Add(".martin/**"));
        var discovered = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var pattern in patterns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_pathPolicy.IsRootEscapingPattern(pattern))
            {
                if (reportInvalidIncludePatterns)
                {
                    diagnostics.Add(ProjectDiagnostics.Error("MRT4007", $"Source path '{pattern}' escapes the project root."));
                }

                continue;
            }

            foreach (var (relativePath, fullPath) in EnumerateMartinFiles(rootFullPath, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Matches(relativePath, pattern) && !excludes.Any(exclude => Matches(relativePath, exclude)))
                {
                    discovered.TryAdd(relativePath, fullPath);
                }
            }
        }

        return discovered.Values.ToImmutableArray();
    }

    static ImmutableArray<string> NormalizePatterns(ImmutableArray<string> patterns) =>
        patterns.Select(NormalizePattern).ToImmutableArray();

    static string NormalizePattern(string pattern) =>
        pattern.Replace('\\', '/');

    static IEnumerable<(string RelativePath, string FullPath)> EnumerateMartinFiles(string rootDirectory, CancellationToken cancellationToken)
    {
        var options = new EnumerationOptions {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var file in Directory.EnumerateFiles(rootDirectory, "*.martin", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(file);
            var relativePath = Path.GetRelativePath(rootDirectory, fullPath).Replace('\\', '/');
            yield return (relativePath, fullPath);
        }
    }

    static bool Matches(string relativePath, string pattern) =>
        GlobRegexCache.GetOrAdd(pattern).IsMatch(relativePath);

    static class GlobRegexCache
    {
        static readonly Dictionary<string, Regex> Regexes = new(StringComparer.Ordinal);

        public static Regex GetOrAdd(string pattern)
        {
            lock (Regexes)
            {
                if (!Regexes.TryGetValue(pattern, out var regex))
                {
                    regex = new Regex(ToRegex(pattern), RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
                    Regexes.Add(pattern, regex);
                }

                return regex;
            }
        }

        static string ToRegex(string pattern)
        {
            var builder = new System.Text.StringBuilder("^");
            for (var i = 0; i < pattern.Length; i++)
            {
                var current = pattern[i];
                if (current == '*')
                {
                    var isDoubleStar = i + 1 < pattern.Length && pattern[i + 1] == '*';
                    if (isDoubleStar)
                    {
                        var followedBySlash = i + 2 < pattern.Length && pattern[i + 2] == '/';
                        builder.Append(followedBySlash ? "(?:.*/)?" : ".*");
                        i += followedBySlash ? 2 : 1;
                    }
                    else
                    {
                        builder.Append("[^/]*");
                    }
                }
                else if (current == '?')
                {
                    builder.Append("[^/]");
                }
                else if (current == '/')
                {
                    builder.Append('/');
                }
                else
                {
                    builder.Append(Regex.Escape(current.ToString()));
                }
            }

            builder.Append('$');
            return builder.ToString();
        }
    }
}
