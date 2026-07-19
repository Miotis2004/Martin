using System.Diagnostics;
using System.Text.Json;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Martin.LanguageServices;
using Xunit;
using Xunit.Abstractions;

namespace Martin.Performance.Tests;

/// <summary>
/// Repeatable, dependency-free Phase 12 benchmark modes. These are characterization
/// benchmarks rather than timing assertions: run Release builds on an idle machine and
/// compare the emitted JSON with the checked-in baseline documentation.
/// </summary>
public sealed class Phase12BenchmarkTests(ITestOutputHelper output)
{
    [Fact(Timeout = 180_000)]
    public async Task RunRequiredWorkloads()
    {
        var results = new List<BenchmarkResult>();
        Run(results, "parse-1k-lines", 1_000, () => SyntaxTree.Parse(BenchmarkData.File(1_000)));
        Run(results, "parse-10k-lines", 10_000, () => SyntaxTree.Parse(BenchmarkData.File(10_000)));
        Run(results, "compile-10-files", 10, () => Compilation.Create(BenchmarkData.Project(10, 100)).BindProgram());
        Run(results, "compile-100-files", 100, () => Compilation.Create(BenchmarkData.Project(100, 10)).BindProgram());

        var (workspace, project, document) = BenchmarkData.Workspace(1_000);
        using var cache = new LanguageAnalysisCache(new()
        {
            MaximumDocumentVersions = 8,
            MaximumProjectVersions = 2,
            ApproximateMemoryLimitBytes = 16 * 1024 * 1024
        });
        var service = new MartinLanguageService(cache);
        await RunAsync(results, "cold-analysis", 1, () => service.AnalyzeDocumentAsync(workspace, document.Id));
        await RunAsync(results, "warm-analysis", 10, async () =>
        {
            for (var i = 0; i < 10; i++) await service.AnalyzeDocumentAsync(workspace, document.Id);
        });
        Run(results, "repeated-completion", 100, () =>
        {
            for (var i = 0; i < 100; i++) _ = service.Complete(workspace, document.Id, document.Text.Length);
        });
        Run(results, "repeated-hover", 100, () =>
        {
            for (var i = 0; i < 100; i++) _ = service.Hover(workspace, document.Id, 5);
        });
        Run(results, "reference-index-and-query", 1, () => _ = service.FindReferences(workspace, "f500"));
        Run(results, "format-1k-lines", 1, () => _ = service.GetFormattingEdits(document.Text));
        await RapidEditsAsync(results, cache, service, workspace, project, document);

        var report = new BenchmarkReport(1, results, cache.Metrics);
        output.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        Assert.Equal(1, report.SchemaVersion);
        Assert.True(report.Cache.DocumentEntries <= 8, "The document cache exceeded its configured bound.");
        Assert.True(report.Cache.ProjectEntries <= 2, "The project cache exceeded its configured bound.");
        Assert.All(results, result => Assert.True(result.ElapsedMilliseconds >= 0));
    }

    [Fact(Timeout = 30_000)]
    public async Task SchedulerReportsBoundedQueueDepthAndCancellation()
    {
        await using var workspace = new BenchmarkWorkspace(BenchmarkData.Workspace(10).Workspace);
        await using var scheduler = new AnalysisScheduler(workspace, new()
        {
            MaximumConcurrency = 1,
            MaximumQueuedRequests = 4
        });
        var snapshot = workspace.CurrentSnapshot;
        var project = snapshot.Projects[0];
        var document = project.Documents[0];
        var requests = Enumerable.Range(0, 12).Select(i => scheduler.RunAsync(
            BenchmarkData.Request(snapshot, project, document, i), AnalysisPriority.Background,
            async (_, cancellationToken) => { await Task.Delay(5, cancellationToken); return i; })).ToArray();

        await Task.WhenAll(requests);
        Assert.InRange(scheduler.Metrics.PeakQueueDepth, 1, 4);
        Assert.True(scheduler.Metrics.Rejected > 0);

        using var cancellation = new CancellationTokenSource();
        var cancelled = scheduler.RunAsync(BenchmarkData.Request(snapshot, project, document, 0),
            AnalysisPriority.Immediate,
            async (_, cancellationToken) => { await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken); return 0; },
            cancellation.Token);
        await Task.Delay(10);
        var timer = Stopwatch.StartNew();
        cancellation.Cancel();
        var cancelledResult = await cancelled;
        timer.Stop();
        output.WriteLine($"Cancellation observed in {timer.Elapsed.TotalMilliseconds:F3} ms");
        Assert.True(cancelledResult.IsCancelled);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(1));
    }

    static async Task RapidEditsAsync(List<BenchmarkResult> results, LanguageAnalysisCache cache,
        MartinLanguageService service, LanguageWorkspaceSnapshot workspace,
        LanguageProjectSnapshot project, LanguageDocumentSnapshot document)
    {
        var timer = Stopwatch.StartNew();
        var before = GC.GetTotalAllocatedBytes(true);
        for (var i = 1; i <= 25; i++)
        {
            var nextDocument = document with { Text = document.Text + $"\nfunc edit{i}() {{ }}", Version = new(i) };
            var nextProject = project with { Documents = [nextDocument], Version = new(i) };
            var nextWorkspace = workspace with { Projects = [nextProject], Version = new(i) };
            await service.AnalyzeDocumentAsync(nextWorkspace, nextDocument.Id);
        }
        timer.Stop();
        results.Add(new("rapid-edits", 25, timer.Elapsed.TotalMilliseconds,
            GC.GetTotalAllocatedBytes(false) - before, cache.Metrics.DocumentEntries, cache.Metrics.ProjectEntries));
    }

    static void Run(List<BenchmarkResult> results, string name, int operations, Action action)
    {
        action(); // JIT warm-up is deliberately excluded.
        var before = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        action();
        timer.Stop();
        results.Add(new(name, operations, timer.Elapsed.TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - before));
    }

    static async Task RunAsync(List<BenchmarkResult> results, string name, int operations, Func<Task> action)
    {
        var before = GC.GetTotalAllocatedBytes(true);
        var timer = Stopwatch.StartNew();
        await action();
        timer.Stop();
        results.Add(new(name, operations, timer.Elapsed.TotalMilliseconds,
            GC.GetTotalAllocatedBytes(false) - before));
    }
}

