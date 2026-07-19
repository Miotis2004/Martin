using Xunit;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Text;
using Martin.LanguageServices;
using Martin.ProjectSystem;

namespace Martin.LanguageServices.Tests;

public sealed class Phase6LanguageServicesTests
{
    [Fact]
    public void Snapshots_keep_stable_identity_and_increment_versions()
    {
        var doc = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "main.martin", "func main() {}", new(0));
        var changed = doc.Apply(new TextChange(new TextSpan(12, 0), " print(1)"));
        Assert.Equal(doc.Id, changed.Id);
        Assert.Equal(1, changed.Version.Value);
        var project = new LanguageProjectSnapshot(ProjectId.CreateNew(), "P", Environment.CurrentDirectory, new(0), [doc]);
        Assert.Equal(1, project.UpsertDocument(changed).Version.Value);
    }
    [Fact]
    public async Task Diagnostics_classification_completion_hover_navigation_references_and_signature_help_work()
    {
        var service = new MartinLanguageService();
        var doc = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "main.martin", "func add(_ x: Int) -> Int { return x }\nfunc main() { print(add(1)) }", new(0));
        var project = new LanguageProjectSnapshot(ProjectId.CreateNew(), "P", Environment.CurrentDirectory, new(0), [doc]);
        var ws = new LanguageWorkspaceSnapshot(WorkspaceId.CreateNew(), new(0), [project]);
        var analysis = await service.AnalyzeDocumentAsync(ws, doc.Id);
        Assert.DoesNotContain(analysis.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(analysis.Classifications, c => c.Kind == ClassificationKind.Keyword);
        Assert.Contains(service.Complete(ws, doc.Id, 0), c => c.DisplayText == "add");
        Assert.Contains("symbol", service.Hover(ws, doc.Id, doc.Text.IndexOf("add(1)"))!.Markdown);
        Assert.NotNull(service.GoToDefinition(ws, doc.Id, doc.Text.LastIndexOf("add", StringComparison.Ordinal)));
        Assert.True(service.FindReferences(ws, "add").Length >= 2);
        Assert.Equal("add", service.GetSignatureHelp(ws, doc.Id, doc.Text.IndexOf("1))", StringComparison.Ordinal))!.Name);
    }
    [Fact]
    public async Task Versioned_diagnostics_reject_stale_requests()
    {
        var service = new MartinLanguageService();
        var doc = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "bad.martin", "func main( {", new(2));
        var ws = new LanguageWorkspaceSnapshot(WorkspaceId.CreateNew(), new(0), [new LanguageProjectSnapshot(ProjectId.CreateNew(), "P", Environment.CurrentDirectory, new(0), [doc])]); var stale = await service.GetDiagnosticsAsync(ws, new VersionedRequest<object?>(doc.Id, new(1), null)); var fresh = await service.GetDiagnosticsAsync(ws, new VersionedRequest<object?>(doc.Id, new(2), null));
        Assert.True(stale.IsStale);
        Assert.False(fresh.IsStale);
        Assert.NotEmpty(fresh.Result);
    }
    [Fact]
    public void Formatter_produces_stable_safe_output()
    {
        var formatted = new MartinLanguageService().FormatDocument("func main(){let x=1 print(x)}");
        Assert.Contains("func main", formatted);
        Assert.EndsWith(Environment.NewLine, formatted);
    }
}
