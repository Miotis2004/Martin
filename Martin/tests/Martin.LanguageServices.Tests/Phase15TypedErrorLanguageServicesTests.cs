using Martin.Compiler.Text;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase15TypedErrorLanguageServicesTests
{
    [Fact]
    public void Completion_filters_throws_clause_to_error_types_and_offers_throw_only_in_throwing_bodies()
    {
        const string source = """
protocol Error {}
enum FileError: Error { case notFound(path: String) }
struct Plain {}
func load() throws  -> String { throw FileError.notFound(path: "x") }
func main() {  }
""";
        var (service, workspace, document) = Workspace(source);

        var throwsItems = service.Complete(workspace, document.Id, source.IndexOf(" -> String", StringComparison.Ordinal));
        Assert.Contains(throwsItems, item => item.Label == "FileError");
        Assert.DoesNotContain(throwsItems, item => item.Label == "Plain");

        var throwingBodyItems = service.Complete(workspace, document.Id, source.IndexOf("throw FileError", StringComparison.Ordinal));
        Assert.Contains(throwingBodyItems, item => item.Label == "throw");

        var mainBodyItems = service.Complete(workspace, document.Id, source.LastIndexOf("  }", StringComparison.Ordinal) + 1);
        Assert.DoesNotContain(mainBodyItems, item => item.Label == "throw");
    }

    [Fact]
    public void Catch_completion_hover_classification_navigation_references_and_diagnostics_use_typed_error_semantics()
    {
        const string source = """
protocol Error {}
enum FileError: Error { case notFound(path: String) case accessDenied(path: String) }
func load() throws FileError -> String { throw FileError.notFound(path: "x") }
func main() {
    do {
        let text = try load()
    } catch . {
    }
}
""";
        var (service, workspace, document) = Workspace(source);
        var catchDot = source.IndexOf("catch .", StringComparison.Ordinal) + "catch .".Length;

        var completions = service.Complete(workspace, document.Id, catchDot);
        Assert.Contains(completions, item => item.Label == ".notFound" && item.InsertTextFormat == InsertTextFormat.Snippet);
        Assert.Contains(completions, item => item.Label == ".accessDenied");

        var tryHover = service.Hover(workspace, document.Id, source.IndexOf("try", StringComparison.Ordinal));
        Assert.NotNull(tryHover);
        Assert.Contains("throws FileError", tryHover!.Markdown);

        var catchHover = service.Hover(workspace, document.Id, source.IndexOf("catch", StringComparison.Ordinal));
        Assert.NotNull(catchHover);
        Assert.Contains("FileError", catchHover!.Markdown);

        var classifications = service.GetClassificationsAsync(workspace, new ClassificationRequest
        {
            WorkspaceId = workspace.Id,
            WorkspaceVersion = workspace.Version,
            ProjectId = workspace.Projects[0].Id,
            ProjectVersion = workspace.Projects[0].Version,
            DocumentId = document.Id,
            DocumentVersion = document.Version
        }).GetAwaiter().GetResult().Value;
        Assert.Contains(classifications, span => span.Span == new TextSpan(source.IndexOf("FileError", StringComparison.Ordinal), "FileError".Length) && span.Kind == ClassificationKind.Enum);
        Assert.Contains(classifications, span => span.Span == new TextSpan(source.IndexOf("notFound", StringComparison.Ordinal), "notFound".Length) && span.Kind == ClassificationKind.EnumCase);

        var definition = service.GoToDefinition(workspace, document.Id, source.LastIndexOf("notFound", StringComparison.Ordinal));
        Assert.NotNull(definition);
        Assert.Equal(new TextSpan(source.IndexOf("notFound", StringComparison.Ordinal), "notFound".Length), definition!.Span);

        var references = service.FindReferences(workspace, "FileError");
        Assert.Contains(references, reference => reference.Kind == ReferenceKind.Type && reference.Span.Start == source.IndexOf("FileError ->", StringComparison.Ordinal));

        var analysis = service.AnalyzeDocumentAsync(workspace, document.Id).GetAwaiter().GetResult();
        Assert.Contains(analysis.Diagnostics, diagnostic => diagnostic.Code is "MRT2193" or "MRT2197");
    }

    [Fact]
    public void Signature_help_preserves_typed_error_signature()
    {
        const string source = """
protocol Error {}
enum DecodeError<T>: Error { case invalid(value: T) }
func decode(_ text: String) throws DecodeError<Int> -> Int { throw DecodeError<Int>.invalid(value: 1) }
func main() { let value: Int = try decode( }
""";
        var (service, workspace, document) = Workspace(source);
        var help = service.GetSignatureHelp(workspace, document.Id, document.Text.Length);
        Assert.NotNull(help);
        Assert.Contains(help!.Signatures, signature => signature.Label.Contains("throws DecodeError<Int>", StringComparison.Ordinal));
    }


    [Fact]
    public void NavigationAndReferencesIncludeThrowsClauseErrorTypes()
    {
        const string source = """
protocol Error {}
enum FileError: Error { case missing }
func load() throws FileError -> String { throw FileError.missing }
""";
        var (service, workspace, document) = Workspace(source);
        var throwsReference = source.IndexOf("FileError ->", StringComparison.Ordinal);

        var definition = service.GoToDefinition(workspace, document.Id, throwsReference);
        Assert.NotNull(definition);
        Assert.Equal(new TextSpan(source.IndexOf("FileError:", StringComparison.Ordinal), "FileError".Length), definition!.Span);

        var references = service.FindReferences(workspace, "FileError");
        Assert.Contains(references, reference => reference.Kind == ReferenceKind.Type && reference.Span.Start == throwsReference);
    }

    private static (MartinLanguageService Service, LanguageWorkspaceSnapshot Workspace, LanguageDocumentSnapshot Document) Workspace(string source)
    {
        var service = new MartinLanguageService();
        var document = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "main.martin", source, new(15));
        var project = new LanguageProjectSnapshot(ProjectId.CreateNew(), "Phase15", Environment.CurrentDirectory, new(15), [document]);
        var workspace = new LanguageWorkspaceSnapshot(WorkspaceId.CreateNew(), new(15), [project]);
        return (service, workspace, document);
    }
}
