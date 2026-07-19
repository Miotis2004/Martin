using Martin.Compiler.Text;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase12SemanticCompletionTests
{
    [Fact]
    public void Completion_is_scope_aware_and_replaces_only_the_identifier_fragment()
    {
        var (service, workspace, document, _) = Workspace("func main() { let visible = 1 vis }\nfunc other() { let hidden = 2 }");
        var position = document.Text.IndexOf("vis }", StringComparison.Ordinal) + 3;

        var items = service.Complete(workspace, document.Id, position);

        var visible = Assert.Single(items, item => item.Label == "visible");
        Assert.Equal(new TextSpan(position - 3, 3), visible.TextEdit.Span);
        Assert.DoesNotContain(items, item => item.Label == "hidden");
        Assert.NotNull(visible.SymbolId);
    }

    [Fact]
    public void Completion_uses_receiver_type_for_members()
    {
        const string source = "struct Point { let distance: Int func move(_ x: Int) -> Int { return x } } func main() { let p = Point() p.di }";
        var (service, workspace, document, _) = Workspace(source);
        var position = source.IndexOf("p.di", StringComparison.Ordinal) + 4;

        var items = service.Complete(workspace, document.Id, position);

        Assert.Contains(items, item => item.Label == "distance" && item.ItemKind == CompletionItemKind.Property);
        Assert.All(items, item => Assert.Equal(position - 2, item.TextEdit.Span.Start));
    }

    [Fact]
    public void Completion_is_suppressed_in_comments_and_strings()
    {
        var (service, workspace, document, _) = Workspace("func main() { // pri\n let text = \"pri\" }");
        Assert.Empty(service.Complete(workspace, document.Id, document.Text.IndexOf("pri", StringComparison.Ordinal) + 3));
        Assert.Empty(service.Complete(workspace, document.Id, document.Text.LastIndexOf("pri", StringComparison.Ordinal) + 3));
    }

    [Fact]
    public async Task Versioned_completion_rejects_stale_requests_and_honors_limits()
    {
        var (service, workspace, document, project) = Workspace("func first() {} func second() {}");
        var request = new CompletionRequest { WorkspaceId = workspace.Id, WorkspaceVersion = workspace.Version,
            ProjectId = project.Id, ProjectVersion = project.Version, DocumentId = document.Id,
            DocumentVersion = new DocumentVersion(99), Position = 0, MaximumResults = 1 };
        Assert.True((await service.CompleteAsync(workspace, request)).IsStale);

        request = request with { DocumentVersion = document.Version };
        var current = await service.CompleteAsync(workspace, request);
        Assert.False(current.IsStale);
        Assert.Single(current.Value);
    }

    private static (MartinLanguageService Service, LanguageWorkspaceSnapshot Workspace, LanguageDocumentSnapshot Document, LanguageProjectSnapshot Project) Workspace(string text)
    {
        var document = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "main.martin", text, new(0));
        var project = new LanguageProjectSnapshot(ProjectId.CreateNew(), "test", ".", new(0), [document]);
        return (new MartinLanguageService(), new LanguageWorkspaceSnapshot(WorkspaceId.CreateNew(), new(0), [project]), document, project);
    }
}