public sealed record BenchmarkResult(string Name, int Operations, double ElapsedMilliseconds,
    long AllocatedBytes, int? DocumentCacheEntries = null, int? ProjectCacheEntries = null);
public sealed record BenchmarkReport(int SchemaVersion, IReadOnlyList<BenchmarkResult> Results,
    AnalysisCacheMetrics Cache);

internal static class BenchmarkData
{
    public static string File(int lines, int offset = 0) => string.Join('\n', Enumerable.Range(0, lines)
        .Select(i => $"func f{i + offset}() -> Int {{ return {i + offset} }}"));

    public static SyntaxTree[] Project(int files, int linesPerFile) => Enumerable.Range(0, files)
        .Select(file => SyntaxTree.Parse(File(linesPerFile, file * linesPerFile), $"file{file:D3}.martin")).ToArray();

    public static (LanguageWorkspaceSnapshot Workspace, LanguageProjectSnapshot Project,
        LanguageDocumentSnapshot Document) Workspace(int lines)
    {
        var projectId = ProjectId.CreateNew();
        var document = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "benchmark.martin", File(lines), new(0))
        { ProjectId = projectId };
        var project = new LanguageProjectSnapshot(projectId, "benchmark", ".", new(0), [document]);
        return (new(WorkspaceId.CreateNew(), new(0), [project]), project, document);
    }

    public static CompletionRequest Request(LanguageWorkspaceSnapshot workspace,
        LanguageProjectSnapshot project, LanguageDocumentSnapshot document, int position) => new()
        {
            WorkspaceId = workspace.Id,
            WorkspaceVersion = workspace.Version,
            ProjectId = project.Id,
            ProjectVersion = project.Version,
            DocumentId = document.Id,
            DocumentVersion = document.Version,
            Position = Math.Min(position, document.Text.Length)
        };
}

internal sealed class BenchmarkWorkspace(LanguageWorkspaceSnapshot snapshot) : ILanguageWorkspace
{
    public LanguageWorkspaceSnapshot CurrentSnapshot { get; } = snapshot;
    public event EventHandler<LanguageWorkspaceChangedEventArgs>? WorkspaceChanged { add { } remove { } }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public Task<ProjectId> OpenProjectAsync(Martin.ProjectSystem.MartinProject project, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task CloseProjectAsync(ProjectId projectId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<DocumentId> OpenDocumentAsync(ProjectId projectId, string filePath, Martin.Compiler.Text.SourceText text, DocumentVersion editorVersion, bool isDirty, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<DocumentChangeResult> ApplyDocumentChangesAsync(DocumentChange change, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task ReplaceDocumentTextAsync(DocumentId documentId, DocumentVersion previousVersion, DocumentVersion newVersion, Martin.Compiler.Text.SourceText text, bool isDirty, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SaveDocumentAsync(DocumentId documentId, Martin.Compiler.Text.SourceText text, DocumentVersion editorVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task CloseDocumentAsync(DocumentId documentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task RefreshProjectAsync(ProjectId projectId, Martin.ProjectSystem.MartinProject project, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task RenameDocumentAsync(DocumentId documentId, string newFilePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
