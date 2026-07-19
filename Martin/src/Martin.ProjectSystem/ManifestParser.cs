using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Tomlyn;
using Tomlyn.Model;

namespace Martin.ProjectSystem;

public sealed class ManifestParser
{
    public const int SupportedManifestVersion = 1;

    static readonly HashSet<string> KnownTopLevelKeys = new(StringComparer.OrdinalIgnoreCase) { "manifest-version" };
    static readonly HashSet<string> KnownSections = new(StringComparer.OrdinalIgnoreCase) { "package", "target", "sources", "build", "tests" };
    static readonly Dictionary<string, HashSet<string>> KnownSectionKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["package"] = new(StringComparer.OrdinalIgnoreCase) { "name", "version" },
        ["target"] = new(StringComparer.OrdinalIgnoreCase) { "kind", "framework", "entry" },
        ["sources"] = new(StringComparer.OrdinalIgnoreCase) { "include", "exclude" },
        ["build"] = new(StringComparer.OrdinalIgnoreCase) { "output", "intermediate" },
        ["tests"] = new(StringComparer.OrdinalIgnoreCase) { "include" }
    };

    readonly ProjectPathPolicy _pathPolicy = new();

    public MartinManifest? Parse(string path, ImmutableArray<ProjectDiagnostic>.Builder diagnostics, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = File.ReadAllText(path);
            var model = new TomlManifestParser().Parse(path, text, diagnostics, cancellationToken);
            if (model is null) return null;

            cancellationToken.ThrowIfCancellationRequested();
            ValidateUnknownKeys(model, diagnostics, path);

            var manifestVersion = RequiredInt(model, model.Top, "manifest-version", diagnostics, path);
            if (manifestVersion is null) return null;
            if (manifestVersion.Value != SupportedManifestVersion)
            {
                diagnostics.Add(ProjectDiagnostics.Error("MRT4005", $"Manifest schema version '{manifestVersion.Value}' is not supported.", path));
                return null;
            }

            var package = RequiredSection(model, model.Sections, "package", diagnostics, path);
            var target = RequiredSection(model, model.Sections, "target", diagnostics, path);
            if (package is null || target is null) return null;

            var nameValue = RequiredString(model, package, "package.name", diagnostics, path);
            var versionValue = RequiredString(model, package, "package.version", diagnostics, path);
            var kind = RequiredString(model, target, "target.kind", diagnostics, path);
            var framework = RequiredString(model, target, "target.framework", diagnostics, path);
            var entry = RequiredString(model, target, "target.entry", diagnostics, path);

            var sourceInclude = OptionalStringArray(model, model.Sections, "sources", "include", ["Sources/**/*.martin"], diagnostics, path);
            var sourceExclude = OptionalStringArray(model, model.Sections, "sources", "exclude", ["bin/**", "obj/**", ".martin/**"], diagnostics, path);
            var buildOutput = OptionalString(model, model.Sections.GetValueOrDefault("build"), "build.output", "bin", diagnostics, path);
            var buildIntermediate = OptionalString(model, model.Sections.GetValueOrDefault("build"), "build.intermediate", "obj", diagnostics, path);
            var testInclude = OptionalStringArray(model, model.Sections, "tests", "include", ["Tests/**/*.martin"], diagnostics, path);

            if (nameValue is not null && versionValue is not null && kind is not null && framework is not null && entry is not null)
                ValidateManifestValues(nameValue, versionValue, kind, framework, entry, diagnostics, path);
            if (buildOutput is not null) ValidatePath("build.output", buildOutput, diagnostics, path);
            if (buildIntermediate is not null) ValidatePath("build.intermediate", buildIntermediate, diagnostics, path);
            foreach (var pattern in sourceInclude.Concat(sourceExclude).Concat(testInclude)) ValidateGlob(pattern, diagnostics, path);

            if (diagnostics.Any(d => d.Severity == ProjectDiagnosticSeverity.Error)) return null;

            return new MartinManifest
            {
                ManifestVersion = manifestVersion.Value,
                Package = new(nameValue!, versionValue!),
                Target = new(kind!, framework!, entry!),
                Sources = new() { Include = sourceInclude, Exclude = sourceExclude },
                Build = new() { Output = buildOutput!, Intermediate = buildIntermediate! },
                Tests = new() { Include = testInclude }
            };
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            diagnostics.Add(ProjectDiagnostics.Error("MRT4002", "Martin.toml could not be parsed.", path));
            return null;
        }
    }

    static ProjectDiagnostic ErrorForKey(ParsedTomlManifest model, string key, string code, string message, string path)
    {
        var location = model.Locations.GetValueOrDefault(key);
        return ProjectDiagnostics.Error(code, message, path, location.Line, location.Column);
    }

    static ProjectDiagnostic WarningForKey(ParsedTomlManifest model, string key, string code, string message, string path)
    {
        var location = model.Locations.GetValueOrDefault(key);
        return ProjectDiagnostics.Warning(code, message, path, location.Line, location.Column);
    }

    static void ValidateUnknownKeys(ParsedTomlManifest model, ImmutableArray<ProjectDiagnostic>.Builder diagnostics, string path)
    {
        foreach (var key in model.Top.Keys.Where(key => !KnownTopLevelKeys.Contains(key)).OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
            diagnostics.Add(WarningForKey(model, key, "MRT4010", $"Unknown manifest key '{key}'.", path));

        foreach (var sectionName in model.Sections.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
        {
            if (!KnownSections.Contains(sectionName))
            {
                diagnostics.Add(WarningForKey(model, sectionName, "MRT4010", $"Unknown manifest section '{sectionName}'.", path));
                continue;
            }

            foreach (var key in model.Sections[sectionName].Keys.Where(key => !KnownSectionKeys[sectionName].Contains(key)).OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
                diagnostics.Add(WarningForKey(model, $"{sectionName}.{key}", "MRT4010", $"Unknown manifest key '{sectionName}.{key}'.", path));
        }
    }

    void ValidatePath(string key, string value, ImmutableArray<ProjectDiagnostic>.Builder diagnostics, string path)
    {
        if (string.IsNullOrWhiteSpace(value) || _pathPolicy.IsRootEscapingPattern(value))
            diagnostics.Add(ProjectDiagnostics.Error("MRT4011", $"Manifest path '{key}' is unsafe.", path));
    }

    void ValidateGlob(string pattern, ImmutableArray<ProjectDiagnostic>.Builder diagnostics, string path)
    {
        if (string.IsNullOrWhiteSpace(pattern) ||
            pattern.Contains('\0') ||
            pattern.Contains("***", StringComparison.Ordinal) ||
            pattern.IndexOfAny(['[', ']', '{', '}']) >= 0)
            diagnostics.Add(ProjectDiagnostics.Error("MRT4008", $"Glob pattern '{pattern}' is invalid.", path));
        else if (_pathPolicy.IsRootEscapingPattern(pattern))
            diagnostics.Add(ProjectDiagnostics.Error("MRT4007", $"Source path '{pattern}' escapes the project root.", path));
    }

    static void ValidateManifestValues(string name, string version, string kind, string framework, string entry, ImmutableArray<ProjectDiagnostic>.Builder diagnostics, string path)
    {
        if (!Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_.-]*$")) diagnostics.Add(ProjectDiagnostics.Error("MRT4013", $"Package name '{name}' is invalid.", path));
        if (!Regex.IsMatch(version, "^\\d+\\.\\d+\\.\\d+(?:[-+][0-9A-Za-z.-]+)?$")) diagnostics.Add(ProjectDiagnostics.Error("MRT4014", $"Package version '{version}' is invalid.", path));
        if (kind != "executable") diagnostics.Add(ProjectDiagnostics.Error("MRT4015", $"Target kind '{kind}' is not supported.", path));
        if (!Regex.IsMatch(framework, "^net(8|9|10)\\.0$")) diagnostics.Add(ProjectDiagnostics.Error("MRT4004", "Manifest value 'target.framework' is invalid.", path));
        if (string.IsNullOrWhiteSpace(entry) || !Regex.IsMatch(entry, "^[A-Za-z_][A-Za-z0-9_.]*$")) diagnostics.Add(ProjectDiagnostics.Error("MRT4004", "Manifest value 'target.entry' is invalid.", path));
    }

    static Dictionary<string, object>? RequiredSection(ParsedTomlManifest model, Dictionary<string, Dictionary<string, object>> sections, string key, ImmutableArray<ProjectDiagnostic>.Builder diagnostics, string path)
    {
        if (sections.TryGetValue(key, out var value)) return value;
        diagnostics.Add(ErrorForKey(model, key, "MRT4003", $"Required manifest section '{key}' is missing.", path));
        return null;
    }

    static int? RequiredInt(ParsedTomlManifest model, Dictionary<string, object> section, string key, ImmutableArray<ProjectDiagnostic>.Builder diagnostics, string path)
    {
        var localKey = key.Contains('.', StringComparison.Ordinal) ? key[(key.LastIndexOf('.') + 1)..] : key;
        if (!section.TryGetValue(localKey, out var value)) { diagnostics.Add(ErrorForKey(model, key, "MRT4003", $"Required manifest key '{key}' is missing.", path)); return null; }
        if (value is long number && number is >= int.MinValue and <= int.MaxValue) return (int)number;
        diagnostics.Add(ErrorForKey(model, key, "MRT4009", $"Manifest key '{key}' must be an integer.", path));
        return null;
    }

    static string? RequiredString(ParsedTomlManifest model, Dictionary<string, object> section, string key, ImmutableArray<ProjectDiagnostic>.Builder diagnostics, string path)
    {
        var localKey = key.Contains('.', StringComparison.Ordinal) ? key[(key.LastIndexOf('.') + 1)..] : key;
        if (!section.TryGetValue(localKey, out var value)) { diagnostics.Add(ErrorForKey(model, key, "MRT4003", $"Required manifest key '{key}' is missing.", path)); return null; }
        if (value is string stringValue) return stringValue;
        diagnostics.Add(ErrorForKey(model, key, "MRT4009", $"Manifest key '{key}' must be a string.", path));
        return null;
    }

    static string? OptionalString(ParsedTomlManifest model, Dictionary<string, object>? section, string key, string defaultValue, ImmutableArray<ProjectDiagnostic>.Builder diagnostics, string path)
    {
        var localKey = key.Contains('.', StringComparison.Ordinal) ? key[(key.LastIndexOf('.') + 1)..] : key;
        if (section is null || !section.TryGetValue(localKey, out var value)) return defaultValue;
        if (value is string stringValue) return stringValue;
        diagnostics.Add(ErrorForKey(model, key, "MRT4009", $"Manifest key '{key}' must be a string.", path));
        return null;
    }

    static ImmutableArray<string> OptionalStringArray(ParsedTomlManifest model, Dictionary<string, Dictionary<string, object>> sections, string section, string key, string[] defaultValue, ImmutableArray<ProjectDiagnostic>.Builder diagnostics, string path)
    {
        if (!sections.TryGetValue(section, out var map) || !map.TryGetValue(key, out var value)) return defaultValue.ToImmutableArray();
        if (value is TomlArray array && array.All(item => item is string)) return array.Cast<string>().ToImmutableArray();
        diagnostics.Add(ErrorForKey(model, $"{section}.{key}", "MRT4009", $"Manifest key '{section}.{key}' must be an array of strings.", path));
        return [];
    }
}

internal sealed class TomlManifestParser
{
    public ParsedTomlManifest? Parse(string path, string text, ImmutableArray<ProjectDiagnostic>.Builder diagnostics, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var document = Toml.Parse(text, path);
        if (document.HasErrors)
        {
            foreach (var diagnostic in document.Diagnostics)
            {
                var span = diagnostic.Span;
                diagnostics.Add(ProjectDiagnostics.Error("MRT4002", diagnostic.Message, path, span.Start.Line + 1, span.Start.Column + 1));
            }
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var table = document.ToModel();
        var top = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var sections = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        var locations = ManifestLocationScanner.Scan(text);

        foreach (var (key, value) in table)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value is TomlTable section)
                sections[key] = section.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            else
                top[key] = value;
        }

        return new ParsedTomlManifest(top, sections, locations);
    }
}

internal sealed record ParsedTomlManifest(
    Dictionary<string, object> Top,
    Dictionary<string, Dictionary<string, object>> Sections,
    Dictionary<string, (int? Line, int? Column)> Locations);

internal static class ManifestLocationScanner
{
    public static Dictionary<string, (int? Line, int? Column)> Scan(string text)
    {
        var locations = new Dictionary<string, (int? Line, int? Column)>(StringComparer.OrdinalIgnoreCase);
        var section = string.Empty;
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0 || trimmed[0] == '#') continue;
            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.Contains(']', StringComparison.Ordinal))
            {
                section = trimmed[1..trimmed.IndexOf(']')].Trim();
                locations.TryAdd(section, (index + 1, line.IndexOf('[') + 2));
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals < 0) continue;
            var rawKey = line[..equals].Trim();
            if (rawKey.Length == 0) continue;
            var fullKey = string.IsNullOrEmpty(section) ? rawKey : $"{section}.{rawKey}";
            locations.TryAdd(fullKey, (index + 1, line.IndexOf(rawKey, StringComparison.Ordinal) + 1));
        }

        return locations;
    }
}
