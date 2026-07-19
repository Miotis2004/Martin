using System.Collections.Immutable;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase12NavigationTests
{
    [Fact]
    public async Task DefinitionAndReferencesUseBoundIdentityAcrossClosedDocuments()
    {
        const string declaration = "func answer() -> Int { return 42 }";
        const string usage = "func main() -> Int { return answer() } // answer";
        var (workspace, project, declarationDocument, usageDocument) = Workspace(declaration, usage);
        var service = new MartinLanguageService();
        var position = usage.IndexOf("answer()", StringComparison.Ordinal);

        var definitions = await service.GetDefinitionsAsync(workspace,
                                                            new DefinitionRequest {
                                                                WorkspaceId = workspace.Id,
                                                                WorkspaceVersion = workspace.Version,
                                                                ProjectId = project.Id,
                                                                ProjectVersion = project.Version,
                                                                DocumentId = usageDocument.Id,
                                                                DocumentVersion = usageDocument.Version,
                                                                Position = position
                                                            });
        var references = await service.FindReferencesAsync(workspace,
                                                           ReferenceRequest(workspace, project, usageDocument, position));

        var definition = Assert.Single(definitions.Value);
        Assert.Equal(declarationDocument.Id, definition.DocumentId);
        Assert.Equal("answer", declaration[definition.Span.Start..definition.Span.End]);
        Assert.Equal([ReferenceKind.Declaration, ReferenceKind.Call],
                     references.Value.Select(reference => reference.Kind).Order().ToArray());
        Assert.DoesNotContain(references.Value, reference =>
                                                    reference.DocumentId == usageDocument.Id && reference.Span.Start == usage.LastIndexOf("answer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HomonymsDeclarationExclusionStalenessAndCancellationAreHonored()
    {
        const string source = "func first() { let value = 1 print(value) } func second() { let value = 2 print(value) }";
        var (workspace, project, _, document) = Workspace(string.Empty, source);
        var service = new MartinLanguageService();
        var position = source.IndexOf("print(value)", StringComparison.Ordinal) + "print(".Length;
        var request = ReferenceRequest(workspace, project, document, position) with { IncludeDeclaration = false };

        var references = await service.FindReferencesAsync(workspace, request);
        Assert.Single(references.Value);
        Assert.Equal(position, references.Value[0].Span.Start);

        var stale = await service.FindReferencesAsync(workspace, request with { DocumentVersion = new(99) });
        Assert.True(stale.IsStale);
        Assert.Empty(stale.Value);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                                                                    service.FindReferencesAsync(workspace, request, cancellation.Token));
    }

    private static ReferencesRequest ReferenceRequest(LanguageWorkspaceSnapshot workspace,
                                                      LanguageProjectSnapshot project, LanguageDocumentSnapshot document, int position) => new() {
        WorkspaceId = workspace.Id,
        WorkspaceVersion = workspace.Version,
        ProjectId = project.Id,
        ProjectVersion = project.Version,
        DocumentId = document.Id,
        DocumentVersion = document.Version,
        Position = position
    };

    private static (LanguageWorkspaceSnapshot, LanguageProjectSnapshot, LanguageDocumentSnapshot, LanguageDocumentSnapshot)
        Workspace(string declaration, string usage)
    {
        var projectId = ProjectId.CreateNew();
        var first = new LanguageDocumentSnapshot(DocumentId.CreateNew(), Path.GetFullPath("closed.martin"), declaration, new(0)) { ProjectId = projectId, IsOpen = false };
        var second = new LanguageDocumentSnapshot(DocumentId.CreateNew(), Path.GetFullPath("open.martin"), usage, new(0)) { ProjectId = projectId, IsOpen = true };
        var project = new LanguageProjectSnapshot(projectId, "Navigation", Environment.CurrentDirectory, new(3), [first, second]);
        return (new(WorkspaceId.CreateNew(), new(4), [project]), project, first, second);
    }
}
