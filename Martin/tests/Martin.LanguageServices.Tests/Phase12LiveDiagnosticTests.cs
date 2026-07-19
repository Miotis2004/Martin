using Martin.Compiler.Text;
using Martin.LanguageServices;
using Martin.ProjectSystem;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase12LiveDiagnosticTests
{
    [Fact]
    public async Task Syntax_and_semantic_snapshots_preserve_exact_document_versions()
    {
        var (workspace, projectId, documentId) = await CreateWorkspaceAsync("func main( {");
        await using var scheduler = new AnalysisScheduler(workspace, new AnalysisSchedulerOptions
        {
            SyntaxDiagnosticsDebounce = TimeSpan.Zero,
            ProjectAnalysisDebounce = TimeSpan.Zero
        });
        await using var publisher = new LiveDiagnosticPublisher(workspace, scheduler, new MartinLanguageService());
        var syntax = WaitForSnapshot(publisher, LanguageDiagnosticSource.LiveSyntax);
        var semantic = WaitForSnapshot(publisher, LanguageDiagnosticSource.LiveSemantic);

        publisher.ScheduleProject(projectId);

        var syntaxSnapshot = await syntax.WaitAsync(TimeSpan.FromSeconds(5));
        var semanticSnapshot = await semantic.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(workspace.CurrentSnapshot.FindProject(projectId)!.Version, syntaxSnapshot.ProjectVersion);
        Assert.Equal(new DocumentVersion(0), syntaxSnapshot.Documents[documentId].DocumentVersion);
        Assert.NotEmpty(syntaxSnapshot.Documents[documentId].Diagnostics);
        Assert.All(syntaxSnapshot.Documents[documentId].Diagnostics, d => Assert.Equal(LanguageDiagnosticSource.LiveSyntax, d.DiagnosticSource));
        Assert.Equal(new DocumentVersion(0), semanticSnapshot.Documents[documentId].DocumentVersion);
    }

    [Fact]
    public async Task Fixed_diagnostics_are_replaced_and_stale_work_cannot_reappear()
    {
        var (workspace, projectId, documentId) = await CreateWorkspaceAsync("func main( {");
        await using var scheduler = new AnalysisScheduler(workspace, new AnalysisSchedulerOptions
        {
            SyntaxDiagnosticsDebounce = TimeSpan.FromMilliseconds(10),
            ProjectAnalysisDebounce = TimeSpan.FromMilliseconds(10)
        });
        await using var publisher = new LiveDiagnosticPublisher(workspace, scheduler, new MartinLanguageService());
        publisher.ScheduleProject(projectId);
        var oldProjectVersion = workspace.CurrentSnapshot.FindProject(projectId)!.Version;
        var replacement = WaitForSnapshot(publisher, LanguageDiagnosticSource.LiveSyntax, s =>
            s.Documents.TryGetValue(documentId, out var set) && set.DocumentVersion == new DocumentVersion(1));

        await workspace.ReplaceDocumentTextAsync(documentId, new(0), new(1), SourceText.From("func main() { return }", "main.martin"), true);

        var snapshot = await replacement.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(snapshot.Documents[documentId].Diagnostics);
        Assert.NotEqual(oldProjectVersion, snapshot.ProjectVersion);
        Assert.DoesNotContain(publisher.CurrentSnapshots, s => s.ProjectId == projectId && s.ProjectVersion == oldProjectVersion);
    }

    [Fact]
    public async Task Closing_a_project_publishes_empty_live_snapshots_and_clears_state()
    {
        var (workspace, projectId, _) = await CreateWorkspaceAsync("func main() { return }");
        await using var scheduler = new AnalysisScheduler(workspace);
        await using var publisher = new LiveDiagnosticPublisher(workspace, scheduler, new MartinLanguageService());
        var clearedSyntax = WaitForSnapshot(publisher, LanguageDiagnosticSource.LiveSyntax, s => s.Documents.Count == 0);
        var clearedSemantic = WaitForSnapshot(publisher, LanguageDiagnosticSource.LiveSemantic, s => s.Documents.Count == 0);

        await workspace.CloseProjectAsync(projectId);

        await clearedSyntax.WaitAsync(TimeSpan.FromSeconds(5));
        await clearedSemantic.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(publisher.CurrentSnapshots);
    }

    static Task<DiagnosticSnapshot> WaitForSnapshot(LiveDiagnosticPublisher publisher, LanguageDiagnosticSource source,
        Func<DiagnosticSnapshot, bool>? predicate = null)
    {
        var completion = new TaskCompletionSource<DiagnosticSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        publisher.SnapshotPublished += Handler;
        return completion.Task;
        void Handler(object? sender, DiagnosticSnapshotEventArgs args)
        {
            if (args.Snapshot.Source != source || !(predicate?.Invoke(args.Snapshot) ?? true)) return;
            publisher.SnapshotPublished -= Handler;
            completion.TrySetResult(args.Snapshot);
        }
    }

    static async Task<(LanguageWorkspace Workspace, ProjectId ProjectId, DocumentId DocumentId)> CreateWorkspaceAsync(string text)
    {
        var root = Path.Combine(Path.GetTempPath(), "martin-live-diagnostics", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "main.martin");
        await File.WriteAllTextAsync(source, text);
        var project = new MartinProject
        {
            ManifestPath = Path.Combine(root, "martin.json"),
            RootDirectory = root,
            Manifest = new MartinManifest
            {
                ManifestVersion = 1,
                Package = new PackageSection("LiveDiagnostics", "1.0.0"),
                Target = new TargetSection("executable", "net8.0", "main.martin")
            },
            SourceFiles = [source]
        };
        var workspace = new LanguageWorkspace();
        var projectId = await workspace.OpenProjectAsync(project);
        return (workspace, projectId, workspace.CurrentSnapshot.FindProject(projectId)!.Documents.Single().Id);
    }
}
