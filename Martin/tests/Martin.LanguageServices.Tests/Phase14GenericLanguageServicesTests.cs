using Martin.Compiler.Text;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase14GenericLanguageServicesTests
{
    [Fact]
    public void Completion_filters_constraints_and_substitutes_constructed_members()
    {
        const string source = "protocol Printable { } struct Concrete { } struct Box<T: Prin> { let value: T } func main() { let box = Box<Int>(value: 1) box. }";
        var (service, workspace, document, _) = Workspace(source);

        var constraint = service.Complete(workspace, document.Id, source.IndexOf("Prin", StringComparison.Ordinal) + 4);
        Assert.Contains(constraint, item => item.Label == "Printable");
        Assert.DoesNotContain(constraint, item => item.Label == "Concrete");

        var members = service.Complete(workspace, document.Id, source.LastIndexOf("box.", StringComparison.Ordinal) + 4);
        var value = Assert.Single(members, item => item.Label == "value");
        Assert.Contains("Int", value.Detail);
    }

    [Fact]
    public void Hover_reports_inferred_arguments_and_constructed_signature()
    {
        const string source = "func identity<T>(_ value: T) -> T { return value } func main() { let answer = identity(42) }";
        var (service, workspace, document, _) = Workspace(source);

        var hover = service.Hover(workspace, document.Id, source.LastIndexOf("identity", StringComparison.Ordinal));

        Assert.NotNull(hover);
        Assert.Contains("(_ value: Int)", hover.Markdown);
        Assert.Contains("Type arguments (inferred)", hover.Markdown);
        Assert.Contains("`Int`", hover.Markdown);
    }

    [Fact]
    public void Signature_help_uses_recursive_substituted_types()
    {
        const string source = "struct Box<T> { let value: T } func unwrap<T>(_ box: Box<T?>) -> T? { return nil } func main() { unwrap(Box<Int?>(value: nil)) }";
        var (service, workspace, document, _) = Workspace(source);
        var position = source.LastIndexOf("Box<Int", StringComparison.Ordinal);

        var help = service.GetSignatureHelp(workspace, document.Id, position);

        Assert.NotNull(help);
        Assert.Contains(help.Signatures, signature => signature.Label.Contains("Box<Int?>", StringComparison.Ordinal));
        Assert.Contains(help.Signatures, signature => signature.ReturnType == "Int?");
    }

    [Fact]
    public async Task Generic_navigation_uses_original_definition_identity_and_rejects_stale_requests()
    {
        const string source = "struct Box<T> { let value: T } func main() { let box = Box<Int>(value: 1) let answer = box.value }";
        var (service, workspace, document, project) = Workspace(source);
        var use = source.LastIndexOf("value", StringComparison.Ordinal);
        var request = new DefinitionRequest { WorkspaceId = workspace.Id, WorkspaceVersion = workspace.Version,
            ProjectId = project.Id, ProjectVersion = project.Version, DocumentId = document.Id,
            DocumentVersion = document.Version, Position = use };

        var current = await service.GetDefinitionsAsync(workspace, request);
        Assert.False(current.IsStale);
        Assert.Contains(current.Value, location => location.Span == new TextSpan(source.IndexOf("value", StringComparison.Ordinal), 5));

        var stale = await service.GetDefinitionsAsync(workspace, request with { DocumentVersion = new(99) });
        Assert.True(stale.IsStale);
        Assert.Empty(stale.Value);
    }

    [Fact]
    public async Task Type_parameters_are_semantically_classified()
    {
        const string source = "struct Box<T> { let value: T }";
        var (service, workspace, document, project) = Workspace(source);
        var request = new ClassificationRequest { WorkspaceId = workspace.Id, WorkspaceVersion = workspace.Version,
            ProjectId = project.Id, ProjectVersion = project.Version, DocumentId = document.Id, DocumentVersion = document.Version };

        var classifications = await service.GetClassificationsAsync(workspace, request);

        Assert.False(classifications.IsStale);
        Assert.Equal(2, classifications.Value.Count(span => span.Kind == ClassificationKind.TypeParameter));
    }

    private static (MartinLanguageService Service, LanguageWorkspaceSnapshot Workspace,
        LanguageDocumentSnapshot Document, LanguageProjectSnapshot Project) Workspace(string text)
    {
        var document = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "main.martin", text, new(0));
        var project = new LanguageProjectSnapshot(ProjectId.CreateNew(), "test", ".", new(0), [document]);
        return (new(), new(WorkspaceId.CreateNew(), new(0), [project]), document, project);
    }
}
