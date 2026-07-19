namespace Martin.Studio.Core;

public sealed record EditorAssetValidationIssue(string RelativePath, string Message);

public sealed record EditorAssetValidationResult(string RootPath, IReadOnlyList<EditorAssetValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public static class EditorAssetValidator
{
    public const string MonacoVersion = "0.52.2";

    public static readonly IReadOnlyList<string> RequiredRelativePaths =
    [
        "index.html",
        "editor.js",
        "editor.css",
        "martin-language.js",
        Path.Combine("monaco", "VERSION.txt"),
        Path.Combine("monaco", "min", "vs", "loader.js"),
        Path.Combine("themes", "martin-dark.json"),
        Path.Combine("themes", "martin-light.json"),
        Path.Combine("licenses", "MONACO-EDITOR.txt"),
        Path.Combine("licenses", "THIRD-PARTY-NOTICES.txt")
    ];

    public static EditorAssetValidationResult Validate(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var fullRoot = Path.GetFullPath(rootPath);
        var issues = new List<EditorAssetValidationIssue>();

        if (!Directory.Exists(fullRoot))
        {
            issues.Add(new EditorAssetValidationIssue(".", "Editor asset root is missing."));
            return new EditorAssetValidationResult(fullRoot, issues);
        }

        foreach (var relativePath in RequiredRelativePaths)
        {
            var fullPath = Path.Combine(fullRoot, relativePath);
            if (!File.Exists(fullPath))
            {
                issues.Add(new EditorAssetValidationIssue(relativePath, "Required editor asset is missing."));
            }
        }

        var versionFile = Path.Combine(fullRoot, "monaco", "VERSION.txt");
        if (File.Exists(versionFile))
        {
            var versionText = File.ReadAllText(versionFile);
            if (!versionText.Contains(MonacoVersion, StringComparison.Ordinal))
            {
                issues.Add(new EditorAssetValidationIssue(Path.Combine("monaco", "VERSION.txt"), $"Expected Monaco version {MonacoVersion}."));
            }
        }

        return new EditorAssetValidationResult(fullRoot, issues);
    }
}
