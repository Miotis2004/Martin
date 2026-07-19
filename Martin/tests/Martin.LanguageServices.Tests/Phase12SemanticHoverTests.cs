using Martin.Compiler.Text;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase12SemanticHoverTests
{
    [Fact]
    public void Hover_uses_bound_symbol_and_exact_identifier_span()
    {
        const string source = "func main() { let value: Int = 1 print(value) }";
        var (service, workspace, project, document) = Workspace(source);
        var use = source.LastIndexOf("value", StringComparison.Ordinal);

        var hover = Assert.IsType<HoverInfo>(service.Hover(workspace, document.Id, use + 2));

        Assert.Equal(new TextSpan(use, "value".Length), hover.Span);
        Assert.Contains("let value: Int", hover.Markdown);
        Assert.Contains("LocalVariable", hover.Markdown);
        Assert.Null(service.Hover(workspace, document.Id, source.IndexOf("main", StringComparison.Ordinal) + 20));
    }

    [Fact]
    public void Hover_formats_selected_callable_throwing_type_and_documentation()
    {
        const string source = "/// Loads text.\n/// - Parameter path: File to load.\n/// - Returns: Loaded text.\n/// - Throws: When the file is unavailable.\nfunc load(_ path: String) throws Error -> String { return path }\nfunc main() { load(\"a\") }";
        var (service, workspace, _, document) = Workspace(source);

        var hover = Assert.IsType<HoverInfo>(service.Hover(workspace, document.Id, source.LastIndexOf("load", StringComparison.Ordinal)));

        Assert.Contains("func load(_ path: String) throws Error -> String", hover.Markdown);
        Assert.Contains("Loads text.", hover.Markdown);
        Assert.Contains("**Parameters**", hover.Markdown);
        Assert.Contains("`path`: File to load.", hover.Markdown);
        Assert.Contains("**Returns**", hover.Markdown);
        Assert.Contains("**Throws**", hover.Markdown);
    }

    [Fact]
    public void Documentation_renderer_escapes_html_from_source_comments()
    {
        const string source = "/// <script>alert(1)</script>\nfunc safe() {}\nfunc main() { safe() }";
        var (service, workspace, _, document) = Workspace(source);

        var markdown = service.Hover(workspace, document.Id, source.LastIndexOf("safe", StringComparison.Ordinal))!.Markdown;

        Assert.DoesNotContain("<script>", markdown);
        Assert.Contains("&lt;script&gt;", markdown);
    }

    [Fact]
    public async Task Versioned_hover_rejects_stale_requests()
    {
        var (service, workspace, project, document) = Workspace("func answer() -> Int { return 42 } func main() { answer() }");
        var request = Request(workspace, project, document, document.Text.LastIndexOf("answer", StringComparison.Ordinal)) with {
            DocumentVersion = new DocumentVersion(99)
        };

        Assert.True((await service.HoverAsync(workspace, request)).IsStale);
        var current = await service.HoverAsync(workspace, request with { DocumentVersion = document.Version });
        Assert.False(current.IsStale);
        Assert.NotNull(current.Value);
    }

    [Fact]
    public void Built_in_hover_has_canonical_signature_and_documentation()
    {
        const string source = "func main() { print(1) }";
        var (service, workspace, _, document) = Workspace(source);

        var hover = Assert.IsType<HoverInfo>(service.Hover(workspace, document.Id, source.IndexOf("print", StringComparison.Ordinal)));

        Assert.Contains("func print", hover.Markdown);
        Assert.Contains("Writes a value to standard output.", hover.Markdown);
    }

    static HoverRequest Request(LanguageWorkspaceSnapshot workspace, LanguageProjectSnapshot project,
                                LanguageDocumentSnapshot document, int position) => new() {
        WorkspaceId = workspace.Id,
        WorkspaceVersion = workspace.Version,
        ProjectId = project.Id,
        ProjectVersion = project.Version,
        DocumentId = document.Id,
        DocumentVersion = document.Version,
        Position = position
    };

    static (MartinLanguageService Service, LanguageWorkspaceSnapshot Workspace, LanguageProjectSnapshot Project,
            LanguageDocumentSnapshot Document) Workspace(string source)
    {
        var document = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "main.martin", source, new(0));
        var project = new LanguageProjectSnapshot(ProjectId.CreateNew(), "test", ".", new(0), [document]);
        return (new MartinLanguageService(), new LanguageWorkspaceSnapshot(WorkspaceId.CreateNew(), new(0), [project]), project, document);
    }
}
