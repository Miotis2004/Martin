using Martin.Compiler.Text;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase13ProtocolLanguageServicesTests
{
    [Fact]
    public void Conformance_completion_only_offers_unlisted_protocols()
    {
        const string source = "protocol Named { let name: String }\nprotocol Saved { func save() -> Void }\nstruct User: Named, Sa { let name: String }";
        var (service, workspace, _, document) = Create(source);
        var position = source.IndexOf("Sa {", StringComparison.Ordinal) + 2;

        var items = service.Complete(workspace, document.Id, position);

        Assert.Contains(items, item => item.Label == "Saved" && item.Kind == CompletionItemKind.Type);
        Assert.DoesNotContain(items, item => item.Label == "Named");
        Assert.DoesNotContain(items, item => item.Label == "String");
    }

    [Fact]
    public void Missing_requirement_completion_inserts_exact_method_and_property_stubs()
    {
        const string source = "protocol Model { let title: String func load(_ id: Int) -> String }\nstruct User: Model {\n    \n}";
        var (service, workspace, _, document) = Create(source);
        var position = source.LastIndexOf("    ", StringComparison.Ordinal) + 4;

        var items = service.Complete(workspace, document.Id, position);

        Assert.Contains(items, item => item.Label == "title" && item.TextEdit.NewText == "let title: String" && item.InsertTextFormat == InsertTextFormat.Snippet);
        Assert.Contains(items, item => item.Label == "load" && item.TextEdit.NewText.Contains("func load(_ id: Int) -> String", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Protocol_relationships_are_hovered_and_semantically_classified()
    {
        const string source = "protocol Named { func name() -> String }\nstruct User: Named { func name() -> String { return \"U\" } }";
        var (service, workspace, project, document) = Create(source);
        var witness = source.LastIndexOf("name()", StringComparison.Ordinal);

        var hover = service.Hover(workspace, document.Id, witness);
        var request = new ClassificationRequest
        {
            WorkspaceId = workspace.Id,
            WorkspaceVersion = workspace.Version,
            ProjectId = project.Id,
            ProjectVersion = project.Version,
            DocumentId = document.Id,
            DocumentVersion = document.Version
        };
        var spans = (await service.GetClassificationsAsync(workspace, request)).Value;

        Assert.Contains("Witness for", hover!.Markdown, StringComparison.Ordinal);
        var conformance = source.IndexOf("Named {", source.IndexOf("struct", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains(spans, span => span.Span == new TextSpan(conformance, 5) && span.Kind == ClassificationKind.ProtocolConformance);
        var requirement = source.IndexOf("name()", StringComparison.Ordinal);
        Assert.Contains(spans, span => span.Span == new TextSpan(requirement, 4) && span.Kind == ClassificationKind.ProtocolRequirement);
    }

    private static (MartinLanguageService Service, LanguageWorkspaceSnapshot Workspace, LanguageProjectSnapshot Project,
        LanguageDocumentSnapshot Document) Create(string source)
    {
        var document = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "main.martin", source, new(1));
        var project = new LanguageProjectSnapshot(ProjectId.CreateNew(), "test", ".", new(1), [document]);
        return (new(), new(WorkspaceId.CreateNew(), new(1), [project]), project, document);
    }
}
