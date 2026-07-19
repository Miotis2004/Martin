using Xunit;
using Martin.Compiler.Diagnostics;
using Martin.LanguageServices;

namespace Martin.LanguageServices.Tests;

public sealed class Phase7BLanguageServicesTests
{
    [Fact]
    public async Task Optionals_are_classified_completed_and_formatted()
    {
        var service = new MartinLanguageService();
        var doc = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "main.martin", "func main(){let value:Int?=nil if let x=value{print(x)}}", new(0));
        var ws = new LanguageWorkspaceSnapshot(WorkspaceId.CreateNew(), new(0), [new LanguageProjectSnapshot(ProjectId.CreateNew(), "P", Environment.CurrentDirectory, new(0), [doc])]);

        var analysis = await service.AnalyzeDocumentAsync(ws, doc.Id);

        Assert.DoesNotContain(analysis.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(analysis.Classifications, c => c.Kind == ClassificationKind.Keyword);
        Assert.Contains(service.Complete(ws, doc.Id, 0), c => c.DisplayText == "Int?");
        Assert.Contains(service.Complete(ws, doc.Id, 0), c => c.DisplayText == "if let");
        Assert.Contains("Int?", service.FormatDocument(doc.Text));
    }
}
