using Martin.Compiler.Text;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase12SemanticClassificationTests
{
    [Fact]
    public async Task Classification_combines_lexical_and_bound_symbol_information()
    {
        const string source = "/// Entry point.\nfunc main() { let fixed: Int = 1 var changing: Int = fixed print(changing) }";
        var (service, workspace, project, document) = Workspace(source);

        var result = await service.GetClassificationsAsync(workspace, Request(workspace, project, document));

        Assert.False(result.IsStale);
        AssertSpan(result.Value, source, "/// Entry point.", ClassificationKind.DocumentationComment,
                   ClassificationModifiers.Documentation);
        AssertSpan(result.Value, source, "main", ClassificationKind.Function,
                   ClassificationModifiers.Declaration | ClassificationModifiers.Definition);
        AssertSpan(result.Value, source, "fixed", ClassificationKind.ImmutableVariable,
                   ClassificationModifiers.Declaration | ClassificationModifiers.Definition | ClassificationModifiers.ReadOnly);
        AssertSpan(result.Value, source, "changing", ClassificationKind.MutableVariable,
                   ClassificationModifiers.Declaration | ClassificationModifiers.Definition);
        AssertSpan(result.Value, source, "Int", ClassificationKind.Type, ClassificationModifiers.DefaultLibrary);
        AssertSpan(result.Value, source, "print", ClassificationKind.BuiltIn, ClassificationModifiers.DefaultLibrary);
    }

    [Fact]
    public async Task Classification_marks_safe_unresolved_identifiers_and_returns_normalized_spans()
    {
        const string source = "func main() { missing() // missing in comment\n let text = \"missing\" }";
        var (service, workspace, project, document) = Workspace(source);

        var spans = (await service.GetClassificationsAsync(workspace, Request(workspace, project, document))).Value;

        AssertSpan(spans, source, "missing", ClassificationKind.UnresolvedIdentifier, ClassificationModifiers.Unresolved);
        Assert.Contains(spans, span => span.Kind == ClassificationKind.Comment);
        Assert.Contains(spans, span => span.Kind == ClassificationKind.StringLiteral);
        Assert.True(spans.Zip(spans.Skip(1), (left, right) => left.Span.End <= right.Span.Start).All(value => value));
    }

    [Fact]
    public async Task Classification_rejects_a_stale_project_version()
    {
        var (service, workspace, project, document) = Workspace("func main() {} ");
        var request = Request(workspace, project, document) with { ProjectVersion = new(42) };

        var result = await service.GetClassificationsAsync(workspace, request);

        Assert.True(result.IsStale);
        Assert.Empty(result.Value);
    }

    private static void AssertSpan(IEnumerable<ClassifiedSpan> spans, string source, string text,
                                   ClassificationKind kind, ClassificationModifiers modifiers)
    {
        var start = source.IndexOf(text, StringComparison.Ordinal);
        Assert.Contains(spans, span => span.Span == new TextSpan(start, text.Length) &&
                                       span.Kind == kind && (span.Modifiers & modifiers) == modifiers);
    }

    private static ClassificationRequest Request(LanguageWorkspaceSnapshot workspace, LanguageProjectSnapshot project,
                                                 LanguageDocumentSnapshot document) => new() {
        WorkspaceId = workspace.Id,
        WorkspaceVersion = workspace.Version,
        ProjectId = project.Id,
        ProjectVersion = project.Version,
        DocumentId = document.Id,
        DocumentVersion = document.Version
    };

    private static (MartinLanguageService Service, LanguageWorkspaceSnapshot Workspace, LanguageProjectSnapshot Project,
                    LanguageDocumentSnapshot Document) Workspace(string source)
    {
        var document = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "main.martin", source, new(0));
        var project = new LanguageProjectSnapshot(ProjectId.CreateNew(), "test", ".", new(0), [document]);
        return (new(), new(WorkspaceId.CreateNew(), new(0), [project]), project, document);
    }
}
