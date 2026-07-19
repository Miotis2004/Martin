using Martin.LanguageServices;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase12SignatureHelpTests
{
    [Fact]
    public void Incomplete_nested_call_uses_direct_argument_separators()
    {
        var (service, workspace, document) = Create("""
            func inner(_ value: Int) -> Int { return value }
            func outer(first x: Int, second y: Int) -> Int { return x + y }
            func main() { outer(first: inner(1), second: 2
            """);

        var position = document.Text.Length;
        var help = service.GetSignatureHelp(workspace, document.Id, position);

        Assert.NotNull(help);
        Assert.Equal("outer", help!.Name);
        Assert.Equal(1, help.ActiveParameter);
        Assert.Contains("first: x: Int", help.Signatures[0].Label);
        Assert.Equal("Int", help.ReturnType);
    }

    [Fact]
    public void Generic_throwing_function_signature_contains_semantic_details()
    {
        var (service, workspace, document) = Create("""
            enum Failure { case bad }
            func load<T>(value item: T) throws(Failure) -> T { return item }
            func main() { try load(value: 1
            """);

        var help = service.GetSignatureHelp(workspace, document.Id, document.Text.Length);

        Assert.NotNull(help);
        Assert.Contains("load<T>", help!.Signatures[0].Label);
        Assert.Contains("throws", help.Signatures[0].Label);
        Assert.Equal("T", help.Signatures[0].ReturnType);
    }

    [Fact]
    public async Task Versioned_request_rejects_stale_document_and_observes_cancellation()
    {
        var (service, workspace, document) = Create("func f(_ x: Int) {}\nfunc main() { f(");
        var project = workspace.Projects[0];
        var request = new SignatureHelpRequest
        {
            WorkspaceId = workspace.Id,
            WorkspaceVersion = workspace.Version,
            ProjectId = project.Id,
            ProjectVersion = project.Version,
            DocumentId = document.Id,
            DocumentVersion = new(document.Version.Value + 1),
            Position = document.Text.Length,
            TriggerCharacter = '('
        };

        var result = await service.GetSignatureHelpAsync(workspace, request);
        Assert.True(result.IsStale);
        Assert.Null(result.Value);

        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetSignatureHelpAsync(workspace,
            request with { DocumentVersion = document.Version }, cancellation.Token));
    }

    private static (MartinLanguageService Service, LanguageWorkspaceSnapshot Workspace, LanguageDocumentSnapshot Document) Create(string text)
    {
        var document = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "main.martin", text, new(1));
        var project = new LanguageProjectSnapshot(ProjectId.CreateNew(), "P", Environment.CurrentDirectory, new(1), [document]);
        return (new(), new(WorkspaceId.CreateNew(), new(1), [project]), document);
    }
}
